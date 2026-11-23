// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared.Gravity;
using Content.Shared.LaborCamp;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Server.LaborCamp;

/// <summary>
/// Optional camp grid. It is not part of any station map. Round-start spawn is off unless configured.
/// Admins can also run the <c>laborcamp</c> command.
/// </summary>
public sealed class LaborCampGridSystem : EntitySystem
{
    private const int Width = 17;
    private const int Height = 11;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private bool _spawn;
    private string _gridPath = string.Empty;
    private float _offsetX = 2500f;
    private float _offsetY = 2500f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);

        Subs.CVar(_cfg, LaborCampCVars.Spawn, value => _spawn = value, true);
        Subs.CVar(_cfg, LaborCampCVars.GridPath, value => _gridPath = value, true);
        Subs.CVar(_cfg, LaborCampCVars.SpawnOffsetX, value => _offsetX = value, true);
        Subs.CVar(_cfg, LaborCampCVars.SpawnOffsetY, value => _offsetY = value, true);
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

        TrySpawnCamp(mapId, new Vector2(_offsetX, _offsetY), out _);
    }

    public Vector2 ConfiguredOffset() => new(_offsetX, _offsetY);

    /// <summary>
    /// Loads <c>labor_camp.grid_path</c> when that file exists, otherwise builds a small scaffold grid.
    /// </summary>
    public bool TrySpawnCamp(MapId mapId, Vector2 worldPosition, out EntityUid grid)
    {
        grid = EntityUid.Invalid;
        if (!_map.MapExists(mapId))
            return false;

        if (TryLoadConfiguredGrid(mapId, worldPosition, out grid))
            return true;

        return TrySpawnScaffold(mapId, worldPosition, out grid);
    }

    private bool TryLoadConfiguredGrid(MapId mapId, Vector2 worldPosition, out EntityUid grid)
    {
        grid = EntityUid.Invalid;
        if (string.IsNullOrWhiteSpace(_gridPath))
            return false;

        ResPath path;
        try
        {
            path = new ResPath(_gridPath);
        }
        catch (Exception ex)
        {
            Log.Warning($"labor_camp.grid_path '{_gridPath}' is not a path: {ex.Message}");
            return false;
        }

        if (!_resources.ContentFileExists(path))
        {
            Log.Warning($"labor_camp.grid_path '{_gridPath}' was not found. Building the scaffold camp instead.");
            return false;
        }

        if (!_loader.TryLoadGrid(mapId, path, out var loaded, offset: worldPosition))
        {
            Log.Warning($"labor_camp.grid_path '{_gridPath}' did not load. Building the scaffold camp instead.");
            return false;
        }

        grid = loaded.Value;
        _meta.SetEntityName(grid, Loc.GetString("labor-camp-grid-name"));
        Log.Info($"Loaded labor camp grid from {path} at {worldPosition}.");
        return true;
    }

    private bool TrySpawnScaffold(MapId mapId, Vector2 worldPosition, out EntityUid grid)
    {
        grid = EntityUid.Invalid;
        if (!_tiles.TryGetDefinition("FloorSteel", out var floor) && !_tiles.TryGetDefinition("Plating", out floor))
        {
            Log.Warning("Labor camp scaffold has no FloorSteel or Plating tile.");
            return false;
        }

        var spawned = _mapManager.CreateGridEntity(mapId);
        grid = spawned.Owner;
        _xform.SetMapCoordinates(grid, new MapCoordinates(worldPosition, mapId));
        _meta.SetEntityName(grid, Loc.GetString("labor-camp-grid-name"));

        var gravity = EnsureComp<GravityComponent>(grid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(grid, gravity);
        EnsureComp<ShuttleComponent>(grid);

        var tile = new Tile(floor.TileId);
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
                _map.SetTile(grid, spawned.Comp, new Vector2i(x, y), tile);
        }

        var enter = new Vector2i(0, Height / 2);
        var leave = new Vector2i(Width - 1, Height / 2);
        var dock = new Vector2i(Width / 2, 0);

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var here = new Vector2i(x, y);
                if (x != 0 && y != 0 && x != Width - 1 && y != Height - 1)
                    continue;

                if (here == enter || here == leave || here == dock)
                    continue;

                TrySpawn(grid, spawned.Comp, "WallSolid", here);
            }
        }

        TrySpawn(grid, spawned.Comp, LaborCampPrototypes.EnterGate, enter);
        TrySpawn(grid, spawned.Comp, LaborCampPrototypes.LeaveGate, leave);
        TrySpawn(grid, spawned.Comp, LaborCampPrototypes.ShuttleAirlock, dock);
        TrySpawn(grid, spawned.Comp, LaborCampPrototypes.Locker, new Vector2i(3, 7));
        TrySpawn(grid, spawned.Comp, LaborCampPrototypes.Redemption, new Vector2i(5, 7));
        TrySpawn(grid, spawned.Comp, LaborCampPrototypes.Console, new Vector2i(7, 7));
        TrySpawn(grid, spawned.Comp, LaborCampPrototypes.ShuttleConsole, new Vector2i(10, 7));
        TrySpawn(grid, spawned.Comp, LaborCampPrototypes.Sign, new Vector2i(3, 5));
        TrySpawn(grid, spawned.Comp, "GasMinerOxygen", new Vector2i(13, 7));
        TrySpawn(grid, spawned.Comp, "AlwaysPoweredWallLight", new Vector2i(8, 8));
        TrySpawn(grid, spawned.Comp, "AlwaysPoweredWallLight", new Vector2i(8, 3));
        TrySpawnUnpowered(grid, spawned.Comp, "Thruster", new Vector2i(13, 3));
        TrySpawnUnpowered(grid, spawned.Comp, "Gyroscope", new Vector2i(14, 3));
        TrySpawn(grid, spawned.Comp, "SteelOre", new Vector2i(3, 3));
        TrySpawn(grid, spawned.Comp, "PlasmaOre", new Vector2i(4, 3));
        TrySpawn(grid, spawned.Comp, "GoldOre", new Vector2i(5, 3));
        TrySpawn(grid, spawned.Comp, "DiamondOre", new Vector2i(6, 3));
        TrySpawn(grid, spawned.Comp, "SilverOre", new Vector2i(7, 3));

        Log.Info($"Spawned labor camp scaffold at {worldPosition} on map {mapId}.");
        return true;
    }

    private void TrySpawn(EntityUid grid, MapGridComponent gridComp, string prototype, Vector2i tile)
    {
        if (!_proto.HasIndex<EntityPrototype>(prototype))
        {
            Log.Warning($"Labor camp scaffold skipped missing prototype {prototype}.");
            return;
        }

        Spawn(prototype, _map.GridTileToLocal(grid, gridComp, tile));
    }

    private void TrySpawnUnpowered(EntityUid grid, MapGridComponent gridComp, string prototype, Vector2i tile)
    {
        if (!_proto.HasIndex<EntityPrototype>(prototype))
        {
            Log.Warning($"Labor camp scaffold skipped missing prototype {prototype}.");
            return;
        }

        var uid = Spawn(prototype, _map.GridTileToLocal(grid, gridComp, tile));
        if (HasComp<ApcPowerReceiverComponent>(uid))
            _power.SetNeedsPower(uid, false);
    }
}
