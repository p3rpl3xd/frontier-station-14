// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server.LaborCamp;

/// <summary>
/// Marks the prisoner shuttle. While <see cref="Locked"/> is set it can FTL only to the camp or the main station.
/// </summary>
[RegisterComponent]
public sealed partial class LaborCampShuttleComponent : Component
{
    public EntityUid Camp;

    public bool Locked = true;

    /// <summary>World position of the last camp or station stop. Drift past a few meters is pulled back here.</summary>
    public Vector2 Anchor;
}
