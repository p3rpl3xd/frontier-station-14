// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.LaborCamp;

/// <summary>
/// Marker so the camp-helm check does not subscribe on <c>ShuttleConsoleComponent</c>.
/// HardLight's console system already listens to the open attempt on that component.
/// </summary>
[RegisterComponent]
public sealed partial class LaborCampHelmGuardComponent : Component;
