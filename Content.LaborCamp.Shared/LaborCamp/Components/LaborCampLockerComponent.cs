// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.LaborCamp.Components;

/// <summary>
/// Marker on a genpop locker that also takes a point goal.
/// The official locker still creates the ID and the timer.
/// </summary>
[RegisterComponent]
public sealed partial class LaborCampLockerComponent : Component
{
    /// <summary>Runtime only. The point goal from the labor UI, applied after genpop creates the ID.</summary>
    public int PendingPoints;

    public bool HasPending;

    public EntityUid? PendingPreviousId;

    public EntityUid PendingActor;

    /// <summary>How long the goal has been waiting for genpop to spawn the ID.</summary>
    public float PendingSeconds;
}
