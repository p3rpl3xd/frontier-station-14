// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.VoiceChat.Shared;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VoiceBroadcastConsoleComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(90);

    [AutoNetworkedField]
    public EntityUid? Broadcaster;

    [AutoNetworkedField]
    public TimeSpan EndTime;

    [AutoNetworkedField]
    public TimeSpan NextBroadcast;
}

[Serializable, NetSerializable]
public sealed class VoiceBroadcastToggleMessage : BoundUserInterfaceMessage;
