// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Gravity;
using Content.Shared.LaborCamp;
using Content.Shared.Mining.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Server.LaborCamp;

/// <summary>
/// Spawns the camp and a separate prisoner shuttle, then docks them.
/// The camp grid is not a shuttle: it is static, and helm consoles on it are security-only.
/// </summary>
public sealed class LaborCampGridSystem : EntitySystem
{
    private const int CampWidth = 17;
    private const int CampHeight = 11;
    private const int ShuttleWidth = 5;
    private const int ShuttleHeight = 4;

    private static readonly ProtoId<AccessLevelPrototype> SecurityAccess = LaborCampPrototypes.SecurityAccess;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private DockingSystem _docking = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private LaborCampShuttleSystem _route = default!;

    private bool _spawn;
    private string _gridPath = string.Empty;
    private string _shuttlePath = string.Empty;
    private float _offsetX = 2500f;
    private float _offsetY = 2500f;
    private float _regrowMinutes = 12f;
    private float _regrowAccum;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);

        Subs.CVar(_cfg, LaborCampCVars.Spawn, value => _spawn = value, true);
        Subs.CVar(_cfg, LaborCampCVars.GridPath, value => _gridPath = value, true);
        Subs.CVar(_cfg, LaborCampCVars.ShuttleGridPath, value => _shuttlePath = value, true);
        Subs.CVar(_cfg, LaborCampCVars.SpawnOffsetX, value => _offsetX = value, true);
        Subs.CVar(_cfg, LaborCampCVars.SpawnOffsetY, value => _offsetY = value, true);
        Subs.CVar(_cfg, LaborCampCVars.RockRegrowMinutes, value => _regrowMinutes = value, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_regrowMinutes <= 0f || float.IsNaN(_regrowMinutes))
        {
            _regrowAccum = 0f;
            return;
        }

        _regrowAccum += frameTime;
        if (_regrowAccum < _regrowMinutes * 60f)
            return;

        _regrowAccum = 0f;
        var refilled = RefillRocks();
        if (refilled > 0)
            Log.Info($"Labor camp regrew {refilled} rocks.");
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (!_spawn)
            return;

        var mapId = _ticker.DefaultMap;
        if (!_map.MapExists(mapId))
        {
            Log.Warning("labor_camp.spawn is set, but the round has no default map. The camp was not spawned.");
            return;
        }

        TrySpawnCamp(mapId, new Vector2(_offsetX, _offsetY), out _, out _, out _);
    }

    public Vector2 ConfiguredOffset() => new(_offsetX, _offsetY);

    /// <summary>
    /// Loads the camp and the prisoner shuttle, then docks the shuttle to the camp.
    /// A missing path or a failed load uses the matching scaffold.
    /// </summary>
    public bool TrySpawnCamp(MapId mapId, Vector2 worldPosition, out EntityUid camp, out EntityUid shuttle, out bool docked)
    {
        camp = EntityUid.Invalid;
        shuttle = EntityUid.Invalid;
        docked = false;
        if (!_map.MapExists(mapId))
            return false;

        if (!TryLoadOrScaffold(mapId, worldPosition, _gridPath, "labor_camp.grid_path", out camp, shuttle: false))
            return false;

        FixCampInPlace(camp);
        RecordMine(camp);
        LockCampConsoles(camp);

        // Park the shuttle clear of the camp. Docking moves it onto the camp dock.
        var shuttlePark = worldPosition + new Vector2(CampWidth + ShuttleWidth + 8f, 0f);
        if (!TryLoadOrScaffold(mapId, shuttlePark, _shuttlePath, "labor_camp.shuttle_grid_path", out shuttle, shuttle: true))
            return false;

        MakeFlyable(shuttle, camp);
        docked = TryDockShuttle(shuttle, camp);
        if (!docked)
        {
            Log.Warning("The labor shuttle did not dock. It was left beside the camp. " +
                        "The camp needs a docking airlock (AirlockLaborCampDock) and the shuttle needs AirlockLaborCampShuttle, both DockType Airlock, facing outward so the grids do not overlap.");
        }

        return true;
    }

    /// <summary>
    /// Spawns a fresh rock on every recorded mine tile that is empty.
    /// </summary>
    public int RefillRocks()
    {
        var refilled = 0;
        var query = EntityQueryEnumerator<LaborCampMineComponent, MapGridComponent>();
        while (query.MoveNext(out var grid, out var mine, out var gridComp))
        {
            var occupied = OccupiedTiles(grid, gridComp);
            foreach (var tile in mine.Tiles)
            {
                if (occupied.Contains(tile))
                    continue;

                if (TrySpawnRock(grid, gridComp, tile))
                    refilled++;
            }
        }

        return refilled;
    }

    private bool TryLoadOrScaffold(MapId mapId, Vector2 worldPosition, string pathText, string cvar, out EntityUid grid, bool shuttle)
    {
        if (TryLoadConfiguredGrid(mapId, worldPosition, pathText, cvar, shuttle, out grid))
            return true;

        return shuttle
            ? TrySpawnShuttleScaffold(mapId, worldPosition, out grid)
            : TrySpawnCampScaffold(mapId, worldPosition, out grid);
    }

    private bool TryLoadConfiguredGrid(MapId mapId, Vector2 worldPosition, string pathText, string cvar, bool shuttle, out EntityUid grid)
    {
        grid = EntityUid.Invalid;
        if (string.IsNullOrWhiteSpace(pathText))
            return false;

        ResPath path;
        try
        {
            path = new ResPath(pathText);
        }
        catch (Exception ex)
        {
            Log.Warning($"{cvar} '{pathText}' is not a path: {ex.Message}");
            return false;
        }

        if (!_resources.ContentFileExists(path))
        {
            Log.Warning($"{cvar} '{pathText}' was not found. Building the scaffold instead.");
            return false;
        }

        if (!_loader.TryLoadGrid(mapId, path, out var loaded, offset: worldPosition))
        {
            Log.Warning($"{cvar} '{pathText}' did not load. Building the scaffold instead.");
            return false;
        }

        grid = loaded.Value;
        _meta.SetEntityName(grid, Loc.GetString(shuttle ? "labor-camp-shuttle-grid-name" : "labor-camp-grid-name"));
        Log.Info($"Loaded labor {(shuttle ? "shuttle" : "camp")} grid from {path} at {worldPosition}.");
        return true;
    }

    private bool TrySpawnCampScaffold(MapId mapId, Vector2 worldPosition, out EntityUid grid)
    {
        grid = EntityUid.Invalid;
        if (!TryCreateFlooredGrid(mapId, worldPosition, CampWidth, CampHeight, "labor-camp-grid-name", out grid, out var gridComp))
            return false;

        var enter = new Vector2i(0, CampHeight / 2);
        var leave = new Vector2i(CampWidth - 1, CampHeight / 2);
        var dock = new Vector2i(CampWidth / 2, 0);

        for (var x = 0; x < CampWidth; x++)
        {
            for (var y = 0; y < CampHeight; y++)
            {
                var here = new Vector2i(x, y);
                if (x != 0 && y != 0 && x != CampWidth - 1 && y != CampHeight - 1)
                    continue;

                if (here == enter || here == leave || here == dock)
                    continue;

                TrySpawn(grid, gridComp, "WallSolid", here);
            }
        }

        TrySpawn(grid, gridComp, LaborCampPrototypes.EnterGate, enter);
        TrySpawn(grid, gridComp, LaborCampPrototypes.LeaveGate, leave);
        TrySpawnDock(grid, gridComp, LaborCampPrototypes.CampDock, dock, Angle.Zero);
        TrySpawn(grid, gridComp, LaborCampPrototypes.Locker, new Vector2i(3, 7));
        TrySpawn(grid, gridComp, LaborCampPrototypes.Redemption, new Vector2i(5, 7));
        TrySpawn(grid, gridComp, LaborCampPrototypes.Console, new Vector2i(7, 7));
        TrySpawn(grid, gridComp, LaborCampPrototypes.Sign, new Vector2i(3, 5));
        TrySpawn(grid, gridComp, "GasMinerOxygen", new Vector2i(13, 7));
        TrySpawn(grid, gridComp, "AlwaysPoweredWallLight", new Vector2i(8, 8));
        TrySpawn(grid, gridComp, "AlwaysPoweredWallLight", new Vector2i(8, 3));

        for (var x = 3; x <= 7; x++)
            TrySpawn(grid, gridComp, RockPrototype() ?? LaborCampPrototypes.Rock, new Vector2i(x, 3));

        Log.Info($"Spawned labor camp scaffold at {worldPosition} on map {mapId}.");
        return true;
    }

    private bool TrySpawnShuttleScaffold(MapId mapId, Vector2 worldPosition, out EntityUid grid)
    {
        grid = EntityUid.Invalid;
        if (!TryCreateFlooredGrid(mapId, worldPosition, ShuttleWidth, ShuttleHeight, "labor-camp-shuttle-grid-name", out grid, out var gridComp))
            return false;

        // North edge faces the camp dock. Angle 0 points the docking port south, so the shuttle port is turned north.
        var dock = new Vector2i(ShuttleWidth / 2, ShuttleHeight - 1);
        var westThruster = new Vector2i(0, 2);
        var eastThruster = new Vector2i(ShuttleWidth - 1, 2);

        for (var x = 0; x < ShuttleWidth; x++)
        {
            for (var y = 0; y < ShuttleHeight; y++)
            {
                var here = new Vector2i(x, y);
                if (x != 0 && y != 0 && x != ShuttleWidth - 1 && y != ShuttleHeight - 1)
                    continue;

                if (here == dock || here == westThruster || here == eastThruster)
                    continue;

                TrySpawn(grid, gridComp, ResolveProto(LaborCampPrototypes.Wall, "WallSolid"), here);
            }
        }

        // A wall across the dock closes the walk-around. The only way into the helm is the cockpit door.
        var cockpit = new Vector2i(ShuttleWidth / 2, 2);
        TrySpawn(grid, gridComp, ResolveProto(LaborCampPrototypes.Wall, "WallSolid"), new Vector2i(1, 2));
        TrySpawn(grid, gridComp, ResolveProto(LaborCampPrototypes.Wall, "WallSolid"), new Vector2i(ShuttleWidth - 2, 2));
        TrySpawn(grid, gridComp, LaborCampPrototypes.CockpitDoor, cockpit, rotation: null, anchor: true);

        TrySpawnDock(grid, gridComp, LaborCampPrototypes.ShuttleAirlock, dock, Angle.FromDegrees(180));
        // Anchored so security can pilot. Map-placed consoles are anchored; a code spawn is not.
        TrySpawn(grid, gridComp, LaborCampPrototypes.ShuttleConsole, new Vector2i(2, 1), rotation: null, anchor: true);
        TrySpawnUnpowered(grid, gridComp, ResolveProto(LaborCampPrototypes.Thruster, "Thruster"), westThruster, Angle.Zero);
        TrySpawnUnpowered(grid, gridComp, ResolveProto(LaborCampPrototypes.Thruster, "Thruster"), eastThruster, Angle.FromDegrees(180));
        TrySpawnUnpowered(grid, gridComp, ResolveProto(LaborCampPrototypes.Gyroscope, "Gyroscope"), new Vector2i(3, 1), Angle.Zero);
        TrySpawn(grid, gridComp, "GasMinerOxygen", new Vector2i(1, 1));
        TrySpawn(grid, gridComp, "AlwaysPoweredWallLight", new Vector2i(1, 2));

        Log.Info($"Spawned labor shuttle scaffold at {worldPosition} on map {mapId}.");
        return true;
    }

    private bool TryCreateFlooredGrid(
        MapId mapId,
        Vector2 worldPosition,
        int width,
        int height,
        string nameKey,
        out EntityUid grid,
        out MapGridComponent gridComp)
    {
        grid = EntityUid.Invalid;
        gridComp = default!;
        if (!_tiles.TryGetDefinition("FloorSteel", out var floor) && !_tiles.TryGetDefinition("Plating", out floor))
        {
            Log.Warning("Labor camp scaffold has no FloorSteel or Plating tile.");
            return false;
        }

        var spawned = _mapManager.CreateGridEntity(mapId);
        grid = spawned.Owner;
        gridComp = spawned.Comp;
        _xform.SetMapCoordinates(grid, new MapCoordinates(worldPosition, mapId));
        _meta.SetEntityName(grid, Loc.GetString(nameKey));

        var gravity = EnsureComp<GravityComponent>(grid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(grid, gravity);

        var tile = new Tile(floor.TileId);
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
                _map.SetTile(grid, gridComp, new Vector2i(x, y), tile);
        }

        return true;
    }

    /// <summary>
    /// The camp is not a ship. Static body so a docked shuttle cannot drag it,
    /// and <see cref="PreventPilotComponent"/> so thrust is not applied if a shuttle component is added later.
    /// </summary>
    private void FixCampInPlace(EntityUid grid)
    {
        EnsureComp<LaborCampSiteComponent>(grid);
        EnsureComp<PreventPilotComponent>(grid);

        if (HasComp<ShuttleComponent>(grid))
            RemComp<ShuttleComponent>(grid);

        if (!TryComp<PhysicsComponent>(grid, out var body))
            return;

        var fixtures = CompOrNull<FixturesComponent>(grid);
        _physics.SetBodyType(grid, BodyType.Static, fixtures, body);
        _physics.SetBodyStatus(grid, body, BodyStatus.OnGround);
        _physics.SetFixedRotation(grid, true, manager: fixtures, body: body);
    }

    private void MakeFlyable(EntityUid grid, EntityUid camp)
    {
        RemComp<LaborCampSiteComponent>(grid);
        RemComp<PreventPilotComponent>(grid);
        var shuttle = EnsureComp<ShuttleComponent>(grid);
        if (TryComp<PhysicsComponent>(grid, out var body))
            _shuttle.Enable(grid, CompOrNull<FixturesComponent>(grid), body, shuttle);
        _route.Attach(grid, camp);
    }

    private string ResolveProto(string preferred, string fallback)
    {
        return _proto.HasIndex<EntityPrototype>(preferred) ? preferred : fallback;
    }

    /// <summary>
    /// Same alignment the shipyard and grid-fill use: <see cref="DockingSystem.GetDockingConfig(EntityUid, EntityUid, EntityUid, DockingComponent, EntityUid, DockingComponent)"/>
    /// then <see cref="ShuttleSystem.FTLDock"/>. Prefers the labor dock pair. Any other Airlock docks are the fallback.
    /// If nothing fits, the shuttle stays where it was parked.
    /// </summary>
    private bool TryDockShuttle(EntityUid shuttle, EntityUid camp)
    {
        if (!TryComp<ShuttleComponent>(shuttle, out var shuttleComp))
            return false;

        var shuttleDock = FindDock(shuttle, LaborCampPrototypes.ShuttleAirlock);
        var campDock = FindDock(camp, LaborCampPrototypes.CampDock);
        if (shuttleDock != null && campDock != null)
        {
            var config = _docking.GetDockingConfig(
                shuttle,
                camp,
                shuttleDock.Value.Owner,
                shuttleDock.Value.Comp,
                campDock.Value.Owner,
                campDock.Value.Comp);

            if (config != null)
            {
                _shuttle.FTLDock((shuttle, Transform(shuttle)), config);
                return true;
            }

            Log.Warning("AirlockLaborCampShuttle and AirlockLaborCampDock could not be aligned. Trying any free dock pair.");
        }
        else
        {
            Log.Warning("The labor shuttle or camp is missing its named dock. Trying any free dock pair.");
        }

        return _shuttle.TryFTLDock(shuttle, shuttleComp, camp);
    }

    private Entity<DockingComponent>? FindDock(EntityUid grid, string prototypeId)
    {
        if (!TryComp<TransformComponent>(grid, out var xform))
            return null;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!TryComp<DockingComponent>(child, out var dock))
                continue;

            if (PrototypeId(child) == prototypeId)
                return (child, dock);
        }

        return null;
    }

    private void RecordMine(EntityUid grid)
    {
        var mine = EnsureComp<LaborCampMineComponent>(grid);
        mine.Tiles.Clear();
        if (!TryComp<MapGridComponent>(grid, out var gridComp) || !TryComp<TransformComponent>(grid, out var xform))
            return;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!HasComp<OreVeinComponent>(child))
                continue;

            mine.Tiles.Add(_map.LocalToTile(grid, gridComp, Transform(child).Coordinates));
        }
    }

    private void LockCampConsoles(EntityUid grid)
    {
        if (!TryComp<TransformComponent>(grid, out var xform))
            return;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!HasComp<ShuttleConsoleComponent>(child))
                continue;

            var reader = EnsureComp<AccessReaderComponent>(child);
            LaborCampAccess.SetAny(_access, child, reader, SecurityAccess);
            EnsureComp<ActivatableUIRequiresAccessComponent>(child);
        }
    }

    private HashSet<Vector2i> OccupiedTiles(EntityUid grid, MapGridComponent gridComp)
    {
        var tiles = new HashSet<Vector2i>();
        if (!TryComp<TransformComponent>(grid, out var xform))
            return tiles;

        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!TryComp<TransformComponent>(child, out var childXform))
                continue;

            tiles.Add(_map.LocalToTile(grid, gridComp, childXform.Coordinates));
        }

        return tiles;
    }

    private bool TrySpawnRock(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        var prototype = RockPrototype();
        if (prototype == null)
        {
            Log.Warning("Labor camp has no LaborCampRock or AsteroidRockMining prototype. The mine was not refilled.");
            return false;
        }

        TrySpawn(grid, gridComp, prototype, tile);
        return _proto.HasIndex<EntityPrototype>(prototype);
    }

    private string? RockPrototype()
    {
        if (_proto.HasIndex<EntityPrototype>(LaborCampPrototypes.Rock))
            return LaborCampPrototypes.Rock;

        EntProtoId fallback = "AsteroidRockMining";
        if (_proto.HasIndex(fallback))
            return fallback;

        return null;
    }

    private string? PrototypeId(EntityUid uid)
    {
        return TryComp<MetaDataComponent>(uid, out var meta) ? meta.EntityPrototype?.ID : null;
    }

    private void TrySpawn(EntityUid grid, MapGridComponent gridComp, string prototype, Vector2i tile)
    {
        TrySpawn(grid, gridComp, prototype, tile, null);
    }

    private void TrySpawnDock(EntityUid grid, MapGridComponent gridComp, string prototype, Vector2i tile, Angle rotation)
    {
        TrySpawn(grid, gridComp, prototype, tile, rotation);
    }

    private void TrySpawn(EntityUid grid, MapGridComponent gridComp, string prototype, Vector2i tile, Angle? rotation, bool anchor = false)
    {
        if (!_proto.HasIndex<EntityPrototype>(prototype))
        {
            Log.Warning($"Labor camp scaffold skipped missing prototype {prototype}.");
            return;
        }

        var uid = Spawn(prototype, _map.GridTileToLocal(grid, gridComp, tile));
        var xform = Transform(uid);
        if (rotation != null)
            _xform.SetLocalRotation(uid, rotation.Value, xform);

        // Structures such as airlocks spawn already anchored. Anchoring them again asserts in the snap grid.
        if ((anchor || rotation != null || HasComp<DockingComponent>(uid)) && !xform.Anchored)
            _xform.AnchorEntity(uid, xform);
    }

    private void TrySpawnUnpowered(EntityUid grid, MapGridComponent gridComp, string prototype, Vector2i tile, Angle rotation)
    {
        if (!_proto.HasIndex<EntityPrototype>(prototype))
        {
            Log.Warning($"Labor camp scaffold skipped missing prototype {prototype}.");
            return;
        }

        var uid = Spawn(prototype, _map.GridTileToLocal(grid, gridComp, tile));
        var xform = Transform(uid);
        _xform.SetLocalRotation(uid, rotation, xform);
        if (!xform.Anchored)
            _xform.AnchorEntity(uid, xform);
        if (HasComp<ApcPowerReceiverComponent>(uid))
            _power.SetNeedsPower(uid, false);
    }
}
