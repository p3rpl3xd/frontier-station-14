// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.VoiceChat.Shared;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VoiceChatCanadianComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MouthHeight = 8f;

    [DataField, AutoNetworkedField]
    public float HingeOffset = 4f;

    [DataField, AutoNetworkedField]
    public float MaxAngle = 35f;

    [DataField, AutoNetworkedField]
    public float MaxLift = 4f;

    [DataField, AutoNetworkedField]
    public float Sensitivity = 1.4f;

    [DataField, AutoNetworkedField]
    public float HingeLift = 1f;

    [DataField, AutoNetworkedField]
    public float PivotSmoothing = 0.15f;

    [DataField, AutoNetworkedField]
    public float MinPivotTime = 0.35f;

    [DataField, AutoNetworkedField]
    public float MaxPivotTime = 0.9f;
}
