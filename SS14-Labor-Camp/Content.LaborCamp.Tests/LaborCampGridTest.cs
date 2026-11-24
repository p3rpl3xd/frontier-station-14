// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Robust.Server.GameObjects;
using Content.IntegrationTests;
using Content.Server.Doors.Systems;
using Content.Server.LaborCamp;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.LaborCamp;
using Content.Shared.Mining.Components;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.LaborCamp;

[TestFixture]
public sealed class LaborCampGridTest
{
    [Test]
    public async Task ShuttleDocksAndCampStaysPut()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        await server.WaitAssertion(() =>
        {
            var map = entMan.EntitySysManager.GetEntitySystem<SharedMapSystem>();
            map.CreateMap(out var mapId);
            var grids = entMan.EntitySysManager.GetEntitySystem<LaborCampGridSystem>();
            var spot = new Vector2(200f, 200f);
            Assert.That(grids.TrySpawnCamp(mapId, spot, out var camp, out var shuttle, out var docked), Is.True);
            Assert.That(docked, Is.True, "shuttle should dock to the camp");
            Assert.That(camp, Is.Not.EqualTo(shuttle));

            Assert.That(entMan.HasComponent<LaborCampSiteComponent>(camp), Is.True);
            Assert.That(entMan.HasComponent<PreventPilotComponent>(camp), Is.True);
            Assert.That(entMan.HasComponent<ShuttleComponent>(camp), Is.False);
            Assert.That(entMan.GetComponent<PhysicsComponent>(camp).BodyType, Is.EqualTo(BodyType.Static));

            Assert.That(entMan.HasComponent<ShuttleComponent>(shuttle), Is.True);
            Assert.That(entMan.HasComponent<PreventPilotComponent>(shuttle), Is.False);
            Assert.That(entMan.HasComponent<LaborCampSiteComponent>(shuttle), Is.False);

            Assert.That(OnGrid(entMan, camp, LaborCampPrototypes.CampDock), Is.True);
            Assert.That(OnGrid(entMan, camp, LaborCampPrototypes.ShuttleConsole), Is.False);
            Assert.That(OnGrid(entMan, camp, LaborCampPrototypes.ShuttleAirlock), Is.False);
            Assert.That(OnGrid(entMan, shuttle, LaborCampPrototypes.ShuttleAirlock), Is.True);
            Assert.That(OnGrid(entMan, shuttle, LaborCampPrototypes.ShuttleConsole), Is.True);

            var joined = false;
            var docks = entMan.EntityQueryEnumerator<DockingComponent, TransformComponent>();
            while (docks.MoveNext(out _, out var dock, out var xform))
            {
                if (xform.GridUid != shuttle || dock.DockedWith is not { } other)
                    continue;
                if (entMan.GetComponent<TransformComponent>(other).GridUid == camp)
                    joined = true;
            }

            Assert.That(joined, Is.True, "a shuttle dock should be welded to a camp dock");

            var campPos = entMan.EntitySysManager.GetEntitySystem<SharedTransformSystem>().GetWorldPosition(camp);
            Assert.That(campPos.X, Is.EqualTo(spot.X).Within(0.1f));
            Assert.That(campPos.Y, Is.EqualTo(spot.Y).Within(0.1f));

            map.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReleasedIdPilotsShuttleNotCamp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        MapId mapId = default;
        EntityUid camp = default;
        EntityUid shuttle = default;
        EntityUid prisoner = default;
        EntityUid? shuttleConsole = null;
        Vector2 anchor = default;

        await server.WaitAssertion(() =>
        {
            var map = entMan.EntitySysManager.GetEntitySystem<SharedMapSystem>();
            map.CreateMap(out mapId);
            map.SetPaused(mapId, false);
            var grids = entMan.EntitySysManager.GetEntitySystem<LaborCampGridSystem>();
            Assert.That(grids.TrySpawnCamp(mapId, Vector2.Zero, out camp, out shuttle, out var docked), Is.True);
            Assert.That(docked, Is.True);

            var labor = entMan.EntitySysManager.GetEntitySystem<LaborCampSystem>();
            var id = entMan.SpawnEntity(LaborCampPrototypes.PrisonerId, new MapCoordinates(Vector2.Zero, mapId));
            labor.ApplySentence(id, 1, 0f);
            var ore = entMan.SpawnEntity("SteelOre1", new MapCoordinates(Vector2.Zero, mapId));
            Assert.That(labor.TryAwardOre(id, ore, out _), Is.True);

            prisoner = entMan.SpawnEntity("MobHuman", new EntityCoordinates(shuttle, new Vector2(2.5f, 1.5f)));
            entMan.EnsureComponent<AccessComponent>(prisoner);
            var access = entMan.EntitySysManager.GetEntitySystem<SharedAccessSystem>();
            Assert.That(access.TrySetTags(prisoner, access.TryGetTags(id)!), Is.True);
            shuttleConsole = FindOnGrid(entMan, shuttle, LaborCampPrototypes.ShuttleConsole);
            Assert.That(shuttleConsole, Is.Not.Null);

            var mapManager = server.ResolveDependency<IMapManager>();
            var tiles = server.ResolveDependency<ITileDefinitionManager>();
            Assert.That(tiles.TryGetDefinition("Plating", out var plating), Is.True);
            var stationGrid = mapManager.CreateGridEntity(mapId);
            var xform = entMan.EntitySysManager.GetEntitySystem<SharedTransformSystem>();
            xform.SetMapCoordinates(stationGrid.Owner, new MapCoordinates(new Vector2(300f, 300f), mapId));
            var tile = new Tile(plating!.TileId);
            map.SetTile(stationGrid.Owner, stationGrid.Comp, Vector2i.Zero, tile);
            map.SetTile(stationGrid.Owner, stationGrid.Comp, new Vector2i(1, 0), tile);
            entMan.SpawnEntity("AirlockShuttle", new EntityCoordinates(stationGrid.Owner, new Vector2(0.5f, 0.5f)));
            var station = entMan.SpawnEntity("TestStation", MapCoordinates.Nullspace);
            entMan.EntitySysManager.GetEntitySystem<StationSystem>().AddGridToStation(station, stationGrid.Owner);
        });

        // Power and grid membership settle on the next ticks. needsPower: false still starts unpowered.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var doors = entMan.EntitySysManager.GetEntitySystem<DoorSystem>();
            var readers = entMan.EntitySysManager.GetEntitySystem<AccessReaderSystem>();
            var airlock = FindOnGrid(entMan, shuttle, LaborCampPrototypes.ShuttleAirlock);
            var cockpit = FindOnGrid(entMan, shuttle, LaborCampPrototypes.CockpitDoor);
            Assert.That(airlock, Is.Not.Null);
            Assert.That(cockpit, Is.Not.Null);
            Assert.That(doors.HasAccess(airlock!.Value, prisoner), Is.True, "released ID should open the shuttle dock");
            Assert.That(doors.HasAccess(cockpit!.Value, prisoner), Is.True, "released ID should open the cockpit");
            Assert.That(readers.IsAllowed(prisoner, shuttleConsole!.Value), Is.True);

            var openShuttle = OpenAttempt(prisoner);
            entMan.EventBus.RaiseLocalEvent(shuttleConsole.Value, openShuttle);
            Assert.That(openShuttle.Cancelled, Is.False, "released ID should open the shuttle helm");

            var route = entMan.EntitySysManager.GetEntitySystem<LaborCampShuttleSystem>();
            Assert.That(route.OfferedDestinations(shuttle), Is.EquivalentTo(new[]
            {
                LaborCampShuttleDestination.Camp,
                LaborCampShuttleDestination.Station,
            }));

            var attempt = new ConsoleFTLAttemptEvent(shuttle, false, string.Empty);
            entMan.EventBus.RaiseLocalEvent(shuttle, ref attempt);
            Assert.That(attempt.Cancelled, Is.True, "the map helm cannot pick a free destination");

            var swapped = entMan.SpawnEntity("ComputerShuttle", new EntityCoordinates(shuttle, new Vector2(1.5f, 1.5f)));
            Assert.That(readers.IsAllowed(prisoner, swapped), Is.True, "a rebuilt helm keeps prisoner access");
            var swappedAttempt = new ConsoleFTLAttemptEvent(shuttle, false, string.Empty);
            entMan.EventBus.RaiseLocalEvent(shuttle, ref swappedAttempt);
            Assert.That(swappedAttempt.Cancelled, Is.True, "a rebuilt helm is still route locked");

            var ship = entMan.GetComponent<ShuttleComponent>(shuttle);
            Assert.That(ship.Enabled, Is.False, "manual piloting is disabled");
            foreach (var value in ship.LinearThrust)
                Assert.That(value, Is.EqualTo(0f));
            Assert.That(ship.AngularThrust, Is.EqualTo(0f));

            var unanchor = new UnanchorAttemptEvent(prisoner, shuttleConsole.Value);
            entMan.EventBus.RaiseLocalEvent(shuttleConsole.Value, unanchor);
            Assert.That(unanchor.Cancelled, Is.True, "the helm cannot be unanchored");

            var xformSys = entMan.EntitySysManager.GetEntitySystem<SharedTransformSystem>();
            anchor = xformSys.GetWorldPosition(shuttle);
            entMan.EntitySysManager.GetEntitySystem<DockingSystem>().UndockDocks(shuttle);
            entMan.EntitySysManager.GetEntitySystem<SharedPhysicsSystem>()
                .SetLinearVelocity(shuttle, new Vector2(30f, 0f));
            xformSys.SetWorldPosition(shuttle, anchor + new Vector2(30f, 0f));

            var campConsole = entMan.SpawnEntity(
                LaborCampPrototypes.ShuttleConsole,
                new EntityCoordinates(camp, new Vector2(8.5f, 5.5f)));
            Assert.That(entMan.GetComponent<TransformComponent>(campConsole).GridUid, Is.EqualTo(camp));

            var before = xformSys.GetWorldPosition(camp);
            var openCamp = OpenAttempt(prisoner);
            entMan.EventBus.RaiseLocalEvent(campConsole, openCamp);
            Assert.That(openCamp.Cancelled, Is.True, "released ID must not open a helm on the camp");
            var after = xformSys.GetWorldPosition(camp);
            Assert.That(after.X, Is.EqualTo(before.X).Within(0.01f));
            Assert.That(after.Y, Is.EqualTo(before.Y).Within(0.01f));
            Assert.That(entMan.GetComponent<PhysicsComponent>(camp).BodyType, Is.EqualTo(BodyType.Static));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var xformSys = entMan.EntitySysManager.GetEntitySystem<SharedTransformSystem>();
            var back = xformSys.GetWorldPosition(shuttle);
            Assert.That((back - anchor).Length(), Is.LessThan(2.5f), "the shuttle snaps back instead of flying off");
            Assert.That(entMan.GetComponent<PhysicsComponent>(shuttle).LinearVelocity.Length(), Is.LessThan(0.1f));

            var route = entMan.EntitySysManager.GetEntitySystem<LaborCampShuttleSystem>();
            Assert.That(route.TryTravel(shuttleConsole!.Value, LaborCampShuttleDestination.Station), Is.True,
                "released ID can FTL to the main station");
            Assert.That(entMan.HasComponent<FTLComponent>(shuttle), Is.True);
            entMan.RemoveComponent<FTLComponent>(shuttle);
            Assert.That(route.TryTravel(shuttleConsole.Value, LaborCampShuttleDestination.Camp), Is.True,
                "released ID can FTL to the labor camp");

            entMan.EntitySysManager.GetEntitySystem<SharedMapSystem>().DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RocksRegrowOnEmptyTilesOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        await server.WaitAssertion(() =>
        {
            var map = entMan.EntitySysManager.GetEntitySystem<SharedMapSystem>();
            map.CreateMap(out var mapId);
            var grids = entMan.EntitySysManager.GetEntitySystem<LaborCampGridSystem>();
            Assert.That(grids.TrySpawnCamp(mapId, Vector2.Zero, out var camp, out _, out _), Is.True);
            Assert.That(entMan.TryGetComponent(camp, out LaborCampMineComponent? mine), Is.True);
            Assert.That(mine!.Tiles, Is.Not.Empty);

            var rocks = RocksOn(entMan, camp);
            Assert.That(rocks, Is.Not.Empty);
            var blocked = entMan.GetComponent<TransformComponent>(rocks[0]).Coordinates;
            var mapSys = entMan.EntitySysManager.GetEntitySystem<SharedMapSystem>();
            var blockedTile = mapSys.LocalToTile(
                camp,
                entMan.GetComponent<MapGridComponent>(camp),
                blocked);

            foreach (var rock in rocks)
                entMan.DeleteEntity(rock);

            entMan.SpawnEntity("WallSolid", blocked);
            var refilled = grids.RefillRocks();
            Assert.That(refilled, Is.EqualTo(mine.Tiles.Count - 1));

            var back = RocksOn(entMan, camp);
            Assert.That(back, Has.Count.EqualTo(mine.Tiles.Count - 1));
            foreach (var rock in back)
            {
                var tile = mapSys.LocalToTile(
                    camp,
                    entMan.GetComponent<MapGridComponent>(camp),
                    entMan.GetComponent<TransformComponent>(rock).Coordinates);
                Assert.That(tile, Is.Not.EqualTo(blockedTile));
                Assert.That(entMan.GetComponent<MetaDataComponent>(rock).EntityPrototype?.ID, Is.EqualTo(LaborCampPrototypes.Rock));
            }

            map.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }

    private static List<EntityUid> RocksOn(IServerEntityManager entMan, EntityUid grid)
    {
        var found = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<OreVeinComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == grid)
                found.Add(uid);
        }

        return found;
    }

    private static bool OnGrid(IServerEntityManager entMan, EntityUid grid, string prototype)
    {
        return FindOnGrid(entMan, grid, prototype) != null;
    }

    private static EntityUid? FindOnGrid(IServerEntityManager entMan, EntityUid grid, string prototype)
    {
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var xform))
        {
            if (xform.GridUid == grid && meta.EntityPrototype?.ID == prototype)
                return uid;
        }

        return null;
    }

    /// <summary>
    /// Frontier's open-attempt takes a silent flag. HardLight's takes only the user.
    /// </summary>
    private static ActivatableUIOpenAttemptEvent OpenAttempt(EntityUid user)
    {
        var type = typeof(ActivatableUIOpenAttemptEvent);
        var two = type.GetConstructor(new[] { typeof(EntityUid), typeof(bool) });
        if (two != null)
            return (ActivatableUIOpenAttemptEvent)two.Invoke(new object[] { user, true })!;

        var one = type.GetConstructor(new[] { typeof(EntityUid) })!;
        return (ActivatableUIOpenAttemptEvent)one.Invoke(new object[] { user })!;
    }
}
