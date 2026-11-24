// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.LaborCamp;

/// <summary>
/// Marks the camp grid. It is fixed in place. Helm consoles on it are security-only.
/// The prisoner shuttle is a different grid and does not have this component.
/// </summary>
[RegisterComponent]
public sealed partial class LaborCampSiteComponent : Component
{
}
