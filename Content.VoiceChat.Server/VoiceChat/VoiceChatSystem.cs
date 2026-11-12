// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Reflection;
using Content.VoiceChat.Shared;
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Tag;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.VoiceChat.Server;

public sealed partial class VoiceChatSystem : EntitySystem
{
    [Dependency] private VoiceChatManager _voice = default!;
    [Dependency] private VoiceFeatures _features = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IEntitySystemManager _systems = default!;

    private static readonly TimeSpan TransmissionGap = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PermissionRecheck = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RefusalDisplay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StateRefreshInterval = TimeSpan.FromMilliseconds(500);
    private const float ListenerMargin = 2f;
    private const float WhisperRange = 3f;
    private static readonly ProtoId<TagPrototype> MuzzleTag = "Muzzle";

    private readonly Dictionary<NetUserId, Speaker> _speakers = new();
    private readonly HashSet<NetUserId> _adminMuted = new();
    private readonly Dictionary<NetUserId, Route> _routes = new();
    private readonly Dictionary<string, VoiceRadioMixer> _mixers = new();
    private readonly List<string> _idleMixers = new();
    private readonly HashSet<ushort> _speakerIds = new();
    private readonly List<ICommonSession> _lobbySessions = new();
    private readonly List<INetChannel> _lobbyChannels = new();
    private bool _lobbyCollected;

    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<SpeechComponent> _speechQuery;
    private ICommonSession[]? _sessions;
    private ushort _nextSpeakerId = 1;
    private float _range;
    private bool _lobby;
    private bool _dynamicRange;
    private float _shoutRange;
    private float _whisperRange;
    private float _shoutThreshold;
    private float _whisperThreshold;
    private VoiceFormat _format;
    private TimeSpan _nextStateRefresh;

    public override void Initialize()
    {
        base.Initialize();

        _eyeQuery = GetEntityQuery<EyeComponent>();
        _speechQuery = GetEntityQuery<SpeechComponent>();

        Subs.CVar(_cfg, VoiceCVars.VoiceChatRange, value => _range = value, true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatLobby, value => _lobby = value, true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatDynamicRange, value => _dynamicRange = value, true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatShoutRange, value => _shoutRange = value, true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatWhisperRange, value => _whisperRange = value, true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatShoutThreshold, value => _shoutThreshold = value, true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatWhisperThreshold, value => _whisperThreshold = value, true);
        Subs.CVar(_cfg, VoiceCVars.VoiceChatBitrate, value => _format = VoiceFormats.FromBitrate(value), true);

        _voice.PushToTalkReceived += OnPushToTalk;
        _voice.MicMuteReceived += OnMicMute;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _voice.PushToTalkReceived -= OnPushToTalk;
        _voice.MicMuteReceived -= OnMicMute;
    }

    private void OnPushToTalk(NetUserId user, bool pressed, bool radio)
    {
        if (_player.TryGetSessionById(user, out var session))
            SetPushToTalk(session, pressed, radio);
    }

    private void OnMicMute(NetUserId user, bool muted)
    {
        var speaker = GetSpeaker(user);
        if (speaker.SelfMuted == muted)
            return;

        speaker.SelfMuted = muted;
        SendState(user, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _voice.SetProcessor(_features.Pcm);
        var now = _timing.RealTime;
        _routes.Clear();
        _sessions = null;
        _lobbyCollected = false;

        while (_voice.TryDequeueEvent(out var ev))
        {
            switch (ev)
            {
                case VoiceWebFrame frame:
                    HandleFrame(frame, now);
                    break;
                case VoiceWebConnectionChanged changed:
                    _voice.SendStatus(changed.User);
                    SendState(changed.User, true);
                    UpdateEffect(changed.User);
                    break;
                case VoiceWebEffectSelected selected:
                    GetSpeaker(selected.User).SelectedEffect = selected.Effect;
                    UpdateEffect(selected.User);
                    break;
            }
        }

        ProcessMixers(now);
        foreach (var feature in _features.All)
            feature.Update(now);

        if (now < _nextStateRefresh)
            return;

        _nextStateRefresh = now + StateRefreshInterval;
        foreach (var user in _speakers.Keys)
        {
            SendState(user, false);
            UpdateEffect(user);
        }
    }

    public bool ToggleAdminMute(NetUserId user)
    {
        var muted = _adminMuted.Add(user);
        if (!muted)
            _adminMuted.Remove(user);

        SendState(user, false);
        return muted;
    }

    private void HandleFrame(VoiceWebFrame frame, TimeSpan now)
    {
        if (!_player.TryGetSessionById(frame.User, out var session) ||
            session.Status != SessionStatus.InGame)
        {
            return;
        }

        if (_speakers.TryGetValue(frame.User, out var existing) && existing.SelfMuted)
            return;

        foreach (var feature in _features.All)
        {
            if (feature.TryConsumeFrame(session, frame, now))
                return;
        }


        if (IsInLobby(session))
        {
            HandleLobbyFrame(session, frame, now);
            return;
        }

        if (session.AttachedEntity is not { } uid)
            return;

        var speaker = GetSpeaker(frame.User);
        var levels = speaker.Measure(frame.Payload, out var db);

        if (_adminMuted.Contains(frame.User))
        {
            SendSelf(session, speaker, levels, VoiceSelfFlags.Blocked);
            Record(session, frame, VoiceLogFlags.Blocked, Name(uid), string.Empty);
            return;
        }

        var newTransmission = now - speaker.LastAttempt > TransmissionGap;
        speaker.LastAttempt = now;

        if (!CanTransmit(speaker, uid, now, newTransmission))
        {
            SendSelf(session, speaker, levels, VoiceSelfFlags.Blocked);
            Record(session, frame, VoiceLogFlags.Blocked, Name(uid), string.Empty);
            return;
        }

        if (newTransmission)
        {
            speaker.InfoSent.Clear();
            speaker.Loudness.Reset();
        }

        var loudness = _dynamicRange
            ? speaker.Loudness.Update(db, _shoutThreshold, _whisperThreshold)
            : VoiceLoudness.Normal;
        var directRange = GetDirectRange(loudness);
        var megaphone = false;
        foreach (var feature in _features.All)
            feature.AdjustRange(uid, ref directRange, ref megaphone);

        if (!_routes.TryGetValue(frame.User, out var route))
        {
            route = BuildRoute(session, uid, speaker, directRange);
            _routes[frame.User] = route;
            UpdateEffect(frame.User);
            SendSpeakerInfo(speaker, uid, route, session.Channel);

            foreach (var kept in route.KeepAwake)
            {
                foreach (var feature in _features.All)
                    feature.KeepAlive(kept);
            }
        }

        var selfFlags = VoiceSelfFlags.None;
        if (route.Radio.Count > 0)
            selfFlags |= VoiceSelfFlags.Radio;
        if (route.Broadcasting)
            selfFlags |= VoiceSelfFlags.Broadcast;
        if (megaphone)
            selfFlags |= VoiceSelfFlags.Megaphone;
        else if (loudness == VoiceLoudness.Shout)
            selfFlags |= VoiceSelfFlags.Shout;
        else if (loudness == VoiceLoudness.Whisper)
            selfFlags |= VoiceSelfFlags.Whisper;

        SendSelf(session, speaker, levels, selfFlags);
        Record(session, frame, ToLogFlags(selfFlags), Name(uid), route.RadioChannel ?? string.Empty);

        foreach (var transmission in route.Radio)
        {
            if (transmission.Targets.Count > 0)
                GetMixer(transmission.Channel).Push(speaker.Id, speaker.CopyPcm(), ToLevelByte(levels.Overall), transmission.Targets, now);
        }

        if (route.Groups.Count == 0 || speaker.Outgoing(frame.Payload, _format) is not { } payload)
            return;

        foreach (var group in route.Groups)
        {
            _net.ServerSendToMany(new MsgVoiceFrame
            {
                Source = group.Source,
                Speaker = speaker.Id,
                Sequence = frame.Sequence,
                Flags = frame.Flags,
                Route = group.Route,
                Format = _format,
                Global = group.Global,
                Range = group.Range,
                Payload = payload,
            }, group.Channels);
        }
    }

    private void ProcessMixers(TimeSpan now)
    {
        _idleMixers.Clear();
        foreach (var (channel, mixer) in _mixers)
        {
            mixer.Process(now, _format);
            if (mixer.Idle)
                _idleMixers.Add(channel);
        }

        foreach (var channel in _idleMixers)
        {
            _mixers.Remove(channel);
        }
    }

    private VoiceRadioMixer GetMixer(string channel)
    {
        if (!_mixers.TryGetValue(channel, out var mixer))
        {
            mixer = new VoiceRadioMixer(channel, _net, AllocateStreamId, () => _features.Pcm);
            _mixers[channel] = mixer;
        }

        return mixer;
    }

    private static byte ToLevelByte(float level)
    {
        return (byte) Math.Clamp(MathF.Round(level * 255f), 0f, 255f);
    }

    private bool IsInLobby(ICommonSession session)
    {
        if (session.AttachedEntity != null)
            return false;

        if (UserHasJoined(session, out var joined))
            return !joined;

        return true;
    }

    private static MethodInfo? _joined;
    private static Type? _tickerType;
    private static bool _joinedLookup;
    private object? _ticker;

    private bool UserHasJoined(ICommonSession session, out bool joined)
    {
        joined = false;
        if (!_joinedLookup)
        {
            _joinedLookup = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("Content.Server.GameTicking.GameTicker");
                var method = type?.GetMethod("UserHasJoinedGame", new[] { typeof(ICommonSession) });
                if (type == null || method == null)
                    continue;

                _tickerType = type;
                _joined = method;
                break;
            }
        }

        if (_joined == null || _tickerType == null)
            return false;

        _ticker ??= _systems.GetType()
            .GetMethods()
            .First(method => method.Name == "GetEntitySystem" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0)
            .MakeGenericMethod(_tickerType)
            .Invoke(_systems, null);

        joined = (bool) _joined.Invoke(_ticker, new object[] { session })!;
        return true;
    }

    private void HandleLobbyFrame(ICommonSession session, VoiceWebFrame frame, TimeSpan now)
    {
        var speaker = GetSpeaker(frame.User);
        var levels = speaker.Measure(frame.Payload, out _);

        if (!_lobby || _adminMuted.Contains(frame.User))
        {
            SendSelf(session, speaker, levels, VoiceSelfFlags.Blocked);
            Record(session, frame, VoiceLogFlags.Lobby | VoiceLogFlags.Blocked, string.Empty, string.Empty);
            return;
        }

        if (now - speaker.LastAttempt > TransmissionGap)
            speaker.InfoSent.Clear();

        speaker.LastAttempt = now;
        SendSelf(session, speaker, levels, VoiceSelfFlags.None);
        Record(session, frame, VoiceLogFlags.Lobby, string.Empty, string.Empty);

        CollectLobby();
        _lobbyChannels.Clear();
        foreach (var listener in _lobbySessions)
        {
            if (listener == session && !_voice.HearsSelf(listener.UserId) ||
                _voice.IsSpeakerMuted(listener.UserId, speaker.Id))
            {
                continue;
            }

            _lobbyChannels.Add(listener.Channel);
        }

        if (_lobbyChannels.Count == 0 || speaker.Outgoing(frame.Payload, _format) is not { } payload)
            return;

        UpdateSpeakerInfo(speaker, session.Name, string.Empty);
        foreach (var channel in _lobbyChannels)
        {
            SendSpeakerInfoTo(speaker, channel);
        }

        _net.ServerSendToMany(new MsgVoiceFrame
        {
            Source = NetEntity.Invalid,
            Speaker = speaker.Id,
            Sequence = frame.Sequence,
            Flags = frame.Flags,
            Route = VoiceRoute.Lobby,
            Format = _format,
            Global = true,
            Range = 0f,
            Payload = payload,
        }, _lobbyChannels);
    }

    private void CollectLobby()
    {
        if (_lobbyCollected)
            return;

        _lobbyCollected = true;
        _lobbySessions.Clear();

        _sessions ??= _player.Sessions;
        foreach (var session in _sessions)
        {
            if (session.Status == SessionStatus.InGame &&
                _voice.Receives(session.UserId) &&
                IsInLobby(session))
            {
                _lobbySessions.Add(session);
            }
        }
    }

    private void UpdateSpeakerInfo(Speaker speaker, string name, string channel)
    {
        if (name == speaker.InfoName && channel == speaker.InfoChannel)
            return;

        speaker.InfoName = name;
        speaker.InfoChannel = channel;
        speaker.InfoSent.Clear();
    }

    private void SendSpeakerInfoTo(Speaker speaker, INetChannel recipient)
    {
        if (speaker.InfoSent.Add(recipient))
            _net.ServerSendMessage(CreateSpeakerInfo(speaker), recipient);
    }

    private static VoiceLogFlags ToLogFlags(VoiceSelfFlags flags)
    {
        var result = VoiceLogFlags.None;
        if ((flags & VoiceSelfFlags.Radio) != 0)
            result |= VoiceLogFlags.Radio;
        if ((flags & VoiceSelfFlags.Broadcast) != 0)
            result |= VoiceLogFlags.Broadcast;
        if ((flags & VoiceSelfFlags.Megaphone) != 0)
            result |= VoiceLogFlags.Megaphone;
        if ((flags & VoiceSelfFlags.Shout) != 0)
            result |= VoiceLogFlags.Shout;
        if ((flags & VoiceSelfFlags.Whisper) != 0)
            result |= VoiceLogFlags.Whisper;
        return result;
    }

    private void SendSelf(ICommonSession session, Speaker speaker, VoiceLevels levels, VoiceSelfFlags flags)
    {
        _net.ServerSendMessage(new MsgVoiceSelf
        {
            Speaker = speaker.Id,
            Flags = flags,
            Levels = levels,
        }, session.Channel);
    }

    private void SendSpeakerInfo(Speaker speaker, EntityUid uid, Route route, INetChannel self)
    {
        UpdateSpeakerInfo(speaker, GetSpeakerName(uid), route.RadioChannel ?? string.Empty);

        foreach (var group in route.Groups)
        {
            foreach (var recipient in group.Channels)
            {
                SendSpeakerInfoTo(speaker, recipient);
            }
        }

        foreach (var transmission in route.Radio)
        {
            foreach (var recipient in transmission.Targets.Keys)
            {
                SendSpeakerInfoTo(speaker, recipient);
            }
        }

        SendSpeakerInfoTo(speaker, self);
    }

    private static MsgVoiceSpeakerInfo CreateSpeakerInfo(Speaker speaker)
    {
        return new MsgVoiceSpeakerInfo
        {
            Speaker = speaker.Id,
            Name = speaker.InfoName,
            Channel = speaker.InfoChannel,
        };
    }

    private string GetSpeakerName(EntityUid uid)
    {
        var nameEv = new TransformSpeakerNameEvent(uid, Name(uid));
        RaiseLocalEvent(uid, nameEv);

        var name = nameEv.VoiceName;
        foreach (var feature in _features.All)
            feature.AdjustName(uid, ref name);

        return name;
    }

    private float GetDirectRange(VoiceLoudness loudness)
    {
        return loudness switch
        {
            VoiceLoudness.Shout => MathF.Max(_shoutRange, _range),
            VoiceLoudness.Whisper => MathF.Min(_whisperRange, _range),
            _ => _range,
        };
    }

    private Route BuildRoute(ICommonSession speakerSession, EntityUid speakerUid, Speaker speaker, float directRange)
    {
        var ghostSpeaker = VoiceContentTypes.IsGhost(EntityManager, speakerUid);
        var contribution = new VoiceContribution
        {
            SpeakerUid = speakerUid,
            Speaker = speakerSession,
            Ghost = ghostSpeaker,
            RadioPtt = speaker.PushToTalkRadio,
            LocalPtt = speaker.PushToTalk,
            DirectRange = directRange,
        };

        if (!ghostSpeaker)
        {
            foreach (var feature in _features.All)
                feature.Contribute(contribution);
        }

        var route = new Route();
        route.KeepAwake.AddRange(contribution.KeepAwake);
        route.Broadcasting = contribution.Broadcasting;

        foreach (var radio in contribution.Radios)
        {
            var transmission = new RadioTransmission(radio.ChannelId);
            foreach (var recipient in radio.Recipients)
                transmission.Recipients.Add(recipient);

            transmission.Relays.AddRange(radio.Speakers);
            route.Radio.Add(transmission);
        }

        route.RadioChannel = route.Radio.Count > 0 ? route.Radio[0].Channel : null;

        EntityUid? direct = null;
        var directRoute = VoiceRoute.Direct;
        var directOrigin = MapCoordinates.Nullspace;
        if (!contribution.SuppressDirect)
        {
            direct = contribution.DirectEmitter ?? speakerUid;
            directRoute = contribution.DirectEmitter != null ? contribution.DirectRoute : VoiceRoute.Direct;
            directOrigin = _transform.GetMapCoordinates(direct.Value);
            if (directOrigin.MapId == MapId.Nullspace)
                direct = null;
        }

        _sessions ??= _player.Sessions;
        foreach (var session in _sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } listener ||
                !_voice.Receives(session.UserId))
            {
                continue;
            }

            if (session == speakerSession && !_voice.HearsSelf(session.UserId))
                continue;

            if (ghostSpeaker && !VoiceContentTypes.IsGhost(EntityManager, listener) ||
                _voice.IsSpeakerMuted(session.UserId, speaker.Id))
            {
                continue;
            }

            var primary = GetListenerCoordinates(listener);
            MapCoordinates? secondary = null;
            foreach (var feature in _features.All)
            {
                if (feature.TrySecondaryListener(listener, out var extra))
                {
                    secondary = extra;
                    break;
                }
            }

            if (direct is { } emitter && CanHear(directOrigin, directRange, primary, secondary, out var global))
            {
                route.Add(GetNetEntity(emitter), directRoute, global, directRange, session.Channel);
            }
            else if (TryHearRelay(contribution.Relays, primary, secondary, out var relay, out global))
            {
                route.Add(GetNetEntity(relay.Emitter), relay.Route, global, relay.Range, session.Channel);
            }
            else if (contribution.BroadcastRecipients.Contains(session))
            {
                route.Add(contribution.BroadcastSource, VoiceRoute.Broadcast, true, 0f, session.Channel);
            }
            else
            {
                AssignRadio(session, primary, secondary, route);
            }
        }

        return route;
    }

    private void AssignRadio(ICommonSession session, MapCoordinates primary, MapCoordinates? secondary, Route route)
    {
        foreach (var transmission in route.Radio)
        {
            if (_voice.IsRadioChannelMuted(session.UserId, transmission.Channel))
                continue;

            if (transmission.Recipients.Contains(session))
            {
                transmission.Targets[session.Channel] = new VoiceMixTarget(NetEntity.Invalid, VoiceRoute.Radio, true, 0f);
                return;
            }

            if (TryHearRelay(transmission.Relays, primary, secondary, out var relay, out var global))
            {
                transmission.Targets[session.Channel] = new VoiceMixTarget(GetNetEntity(relay.Emitter), VoiceRoute.RadioSpeaker, global, relay.Range);
                return;
            }
        }
    }

    private static bool TryHearRelay(List<VoiceRelay> relays, MapCoordinates primary, MapCoordinates? secondary, out VoiceRelay relay, out bool global)
    {
        relay = default;
        global = false;

        var bestDistance = float.MaxValue;
        foreach (var candidate in relays)
        {
            if (!CanHear(candidate.Origin, candidate.Range, primary, secondary, out var candidateGlobal))
                continue;

            var position = candidateGlobal && secondary is { } fallback ? fallback : primary;
            var distance = Vector2.DistanceSquared(candidate.Origin.Position, position.Position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            relay = candidate;
            global = candidateGlobal;
        }

        return bestDistance < float.MaxValue;
    }

    private static bool CanHear(MapCoordinates origin, float range, MapCoordinates primary, MapCoordinates? secondary, out bool global)
    {
        var maxDistance = range + ListenerMargin;
        var maxDistanceSquared = maxDistance * maxDistance;

        global = false;
        if (primary.MapId == origin.MapId && Vector2.DistanceSquared(primary.Position, origin.Position) <= maxDistanceSquared)
            return true;

        global = true;
        return secondary is { } other &&
               other.MapId == origin.MapId &&
               Vector2.DistanceSquared(other.Position, origin.Position) <= maxDistanceSquared;
    }

    private void UpdateEffect(NetUserId user)
    {
        var speaker = GetSpeaker(user);
        var settings = default(VoiceEffectSettings);

        if (_player.TryGetSessionById(user, out var session) && session.AttachedEntity is { } uid && !VoiceContentTypes.IsGhost(EntityManager, uid))
        {
            var effect = VoiceEffect.None;
            if (speaker.SelectedEffect != VoiceEffect.None && HasVoiceChanger(uid))
                effect = speaker.SelectedEffect;
            else if (TryComp<VoiceChatEffectComponent>(uid, out var builtIn))
                effect = builtIn.Effect;

            var range = 0f;
            var megaphone = false;
            foreach (var feature in _features.All)
                feature.AdjustRange(uid, ref range, ref megaphone);

            settings = new VoiceEffectSettings(effect, GetMuffle(uid), megaphone);
        }

        if (settings == speaker.AppliedEffect)
            return;

        speaker.AppliedEffect = settings;
        _voice.SetEffect(user, settings);
    }

    private VoiceMuffle GetMuffle(EntityUid uid)
    {
        if (_container.TryGetContainingContainer((uid, null, null), out var container) &&
            !_hands.IsHolding(container.Owner, uid) &&
            !ContainerAllowsVoice(uid, container.Owner))
        {
            return VoiceMuffle.Heavy;
        }

        if (_inventory.TryGetSlotEntity(uid, "mask", out var mask) && CoversMouth(mask.Value))
            return VoiceMuffle.Light;

        if (_inventory.TryGetSlotEntity(uid, "head", out var head) && CoversMouth(head.Value))
            return VoiceMuffle.Light;

        var muffle = VoiceMuffle.None;
        foreach (var feature in _features.All)
            feature.AdjustMuffle(uid, ref muffle);

        return muffle;
    }

    private bool CoversMouth(EntityUid item)
    {
        return TryComp<IngestionBlockerComponent>(item, out var blocker) &&
               blocker.Enabled &&
               !(TryComp<MaskComponent>(item, out var mask) && mask.IsToggled);
    }

    private bool IsMuzzled(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "mask", out var mask) &&
               _tag.HasTag(mask.Value, MuzzleTag) &&
               !(TryComp<MaskComponent>(mask, out var toggle) && toggle.IsToggled);
    }

    private bool HasVoiceChanger(EntityUid uid)
    {
        foreach (var feature in _features.All)
        {
            if (feature.HasVoiceChanger(uid))
                return true;
        }

        return false;
    }

    private bool ContainerAllowsVoice(EntityUid speaker, EntityUid container)
    {
        foreach (var feature in _features.All)
        {
            if (feature.AllowsContainer(speaker, container))
                return true;
        }

        return false;
    }

    private bool CanTransmit(Speaker speaker, EntityUid uid, TimeSpan now, bool recheck)
    {
        if (!recheck && speaker.CheckedEntity == uid && now - speaker.CheckedAt < PermissionRecheck)
            return speaker.Allowed;

        var allowed = CanSpeak(uid);
        var changed = allowed != speaker.Allowed || speaker.CheckedEntity != uid;

        speaker.CheckedEntity = uid;
        speaker.CheckedAt = now;
        speaker.Allowed = allowed;

        if (changed)
            SendState(speaker.User, false);

        return allowed;
    }

    private bool CanSpeak(EntityUid uid)
    {
        if (VoiceContentTypes.IsGhost(EntityManager, uid))
            return true;

        if (!CanSpeakPassive(uid))
            return false;

        return _actionBlocker.CanSpeak(uid);
    }

    private bool CanSpeakPassive(EntityUid uid)
    {
        if (VoiceContentTypes.IsGhost(EntityManager, uid))
            return true;

        if (_mobState.IsIncapacitated(uid) || IsMuzzled(uid))
            return false;

        return _speechQuery.TryComp(uid, out var speech) && speech.Enabled;
    }

    private MapCoordinates GetListenerCoordinates(EntityUid listener)
    {
        if (_eyeQuery.TryComp(listener, out var eye) && eye.Target is { } target && Exists(target))
            return _transform.GetMapCoordinates(target);

        return _transform.GetMapCoordinates(listener);
    }

    private void SetPushToTalk(ICommonSession? session, bool active, bool radio)
    {
        if (session == null)
            return;

        var speaker = GetSpeaker(session.UserId);
        if (radio)
        {
            if (speaker.PushToTalkRadio == active)
                return;

            speaker.PushToTalkRadio = active;
        }
        else
        {
            if (speaker.PushToTalk == active)
                return;

            speaker.PushToTalk = active;
        }

        SendState(session.UserId, false);
    }

    private void SendState(NetUserId user, bool force)
    {
        if (!_voice.IsWebConnected(user))
            return;

        var speaker = GetSpeaker(user);
        var state = BuildState(user, speaker) with
        {
            PushToTalk = speaker.PushToTalk || speaker.PushToTalkRadio,
            SelfMuted = speaker.SelfMuted,
        };
        if (!force && speaker.LastState == state)
            return;

        speaker.LastState = state;
        _voice.SendState(user, state);
    }

    private VoiceWebState BuildState(NetUserId user, Speaker speaker)
    {
        if (!_player.TryGetSessionById(user, out var session))
            return new VoiceWebState(string.Empty, false, false, _adminMuted.Contains(user), false, false, false, null);

        if (session.Status == SessionStatus.InGame && IsInLobby(session))
            return new VoiceWebState(session.Name, false, _lobby, _adminMuted.Contains(user), speaker.PushToTalk, false, false, null, true);

        if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } uid)
            return new VoiceWebState(session.Name, false, false, _adminMuted.Contains(user), false, false, false, null);

        var refused = speaker.CheckedEntity == uid &&
                      !speaker.Allowed &&
                      _timing.RealTime - speaker.LastAttempt < RefusalDisplay;

        var hints = new VoiceStateHints();
        foreach (var feature in _features.All)
            feature.DescribeState(uid, hints);

        return new VoiceWebState(
            session.Name,
            true,
            !refused && CanSpeakPassive(uid),
            _adminMuted.Contains(user),
            speaker.PushToTalk,
            hints.Broadcasting,
            hints.VoiceChanger || HasVoiceChanger(uid),
            hints.Radio);
    }

    private Speaker GetSpeaker(NetUserId user)
    {
        if (_speakers.TryGetValue(user, out var speaker))
            return speaker;

        speaker = new Speaker(user, AllocateStreamId());
        _speakerIds.Add(speaker.Id);
        _speakers[user] = speaker;
        return speaker;
    }

    private ushort AllocateStreamId()
    {
        for (var attempt = 0; attempt < ushort.MaxValue; attempt++)
        {
            var id = _nextSpeakerId;
            _nextSpeakerId = _nextSpeakerId == ushort.MaxValue ? (ushort) 1 : (ushort) (_nextSpeakerId + 1);
            if (!_speakerIds.Contains(id))
                return id;
        }

        return _nextSpeakerId;
    }

    private sealed partial class Route
    {
        public readonly List<RouteGroup> Groups = new();
        public readonly List<EntityUid> KeepAwake = new();
        public readonly List<RadioTransmission> Radio = new();
        public string? RadioChannel;
        public bool Broadcasting;

        public void Add(NetEntity source, VoiceRoute voiceRoute, bool global, float range, INetChannel channel)
        {
            foreach (var group in Groups)
            {
                if (group.Source == source && group.Route == voiceRoute && group.Global == global && group.Range.Equals(range))
                {
                    group.Channels.Add(channel);
                    return;
                }
            }

            var created = new RouteGroup(source, voiceRoute, global, range);
            created.Channels.Add(channel);
            Groups.Add(created);
        }
    }

    private sealed partial class RadioTransmission(string channel)
    {
        public readonly string Channel = channel;
        public readonly HashSet<ICommonSession> Recipients = new();
        public readonly List<VoiceRelay> Relays = new();
        public readonly Dictionary<INetChannel, VoiceMixTarget> Targets = new();
    }

    private sealed partial class RouteGroup(NetEntity source, VoiceRoute route, bool global, float range)
    {
        public readonly NetEntity Source = source;
        public readonly VoiceRoute Route = route;
        public readonly bool Global = global;
        public readonly float Range = range;
        public readonly List<INetChannel> Channels = new();
    }


    public ushort RentStreamId()
    {
        return AllocateStreamId();
    }

    public VoiceFormat OutboundFormat => _format;

    public bool TryMeasure(NetUserId user, byte[] payload, out VoiceLevels levels, out short[]? pcm)
    {
        var speaker = GetSpeaker(user);
        levels = speaker.Measure(payload, out _);
        pcm = speaker.Decoded ? speaker.CopyPcm() : null;
        return speaker.Decoded;
    }

    public void PushSelf(ICommonSession session, VoiceLevels levels, VoiceSelfFlags flags)
    {
        SendSelf(session, GetSpeaker(session.UserId), levels, flags);
    }

    private void Record(ICommonSession session, VoiceWebFrame frame, VoiceLogFlags flags, string name, string channel)
    {
        _features.Recorder?.Record(session, frame, flags, name, channel);
    }

    private sealed partial class Speaker(NetUserId user, ushort id)
    {
        public readonly NetUserId User = user;
        public readonly ushort Id = id;
        public bool PushToTalk;
        public bool PushToTalkRadio;
        public bool SelfMuted;
        public bool Allowed;
        public EntityUid? CheckedEntity;
        public TimeSpan CheckedAt;
        public TimeSpan LastAttempt;
        public VoiceWebState? LastState;
        public VoiceEffect SelectedEffect;
        public VoiceEffectSettings AppliedEffect;
        public string InfoName = string.Empty;
        public string InfoChannel = string.Empty;
        public readonly HashSet<INetChannel> InfoSent = new();
        public readonly VoiceLoudnessTracker Loudness = new();

        private readonly VoiceLevelAnalyzer _levelAnalyzer = new();
        private readonly short[] _levelPcm = new short[VoiceCodec.FrameSamples];
        private readonly VoiceEncoder _outgoing = new();
        private bool _decoded;

        public bool Decoded => _decoded;

        public VoiceLevels Measure(byte[] payload, out float db)
        {
            _decoded = VoiceCodec.Decode(payload, _levelPcm);
            if (!_decoded)
            {
                db = float.NegativeInfinity;
                return default;
            }

            var levels = _levelAnalyzer.Analyze(_levelPcm);
            db = _levelAnalyzer.LastDb;
            return levels;
        }

        public short[] CopyPcm()
        {
            return (short[]) _levelPcm.Clone();
        }

        public byte[]? Outgoing(byte[] payload, VoiceFormat format)
        {
            if (!_decoded)
                return null;

            return format == VoiceFormat.Wide4 ? payload : _outgoing.Encode(_levelPcm, format);
        }
    }
}
