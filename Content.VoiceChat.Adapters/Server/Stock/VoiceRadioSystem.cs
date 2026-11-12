// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Reflection;
using Content.Server.Power.Components;
using Content.Server.Radio;
using Content.VoiceChat.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
#if VOICECHAT_SERVER_COMPONENTS
using Content.Server.Radio.Components;
#endif
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Shared.Player;
#if VOICECHAT_RADIO_EXTRAS
using Content.Shared.Whitelist;
#endif
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.VoiceChat.Server;

public sealed partial class VoiceRadioSystem : EntitySystem, IVoiceFeature
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VoiceFeatures _features = default!;
#if VOICECHAT_RADIO_EXTRAS
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
#endif
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private readonly List<ProtoId<RadioChannelPrototype>> _channels = new();
    private readonly List<ProtoId<RadioChannelPrototype>> _receive = new();
    private readonly HashSet<EntityUid> _tracked = new();
    private readonly HashSet<EntityUid> _current = new();
    private bool _enabled;
    private bool _common;
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<VoiceRadioSelectEvent>(OnSelect);
        _features.Add(this);

        Subs.CVar(_cfg, VoiceCVars.VoiceChatEnabled, _ => Refresh(), true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatRadioEnabled, _ => Refresh(), true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatRadioCommon, _ => Refresh(), true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;
        Refresh();
    }

    public bool TryGetTransmission(EntityUid speaker, out RadioChannelPrototype channel, out EntityUid radioSource)
    {
        return TryGetTransmission(speaker, false, out channel, out radioSource);
    }

    public bool TryGetTransmission(EntityUid speaker, bool force, out RadioChannelPrototype channel, out EntityUid radioSource)
    {
        channel = default!;
        radioSource = default;

        if (!_enabled ||
            !TryComp<VoiceRadioComponent>(speaker, out var voiceRadio) ||
            (voiceRadio.Active ?? (force && voiceRadio.Channels.Count > 0 ? (ProtoId<RadioChannelPrototype>?) voiceRadio.Channels[0] : null)) is not { } active ||
            !_common && active == SharedChatSystem.CommonChannel ||
            !_prototype.TryIndex(active, out var prototype) ||
            !CanTransmit(speaker, prototype, out radioSource))
        {
            return false;
        }

        channel = prototype;
        return true;
    }

    public bool TryGetMicrophoneChannel(ProtoId<RadioChannelPrototype> id, out RadioChannelPrototype channel)
    {
        channel = default!;
        if (!_enabled ||
            !_common && id == SharedChatSystem.CommonChannel ||
            !_prototype.TryIndex(id, out var prototype))
        {
            return false;
        }

        channel = prototype;
        return true;
    }

    public void Contribute(VoiceContribution contribution)
    {
        if (!_enabled || contribution.Ghost)
            return;

        var localOnly = contribution.LocalPtt && !contribution.RadioPtt;
        if (!localOnly && TryGetTransmission(contribution.SpeakerUid, contribution.RadioPtt, out var channel, out var radioSource))
            AddListen(contribution, channel, radioSource);

        CollectMicrophones(contribution);
    }

    private void CollectMicrophones(VoiceContribution contribution)
    {
        var speaker = contribution.SpeakerUid;
        var speakerXform = Transform(speaker);
        var position = _transform.GetWorldPosition(speakerXform);

        var query = EntityQueryEnumerator<RadioMicrophoneComponent, ActiveListenerComponent, TransformComponent>();
        while (query.MoveNext(out var microphone, out var microphoneComp, out var listener, out var xform))
        {
            if (!microphoneComp.Enabled ||
                xform.MapID != speakerXform.MapID ||
                (_transform.GetWorldPosition(xform) - position).LengthSquared() > listener.Range * listener.Range)
            {
                continue;
            }

            var attempt = new ListenAttemptEvent(speaker);
            RaiseLocalEvent(microphone, attempt);
            if (attempt.Cancelled || !TryGetMicrophoneChannel(microphoneComp.BroadcastChannel, out var channel))
                continue;

            AddListen(contribution, channel, microphone);
        }
    }

    private void AddListen(VoiceContribution contribution, RadioChannelPrototype channel, EntityUid radioSource)
    {
        foreach (var existing in contribution.Radios)
        {
            if (existing.ChannelId == channel.ID)
                return;
        }

        var listen = new VoiceRadioListen
        {
            ChannelId = channel.ID,
            LocalizedName = channel.LocalizedName,
        };

        var receivers = new List<EntityUid>();
        GetReceivers(radioSource, channel, receivers);
        foreach (var receiver in receivers)
        {
            if (receiver == radioSource)
                continue;

            if (TryComp<ActorComponent>(receiver, out var actor))
            {
                listen.Recipients.Add(actor.PlayerSession);
                continue;
            }

            if (TryComp<HeadsetComponent>(receiver, out var headset))
            {
                if (headset.IsEquipped && TryComp<ActorComponent>(Transform(receiver).ParentUid, out var wearer))
                    listen.Recipients.Add(wearer.PlayerSession);
                continue;
            }

            if (TryComp<RadioSpeakerComponent>(receiver, out var radioSpeaker) && radioSpeaker.Enabled)
            {
                var origin = _transform.GetMapCoordinates(receiver);
                if (origin.MapId == MapId.Nullspace)
                    continue;

                var normalRange = _cfg.GetCVar(VoiceCVars.VoiceChatRange);
#if VOICECHAT_RADIO_EXTRAS
                var range = radioSpeaker.SpeakNormally
                    ? normalRange
                    : _cfg.GetCVar(VoiceCVars.VoiceChatWhisperRange);
#else
                var range = normalRange;
#endif
                listen.Speakers.Add(new VoiceRelay(receiver, origin, range, VoiceRoute.RadioSpeaker));
            }
        }

        contribution.Radios.Add(listen);
    }

    public void DescribeState(EntityUid speaker, VoiceStateHints hints)
    {
        if (TryGetTransmission(speaker, out var channel, out _))
            hints.Radio = channel.LocalizedName;
    }

    private static MethodInfo? _frequency;
    private static bool _frequencyChecked;

    public void GetReceivers(EntityUid radioSource, RadioChannelPrototype channel, List<EntityUid> receivers)
    {
        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        if (sendAttemptEv.Cancelled)
            return;

        if (!_frequencyChecked)
        {
            _frequencyChecked = true;
            _frequency = _radio.GetType().GetMethod("GetFrequency", new[] { typeof(EntityUid), typeof(RadioChannelPrototype) });
        }

        var sourceMapId = Transform(radioSource).MapID;
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);
        var sourceServerExempt = HasComp<TelecomExemptComponent>(radioSource);
        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (_frequency != null && !VoiceContentTypes.IsGhost(EntityManager, receiver))
            {
                var sent = _frequency.Invoke(_radio, new object[] { radioSource, channel });
                var heard = _frequency.Invoke(_radio, new object[] { receiver, channel });
                if (!Equals(sent, heard))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            if (!channel.LongRange && !sourceServerExempt && !hasActiveServer)
                continue;

            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            receivers.Add(receiver);
        }
    }

    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId && power.Powered && keys.Channels.Contains(channelId))
                return true;
        }

        return false;
    }

    private void Refresh()
    {
        _enabled = _cfg.GetCVar(VoiceCVars.VoiceChatEnabled) && _cfg.GetCVar(VoiceCVars.VoiceChatRadioEnabled);
        _common = _cfg.GetCVar(VoiceCVars.VoiceChatRadioCommon);

        _current.Clear();
        foreach (var session in _player.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } uid)
                continue;

            _current.Add(uid);
            RefreshEntity(uid);
        }

        foreach (var uid in _tracked)
        {
            if (!_current.Contains(uid) && !TerminatingOrDeleted(uid))
                RemComp<VoiceRadioComponent>(uid);
        }

        _tracked.Clear();
        _tracked.UnionWith(_current);
    }

    private void RefreshEntity(EntityUid uid)
    {
        _channels.Clear();
        _receive.Clear();
        if (_enabled)
        {
            CollectChannels(uid);
            CollectReceive(uid);
        }

        if (_channels.Count == 0 && _receive.Count == 0)
        {
            RemComp<VoiceRadioComponent>(uid);
            return;
        }

        var voiceRadio = EnsureComp<VoiceRadioComponent>(uid);
        var active = voiceRadio.Active is { } current && _channels.Contains(current) ? current : (ProtoId<RadioChannelPrototype>?) null;
        if (voiceRadio.Channels.SequenceEqual(_channels) &&
            voiceRadio.Receive.SequenceEqual(_receive) &&
            voiceRadio.Active == active)
        {
            return;
        }

        voiceRadio.Channels = new List<ProtoId<RadioChannelPrototype>>(_channels);
        voiceRadio.Receive = new List<ProtoId<RadioChannelPrototype>>(_receive);
        voiceRadio.Active = active;
        Dirty(uid, voiceRadio);
    }

    private void CollectChannels(EntityUid uid)
    {
        if (TryComp<WearingHeadsetComponent>(uid, out var wearing) &&
            TryComp<HeadsetComponent>(wearing.Headset, out var headset) &&
            headset.Enabled &&
            TryComp<EncryptionKeyHolderComponent>(wearing.Headset, out var keys))
        {
            AddChannels(uid, ChannelIds(keys.Channels));
        }

        if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var intrinsic))
            AddChannels(uid, ChannelIds(intrinsic.Channels));

        _channels.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }

    private void AddChannels(EntityUid uid, IEnumerable<string> channels)
    {
        foreach (var raw in channels)
        {
            var id = new ProtoId<RadioChannelPrototype>(raw);
            if (!_common && id == SharedChatSystem.CommonChannel)
                continue;

            if (!_prototype.TryIndex(id, out var channel) || !PassesSend(channel, uid))
            {
                continue;
            }

            if (!_channels.Contains(id))
                _channels.Add(id);
        }
    }

    private void CollectReceive(EntityUid uid)
    {
        if (TryComp<WearingHeadsetComponent>(uid, out var wearing) &&
            TryComp<ActiveRadioComponent>(wearing.Headset, out var headsetRadio))
        {
            AddReceive(uid, headsetRadio);
        }

        if (TryComp<ActiveRadioComponent>(uid, out var intrinsic))
            AddReceive(uid, intrinsic);

        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (TryComp<RadioSpeakerComponent>(item, out var speaker) && speaker.Enabled)
                AddReceive(uid, ChannelIds(speaker.Channels));
        }

        _receive.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }

    private void AddReceive(EntityUid uid, ActiveRadioComponent radio)
    {
        if (!radio.ReceiveAllChannels)
        {
            AddReceive(uid, ChannelIds(radio.Channels));
            return;
        }

        foreach (var channel in _prototype.EnumeratePrototypes<RadioChannelPrototype>())
        {
            AddReceive(uid, new[] { channel.ID });
        }
    }

    private void AddReceive(EntityUid uid, IEnumerable<string> channels)
    {
        foreach (var raw in channels)
        {
            var id = new ProtoId<RadioChannelPrototype>(raw);
            if (!_common && id == SharedChatSystem.CommonChannel)
                continue;

            if (!_prototype.TryIndex(id, out var channel) || !PassesReceive(channel, uid))
            {
                continue;
            }

            if (!_receive.Contains(id))
                _receive.Add(id);
        }
    }

    private static IEnumerable<string> ChannelIds(IEnumerable<ProtoId<RadioChannelPrototype>> channels)
    {
        return channels.Select(id => id.Id);
    }

#if VOICECHAT_SERVER_COMPONENTS
    private static IEnumerable<string> ChannelIds(IEnumerable<string> channels)
    {
        return channels;
    }
#endif

    private bool PassesSend(RadioChannelPrototype channel, EntityUid uid)
    {
#if VOICECHAT_RADIO_EXTRAS
        return _whitelist.IsWhitelistPassOrNull(channel.SendWhitelist, uid);
#else
        return true;
#endif
    }

    private bool PassesReceive(RadioChannelPrototype channel, EntityUid uid)
    {
#if VOICECHAT_RADIO_EXTRAS
        return _whitelist.IsWhitelistPassOrNull(channel.ReceiveWhitelist, uid);
#else
        return true;
#endif
    }

    private bool CanTransmit(EntityUid uid, RadioChannelPrototype channel, out EntityUid radioSource)
    {
        radioSource = default;
        if (!PassesSend(channel, uid))
            return false;

        if (TryComp<WearingHeadsetComponent>(uid, out var wearing) &&
            TryComp<HeadsetComponent>(wearing.Headset, out var headset) &&
            headset.Enabled &&
            TryComp<EncryptionKeyHolderComponent>(wearing.Headset, out var keys) &&
            keys.Channels.Contains(channel.ID))
        {
            radioSource = wearing.Headset;
            return true;
        }

        if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var intrinsic) &&
            intrinsic.Channels.Contains(channel.ID))
        {
            radioSource = uid;
            return true;
        }

        return false;
    }

    private void OnSelect(VoiceRadioSelectEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid ||
            !TryComp<VoiceRadioComponent>(uid, out var voiceRadio))
        {
            return;
        }

        if (ev.Channel is { } channel && !voiceRadio.Channels.Contains(channel))
            return;

        if (voiceRadio.Active == ev.Channel)
            return;

        voiceRadio.Active = ev.Channel;
        Dirty(uid, voiceRadio);
    }
}
