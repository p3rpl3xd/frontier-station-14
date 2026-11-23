// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared.LaborCamp.Components;

/// <summary>
/// Point goal stored on a genpop prisoner ID. A goal of zero means there is no point goal.
/// The timer still lives on <see cref="Content.Shared.Security.Components.GenpopIdCardComponent"/>
/// and <see cref="Content.Shared.Access.Components.ExpireIdCardComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LaborCampSentenceComponent : Component
{
    /// <summary>
    /// Points required to finish the stay. Zero means points do not end it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PointGoal;

    [DataField, AutoNetworkedField]
    public int Points;

    /// <summary>
    /// Set once security has been told this stay is over, so the timer and the point goal do not both announce it.
    /// </summary>
    [DataField]
    public bool SecurityNotified;
}
