// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.LaborCamp;
using Content.Shared.Shuttles.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server.LaborCamp;

/// <summary>
/// Prisoners may open a helm on the prisoner shuttle. They may not open one on the camp grid.
/// The camp grid itself is also static and marked so shuttle thrust does not move it.
/// </summary>
public sealed class LaborCampPilotSystem : EntitySystem
{
    private static readonly ProtoId<AccessLevelPrototype> SecurityAccess = LaborCampPrototypes.SecurityAccess;
    private static readonly ProtoId<AccessLevelPrototype> ShuttleAccess = LaborCampPrototypes.ShuttleAccess;

    [Dependency] private AccessReaderSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentStartup>(OnConsoleStartup);
        // After the host console system, which on HardLight cancels anyone without a pilot tag.
        SubscribeLocalEvent<LaborCampHelmGuardComponent, ActivatableUIOpenAttemptEvent>(
            OnConsoleAttempt,
            after: new[] { typeof(ShuttleConsoleSystem) });
    }

    private void OnConsoleStartup(EntityUid uid, ShuttleConsoleComponent _, ComponentStartup args)
    {
        EnsureComp<LaborCampHelmGuardComponent>(uid);
    }

    private void OnConsoleAttempt(EntityUid uid, LaborCampHelmGuardComponent _, ActivatableUIOpenAttemptEvent args)
    {
        var grid = Transform(uid).GridUid;
        if (grid is not { } gridUid)
            return;

        if (HasComp<LaborCampSiteComponent>(gridUid))
        {
            if (!_access.FindAccessTags(args.User).Contains(SecurityAccess))
                args.Cancel();
            return;
        }

        if (!HasComp<LaborCampShuttleComponent>(gridUid))
            return;

        var tags = _access.FindAccessTags(args.User);
        if (tags.Contains(ShuttleAccess) || tags.Contains(SecurityAccess))
            args.Uncancel();
    }
}
