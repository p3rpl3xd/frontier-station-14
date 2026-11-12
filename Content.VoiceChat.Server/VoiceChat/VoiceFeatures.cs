// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.VoiceChat.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.VoiceChat.Server;

public interface IVoicePcmProcessor
{
    void Process(short[] buffer, int offset, int count, VoiceEffectSettings settings);
}

public interface IVoiceRecorder
{
    void Record(ICommonSession session, VoiceWebFrame frame, VoiceLogFlags flags, string name, string channel);
}

public interface IVoiceFeature
{
    void Contribute(VoiceContribution contribution)
    {
    }

    bool TryConsumeFrame(ICommonSession session, VoiceWebFrame frame, TimeSpan now)
    {
        return false;
    }

    void Update(TimeSpan now)
    {
    }

    void AdjustRange(EntityUid speaker, ref float range, ref bool megaphone)
    {
    }

    void AdjustMuffle(EntityUid speaker, ref VoiceMuffle muffle)
    {
    }

    bool AllowsContainer(EntityUid speaker, EntityUid container)
    {
        return false;
    }

    bool HasVoiceChanger(EntityUid speaker)
    {
        return false;
    }

    void AdjustName(EntityUid speaker, ref string name)
    {
    }

    bool TrySecondaryListener(EntityUid listener, out MapCoordinates secondary)
    {
        secondary = default;
        return false;
    }

    void DescribeState(EntityUid speaker, VoiceStateHints hints)
    {
    }

    void KeepAlive(EntityUid entity)
    {
    }
}

public sealed class VoiceStateHints
{
    public bool Broadcasting;
    public bool VoiceChanger;
    public string? Radio;
}

public sealed class VoiceFeatures
{
    public List<IVoiceFeature> All { get; } = new();

    public IVoicePcmProcessor? Pcm { get; set; }

    public IVoiceRecorder? Recorder { get; set; }

    public void Add(IVoiceFeature feature)
    {
        if (!All.Contains(feature))
            All.Add(feature);
    }
}

public readonly record struct VoiceRelay(EntityUid Emitter, MapCoordinates Origin, float Range, VoiceRoute Route);

public sealed class VoiceRadioListen
{
    public string ChannelId = string.Empty;
    public string LocalizedName = string.Empty;
    public readonly HashSet<ICommonSession> Recipients = new();
    public readonly List<VoiceRelay> Speakers = new();
}

public sealed class VoiceContribution
{
    public required EntityUid SpeakerUid;
    public required ICommonSession Speaker;
    public required bool Ghost;
    public required bool RadioPtt;
    public required bool LocalPtt;
    public required float DirectRange;
    public bool SuppressDirect;
    public EntityUid? DirectEmitter;
    public VoiceRoute DirectRoute = VoiceRoute.Direct;
    public bool Broadcasting;
    public NetEntity BroadcastSource = NetEntity.Invalid;
    public readonly HashSet<ICommonSession> BroadcastRecipients = new();
    public readonly List<VoiceRelay> Relays = new();
    public readonly List<VoiceRadioListen> Radios = new();
    public readonly List<EntityUid> KeepAwake = new();
}
