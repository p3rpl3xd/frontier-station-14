// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Shuttles.Components;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.LaborCamp;
using Content.Shared.Shuttles.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server.LaborCamp;

/// <summary>
/// Prisoners may pilot the separate shuttle. They may not open a helm on the camp grid.
/// The camp grid itself is also static and marked so shuttle thrust does not move it.
/// </summary>
public sealed class LaborCampPilotSystem : EntitySystem
{
    private static readonly ProtoId<AccessLevelPrototype> SecurityAccess = LaborCampPrototypes.SecurityAccess;

    [Dependency] private AccessReaderSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleConsoleComponent, ActivatableUIOpenAttemptEvent>(OnConsoleAttempt);
    }

    private void OnConsoleAttempt(EntityUid uid, ShuttleConsoleComponent _, ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var grid = Transform(uid).GridUid;
        if (grid is not { } gridUid || !HasComp<LaborCampSiteComponent>(gridUid))
            return;

        if (_access.FindAccessTags(args.User).Contains(SecurityAccess))
            return;

        args.Cancel();
    }
}
