// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.LaborCamp.Components;

/// <summary>
/// Shows the prisoner's current and remaining points. Server-only display state is not networked.
/// </summary>
[RegisterComponent]
public sealed partial class LaborCampConsoleComponent : Component
{
    public EntityUid? DisplayedId;
}
