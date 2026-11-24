// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Reflection;
using Content.Server.Construction;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.LaborCamp;
using Content.Shared.LaborCamp.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Station.Components;
using Content.Shared.Tiles;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.LaborCamp;

/// <summary>
/// The prisoner shuttle's helm lists two docks: the labor camp and the round's largest station.
/// While <see cref="LaborCampCVars.ShuttleLockedRoute"/> is on, map FTL, beacons, and thrusters cannot take it anywhere else.
/// A served sentence and security both use that helm. Set the cvar false and it flies like a normal shuttle.
/// </summary>
public sealed class LaborCampShuttleSystem : EntitySystem
{
    private const float SnapDistance = 2f;

    private static readonly ProtoId<AccessLevelPrototype> Security = LaborCampPrototypes.SecurityAccess;
    private static readonly ProtoId<AccessLevelPrototype> ShuttleAccess = LaborCampPrototypes.ShuttleAccess;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private bool _locked = true;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(ThrusterSystem));
        UpdatesAfter.Add(typeof(ShuttleSystem));

        Subs.CVar(_cfg, LaborCampCVars.ShuttleLockedRoute, OnCvarChanged, true);

        Subs.BuiEvents<LaborCampShuttleConsoleComponent>(LaborCampShuttleUiKey.Key, subs =>
        {
            subs.Event<LaborCampShuttleTravelMessage>(OnTravel);
            subs.Event<BoundUIOpenedEvent>(OnShuttleUiOpened);
        });
        Subs.BuiEvents<LaborCampShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<ShuttleConsoleFTLPositionMessage>(OnMapFtl);
            subs.Event<ShuttleConsoleFTLBeaconMessage>(OnBeaconFtl);
        });

        SubscribeLocalEvent<ShuttleConsoleComponent, EntParentChangedMessage>(OnConsoleMoved);
        SubscribeLocalEvent<LaborCampCockpitDoorComponent, EntParentChangedMessage>(OnCockpitMoved);
        SubscribeLocalEvent<ThrusterComponent, EntParentChangedMessage>(OnThrusterMoved);
        SubscribeLocalEvent<LaborCampShuttleComponent, ConsoleFTLAttemptEvent>(OnConsoleFtl);
        SubscribeLocalEvent<LaborCampShuttleComponent, FTLRequestEvent>(OnFtlRequest);
        SubscribeLocalEvent<LaborCampShuttleComponent, FTLCompletedEvent>(OnFtlCompleted);
        SubscribeLocalEvent<AnchorableComponent, UnanchorAttemptEvent>(OnUnanchor);
        SubscribeLocalEvent<LaborCampShuttleComponent, FloorTileAttemptEvent>(OnFloor);
        SubscribeLocalEvent<LaborCampShuttlePartComponent, GetVerbsEvent<Verb>>(OnVerbs, after: new[] { typeof(ConstructionSystem) });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LaborCampShuttleComponent, ShuttleComponent>();
        while (query.MoveNext(out var uid, out var route, out var shuttle))
            Hold(uid, route, shuttle);
    }

    /// <summary>
    /// Called once the shuttle grid exists and has been docked, or parked if docking failed.
    /// </summary>
    public void Attach(EntityUid shuttle, EntityUid camp)
    {
        var route = EnsureComp<LaborCampShuttleComponent>(shuttle);
        route.Camp = camp;
        route.Locked = _locked;
        route.Anchor = _xform.GetWorldPosition(shuttle);
        ProtectGrid(shuttle);
        Seal(shuttle);
        ApplyAccess(shuttle);
        if (TryComp<ShuttleComponent>(shuttle, out var shuttleComp))
            Hold(shuttle, route, shuttleComp);
    }

    /// <summary>
    /// Destinations the locked helm is allowed to show. There is no third entry.
    /// </summary>
    public List<LaborCampShuttleDestination> OfferedDestinations(EntityUid shuttle)
    {
        var offered = new List<LaborCampShuttleDestination>();
        if (!TryComp<LaborCampShuttleComponent>(shuttle, out var route))
            return offered;

        if (route.Camp.IsValid() && !Terminating(route.Camp))
            offered.Add(LaborCampShuttleDestination.Camp);
        if (FindMainStation(shuttle) is { } station && station.IsValid())
            offered.Add(LaborCampShuttleDestination.Station);
        return offered;
    }

    /// <summary>
    /// Starts an FTL dock to the camp or the main station. Map coordinates are not accepted.
    /// </summary>
    public bool TryTravel(EntityUid console, LaborCampShuttleDestination destination)
    {
        if (Transform(console).GridUid is not { } grid)
            return false;
        if (!TryComp<LaborCampShuttleComponent>(grid, out var route) || !TryComp<ShuttleComponent>(grid, out var shuttle))
            return false;
        if (!shuttle.Enabled && !route.Locked)
            return false;
        if (InTransit(grid))
            return false;
        if (!TryResolveDestination(grid, route, destination, out var target))
            return false;
        if (DockedTo(grid, target))
            return false;

        // FTLToDock does not consult the console map. Enabled is forced off while locked, so turn it on for the hop.
        var wasEnabled = shuttle.Enabled;
        shuttle.Enabled = true;
        _shuttles.FTLToDock(grid, shuttle, target);
        if (!InTransit(grid))
            shuttle.Enabled = wasEnabled;
        return InTransit(grid);
    }

    private void OnCvarChanged(bool value)
    {
        _locked = value;
        var query = EntityQueryEnumerator<LaborCampShuttleComponent, ShuttleComponent>();
        while (query.MoveNext(out var uid, out var route, out var shuttle))
        {
            route.Locked = value;
            if (!value)
                shuttle.Enabled = true;
            else
                Hold(uid, route, shuttle);
            RefreshConsoles(uid);
        }
    }

    private void OnTravel(Entity<LaborCampShuttleConsoleComponent> ent, ref LaborCampShuttleTravelMessage args)
    {
        if (Transform(ent).GridUid is not { } grid || !HasComp<LaborCampShuttleComponent>(grid))
            return;

        TryTravel(ent, args.Destination);
        PushState(ent);
    }

    private void OnMapFtl(Entity<LaborCampShuttleConsoleComponent> ent, ref ShuttleConsoleFTLPositionMessage args)
    {
        RedirectStockHelm(ent);
    }

    private void OnBeaconFtl(Entity<LaborCampShuttleConsoleComponent> ent, ref ShuttleConsoleFTLBeaconMessage args)
    {
        RedirectStockHelm(ent);
    }

    /// <summary>
    /// A normal shuttle console built onto this grid still cannot pick a free coordinate or beacon.
    /// The click is turned into a trip to whichever endpoint the shuttle is not already at.
    /// </summary>
    private void RedirectStockHelm(EntityUid console)
    {
        if (Transform(console).GridUid is not { } grid || !TryComp<LaborCampShuttleComponent>(grid, out var route) || !route.Locked)
            return;
        if (InTransit(grid))
            return;

        var destination = DockedTo(grid, route.Camp)
            ? LaborCampShuttleDestination.Station
            : LaborCampShuttleDestination.Camp;
        TryTravel(console, destination);
    }

    private void OnShuttleUiOpened(EntityUid uid, LaborCampShuttleConsoleComponent component, BoundUIOpenedEvent args)
    {
        PushState(uid);
    }

    private void OnConsoleFtl(Entity<LaborCampShuttleComponent> ent, ref ConsoleFTLAttemptEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("labor-camp-shuttle-locked");
    }

    private void OnFtlRequest(Entity<LaborCampShuttleComponent> ent, ref FTLRequestEvent args)
    {
        if (!ent.Comp.Locked || !TryComp<FTLComponent>(ent, out var ftl))
            return;

        var destination = DockedTo(ent, ent.Comp.Camp)
            ? LaborCampShuttleDestination.Station
            : LaborCampShuttleDestination.Camp;
        if (!TryResolveDestination(ent, ent.Comp, destination, out var target))
            return;

        var config = _dock.GetDockingConfig(ent, target);
        if (config != null)
        {
            ftl.TargetCoordinates = config.Coordinates;
            ftl.TargetAngle = config.Angle;
            return;
        }

        ftl.TargetCoordinates = new EntityCoordinates(target, Vector2.Zero);
        ftl.TargetAngle = Angle.Zero;
    }

    private void OnFtlCompleted(Entity<LaborCampShuttleComponent> ent, ref FTLCompletedEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        if (NearEndpoint(ent, ent.Comp))
            ent.Comp.Anchor = _xform.GetWorldPosition(ent);
        else
            _xform.SetWorldPosition(ent, ent.Comp.Anchor);

        if (TryComp<ShuttleComponent>(ent, out var shuttle))
            Hold(ent, ent.Comp, shuttle);
    }

    private void OnConsoleMoved(Entity<ShuttleConsoleComponent> ent, ref EntParentChangedMessage args)
    {
        if (Transform(ent).GridUid is not { } grid || !HasComp<LaborCampShuttleComponent>(grid))
            return;

        LockReaders(ent);
        EnsureComp<ActivatableUIRequiresAccessComponent>(ent);
        EnsureComp<LaborCampShuttleConsoleComponent>(ent);
        EnsureComp<LaborCampShuttlePartComponent>(ent);
    }

    private void OnCockpitMoved(Entity<LaborCampCockpitDoorComponent> ent, ref EntParentChangedMessage args)
    {
        if (Transform(ent).GridUid is not { } grid || !HasComp<LaborCampShuttleComponent>(grid))
            return;

        LockReaders(ent);
    }

    private void OnThrusterMoved(Entity<ThrusterComponent> ent, ref EntParentChangedMessage args)
    {
        if (Transform(ent).GridUid is not { } grid || !HasComp<LaborCampShuttleComponent>(grid))
            return;

        EnsureComp<LaborCampShuttlePartComponent>(ent);
    }

    private void Hold(EntityUid uid, LaborCampShuttleComponent route, ShuttleComponent shuttle)
    {
        if (!route.Locked || InTransit(uid))
            return;

        shuttle.Enabled = false;
        for (var i = 0; i < shuttle.LinearThrust.Length; i++)
            shuttle.LinearThrust[i] = 0f;
        shuttle.AngularThrust = 0f;

        if (DockedToEndpoint(uid, route))
        {
            route.Anchor = _xform.GetWorldPosition(uid);
            return;
        }

        // A stock helm on HardLight can instant-dock to some other station. Pull that joint
        // and fall through to the snap, so the shuttle cannot stay anywhere but the two endpoints.
        if (DockedToAnything(uid))
            _dock.UndockDocks(uid);

        if (!TryComp<PhysicsComponent>(uid, out var body))
            return;

        var here = _xform.GetWorldPosition(uid);
        if ((here - route.Anchor).Length() > SnapDistance)
            _xform.SetWorldPosition(uid, route.Anchor);

        _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
        _physics.SetAngularVelocity(uid, 0f, body: body);
    }

    private bool TryResolveDestination(EntityUid shuttle, LaborCampShuttleComponent route, LaborCampShuttleDestination destination, out EntityUid target)
    {
        target = EntityUid.Invalid;
        switch (destination)
        {
            case LaborCampShuttleDestination.Camp:
                if (!route.Camp.IsValid() || Terminating(route.Camp))
                    return false;
                target = route.Camp;
                return true;
            case LaborCampShuttleDestination.Station:
                if (FindMainStation(shuttle) is not { } station)
                    return false;
                target = station;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Largest station member grid that is not the camp or this shuttle.
    /// Station membership is shared by Frontier and HardLight. The station data type is not.
    /// </summary>
    private EntityUid? FindMainStation(EntityUid shuttle)
    {
        EntityUid? best = null;
        var bestArea = -1f;
        var query = EntityQueryEnumerator<StationMemberComponent, MapGridComponent>();
        while (query.MoveNext(out var grid, out _, out var mapGrid))
        {
            if (grid == shuttle || HasComp<LaborCampShuttleComponent>(grid) || HasComp<LaborCampSiteComponent>(grid))
                continue;

            var area = mapGrid.LocalAABB.Width * mapGrid.LocalAABB.Height;
            if (area <= bestArea)
                continue;

            bestArea = area;
            best = grid;
        }

        return best;
    }

    private bool InTransit(EntityUid shuttle)
    {
        return TryComp<FTLComponent>(shuttle, out var ftl)
               && (ftl.State & (FTLState.Starting | FTLState.Travelling | FTLState.Arriving)) != 0;
    }

    private bool DockedToEndpoint(EntityUid shuttle, LaborCampShuttleComponent route)
    {
        if (route.Camp.IsValid() && DockedTo(shuttle, route.Camp))
            return true;
        return FindMainStation(shuttle) is { } station && DockedTo(shuttle, station);
    }

    private bool DockedToAnything(EntityUid shuttle)
    {
        if (!TryComp<TransformComponent>(shuttle, out var xform))
            return false;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (TryComp<DockingComponent>(child, out var dock) && dock.DockedWith != null)
                return true;
        }

        return false;
    }

    private bool DockedTo(EntityUid shuttle, EntityUid grid)
    {
        if (!TryComp<TransformComponent>(shuttle, out var xform))
            return false;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!TryComp<DockingComponent>(child, out var dock) || dock.DockedWith is not { } other)
                continue;
            if (Transform(other).GridUid == grid)
                return true;
        }

        return false;
    }

    private bool NearEndpoint(EntityUid shuttle, LaborCampShuttleComponent route)
    {
        if (route.Camp.IsValid() && NearGrid(shuttle, route.Camp))
            return true;
        return FindMainStation(shuttle) is { } station && NearGrid(shuttle, station);
    }

    private bool NearGrid(EntityUid shuttle, EntityUid grid)
    {
        if (!TryComp<MapGridComponent>(grid, out var mapGrid))
            return false;

        var gridXform = Transform(grid);
        var center = _xform.GetWorldPosition(grid) + gridXform.WorldRotation.RotateVec(mapGrid.LocalAABB.Center);
        var reach = mapGrid.LocalAABB.Size.Length() * 0.5f + 40f;
        return (_xform.GetWorldPosition(shuttle) - center).Length() <= reach;
    }

    private void ApplyAccess(EntityUid shuttle)
    {
        if (!TryComp<TransformComponent>(shuttle, out var xform))
            return;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!HasComp<ShuttleConsoleComponent>(child) && !HasComp<LaborCampCockpitDoorComponent>(child))
                continue;

            LockReaders(child);
            if (HasComp<ShuttleConsoleComponent>(child))
            {
                EnsureComp<ActivatableUIRequiresAccessComponent>(child);
                EnsureComp<LaborCampShuttleConsoleComponent>(child);
            }
        }
    }

    private void LockReaders(EntityUid uid)
    {
        if (TryComp<AccessReaderComponent>(uid, out var reader))
            Write(uid, reader);

        if (!_containers.TryGetContainer(uid, "board", out var board))
            return;

        foreach (var item in board.ContainedEntities)
        {
            if (TryComp<AccessReaderComponent>(item, out var boardReader))
                Write(item, boardReader);
        }
    }

    /// <summary>
    /// Cockpit and helm take a served sentence or security. The cvar does not change this.
    /// </summary>
    private void Write(EntityUid uid, AccessReaderComponent reader)
    {
        LaborCampAccess.SetAny(_access, uid, reader, ShuttleAccess, Security);
    }

    private void RefreshConsoles(EntityUid shuttle)
    {
        if (!TryComp<TransformComponent>(shuttle, out var xform))
            return;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (HasComp<ShuttleConsoleComponent>(child))
                PushState(child);
        }
    }

    private void PushState(EntityUid console)
    {
        if (!_ui.HasUi(console, LaborCampShuttleUiKey.Key))
            return;
        if (Transform(console).GridUid is not { } grid || !TryComp<LaborCampShuttleComponent>(grid, out var route))
            return;

        var station = FindMainStation(grid);
        var state = new LaborCampShuttleState
        {
            Locked = route.Locked,
            InFtl = HasComp<FTLComponent>(grid),
            CampAvailable = route.Camp.IsValid() && !Terminating(route.Camp),
            StationAvailable = station != null,
            CampDocked = route.Camp.IsValid() && DockedTo(grid, route.Camp),
            StationDocked = station is { } stationId && DockedTo(grid, stationId),
            CampName = route.Camp.IsValid() ? Name(route.Camp) : string.Empty,
            StationName = station is { } named ? Name(named) : string.Empty,
        };
        _ui.SetUiState(console, LaborCampShuttleUiKey.Key, state);
    }

    private void OnUnanchor(EntityUid uid, AnchorableComponent component, UnanchorAttemptEvent args)
    {
        if (Transform(uid).GridUid is not { } grid || !HasComp<LaborCampShuttleComponent>(grid))
            return;

        args.Cancel();
    }

    private void OnFloor(Entity<LaborCampShuttleComponent> ent, ref FloorTileAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnVerbs(EntityUid uid, LaborCampShuttlePartComponent component, GetVerbsEvent<Verb> args)
    {
        if (Transform(uid).GridUid is not { } grid || !HasComp<LaborCampShuttleComponent>(grid))
            return;

        var deconstruct = Loc.GetString("deconstructible-verb-begin-deconstruct");
        List<Verb>? drop = null;
        foreach (var verb in args.Verbs)
        {
            if (verb.Text == deconstruct)
                (drop ??= new List<Verb>()).Add(verb);
        }

        if (drop == null)
            return;

        foreach (var verb in drop)
            args.Verbs.Remove(verb);
    }

    private void Seal(EntityUid shuttle)
    {
        if (!TryComp<TransformComponent>(shuttle, out var xform))
            return;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
            EnsureComp<LaborCampShuttlePartComponent>(child);
    }

    /// <summary>
    /// Frontier's protected-grid flags are not on HardLight. Set whichever of them exist.
    /// </summary>
    private void ProtectGrid(EntityUid grid)
    {
        var comp = EnsureComp<ProtectedGridComponent>(grid);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var name in new[] { "PreventFloorRemoval", "PreventFloorPlacement", "PreventRCDUse" })
        {
            var field = comp.GetType().GetField(name, flags);
            if (field?.FieldType == typeof(bool))
                field.SetValue(comp, true);
        }
    }
}
