// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.VoiceChat.Shared;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class VoiceRadioComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [AutoNetworkedField]
    public List<ProtoId<RadioChannelPrototype>> Channels = new();

    [AutoNetworkedField]
    public ProtoId<RadioChannelPrototype>? Active;

    [AutoNetworkedField]
    public List<ProtoId<RadioChannelPrototype>> Receive = new();
}

[Serializable, NetSerializable]
public sealed class VoiceRadioSelectEvent(ProtoId<RadioChannelPrototype>? channel) : EntityEventArgs
{
    public readonly ProtoId<RadioChannelPrototype>? Channel = channel;
}
