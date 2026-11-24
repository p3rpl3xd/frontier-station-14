// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Server.LaborCamp;

/// <summary>
/// Tiles on the camp grid that held a mineable rock when the camp spawned.
/// Regrowth puts a fresh rock back on empty tiles from this set.
/// </summary>
[RegisterComponent]
public sealed partial class LaborCampMineComponent : Component
{
    public HashSet<Vector2i> Tiles = new();
}
