// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Content.VoiceChat.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.VoiceChat.Client.UI;

public static class VoiceRadioClient
{
    public static void Select(IEntityNetworkManager net, IConfigurationManager cfg, ProtoId<RadioChannelPrototype>? channel)
    {
        cfg.SetCVar(VoiceCVars.VoiceRadioChannel, channel?.Id ?? "");
        cfg.SaveToFile();
        net.SendSystemNetworkMessage(new VoiceRadioSelectEvent(channel));
    }

    public static void ApplySaved(IEntityNetworkManager net, IConfigurationManager cfg, VoiceRadioComponent radio)
    {
        var saved = cfg.GetCVar(VoiceCVars.VoiceRadioChannel);
        if (string.IsNullOrWhiteSpace(saved))
            return;

        var channel = new ProtoId<RadioChannelPrototype>(saved);
        if (!radio.Channels.Contains(channel) || radio.Active == channel)
            return;

        net.SendSystemNetworkMessage(new VoiceRadioSelectEvent(channel));
    }

    public static ProtoId<RadioChannelPrototype>? Cycle(VoiceRadioComponent radio)
    {
        if (radio.Channels.Count == 0)
            return radio.Active;

        var index = radio.Active is { } active ? radio.Channels.IndexOf(active) : -1;
        return radio.Channels[(index + 1) % radio.Channels.Count];
    }
}
