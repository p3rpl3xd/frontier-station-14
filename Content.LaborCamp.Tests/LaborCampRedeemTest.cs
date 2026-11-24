// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Numerics;
using Content.IntegrationTests;
using Content.Server.LaborCamp;
using Content.Shared.Access.Components;
using Content.Shared.Access;
using Content.Shared.LaborCamp;
using Content.Shared.LaborCamp.Components;
using Content.Shared.Security.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.LaborCamp;

[TestFixture]
public sealed class LaborCampRedeemTest
{
    private static readonly string[] Prototypes =
    {
        LaborCampPrototypes.PrisonerId,
        LaborCampPrototypes.Locker,
        LaborCampPrototypes.WallLocker,
        LaborCampPrototypes.Redemption,
        LaborCampPrototypes.Console,
        LaborCampPrototypes.EnterGate,
        LaborCampPrototypes.LeaveGate,
        LaborCampPrototypes.ShuttleConsole,
        LaborCampPrototypes.ShuttleAirlock,
        LaborCampPrototypes.CampDock,
        LaborCampPrototypes.Rock,
        LaborCampPrototypes.Sign,
    };

    [Test]
    public async Task PointsReleaseSwapsGenpopAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();
        var protos = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var id in Prototypes)
                Assert.That(protos.HasIndex<EntityPrototype>(id), Is.True, $"missing prototype {id}");

            Assert.That(protos.HasIndex<AccessLevelPrototype>(LaborCampPrototypes.ShuttleAccess), Is.True);

            var map = entMan.EntitySysManager.GetEntitySystem<SharedMapSystem>();
            map.CreateMap(out var mapId);
            var coords = new MapCoordinates(Vector2.Zero, mapId);
            var labor = entMan.EntitySysManager.GetEntitySystem<LaborCampSystem>();

            var goal = entMan.SpawnEntity(LaborCampPrototypes.PrisonerId, coords);
            labor.ApplySentence(goal, 1, 0f);
            var ore = entMan.SpawnEntity("SteelOre1", coords);
            Assert.That(labor.TryAwardOre(goal, ore, out var awarded), Is.True);
            Assert.That(awarded, Is.GreaterThan(0));
            AssertServed(entMan, goal);

            var partial = entMan.SpawnEntity(LaborCampPrototypes.PrisonerId, coords);
            labor.ApplySentence(partial, 1_000_000, 10f);
            var moreOre = entMan.SpawnEntity("SteelOre1", coords);
            Assert.That(labor.TryAwardOre(partial, moreOre, out var partialAward), Is.True);
            Assert.That(partialAward, Is.GreaterThan(0));
            Assert.That(entMan.TryGetComponent(partial, out ExpireIdCardComponent? still), Is.True);
            Assert.That(still!.Expired, Is.False);
            Assert.That(entMan.GetComponent<LaborCampSentenceComponent>(partial).Points, Is.EqualTo(partialAward));

            var life = entMan.SpawnEntity(LaborCampPrototypes.PrisonerId, coords);
            labor.ApplySentence(life, 0, 0f);
            var kept = entMan.SpawnEntity("SteelOre1", coords);
            Assert.That(labor.TryAwardOre(life, kept, out _), Is.False);
            Assert.That(entMan.EntityExists(kept), Is.True);
            Assert.That(entMan.GetComponent<ExpireIdCardComponent>(life).Expired, Is.False);

            Assert.That(labor.TryAwardOre(goal, entMan.SpawnEntity("SteelOre1", coords), out _), Is.False);

            map.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScaffoldSpawnsCampMachines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IServerEntityManager>();

        await server.WaitAssertion(() =>
        {
            var map = entMan.EntitySysManager.GetEntitySystem<SharedMapSystem>();
            map.CreateMap(out var mapId);
            var grids = entMan.EntitySysManager.GetEntitySystem<LaborCampGridSystem>();
            Assert.That(grids.TrySpawnCamp(mapId, Vector2.Zero, out _, out _, out _), Is.True);

            foreach (var lockerId in new[] { LaborCampPrototypes.Locker, LaborCampPrototypes.WallLocker })
            {
                var locker = entMan.SpawnEntity(lockerId, new MapCoordinates(Vector2.Zero, mapId));
                Assert.That(entMan.TryGetComponent(locker, out GenpopLockerComponent? genpop), Is.True, lockerId);
                Assert.That((string)genpop!.IdCardProto, Is.EqualTo(LaborCampPrototypes.PrisonerId), lockerId);
            }

            Assert.That(Count<LaborCampRedemptionComponent>(entMan), Is.GreaterThan(0));
            Assert.That(Count<LaborCampConsoleComponent>(entMan), Is.GreaterThan(0));
            Assert.That(Count<LaborCampLockerComponent>(entMan), Is.GreaterThan(0));

            map.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertServed(IServerEntityManager entMan, EntityUid id)
    {
        Assert.That(entMan.TryGetComponent(id, out ExpireIdCardComponent? expire), Is.True);
        Assert.That(expire!.Expired, Is.True);
        Assert.That(entMan.TryGetComponent(id, out AccessComponent? access), Is.True);

        var leave = false;
        var shuttle = false;
        foreach (var tag in access!.Tags)
        {
            var text = tag.ToString();
            if (text == "GenpopLeave")
                leave = true;
            if (text == LaborCampPrototypes.ShuttleAccess)
                shuttle = true;
        }

        Assert.That(leave, Is.True, "served ID should have GenpopLeave");
        Assert.That(shuttle, Is.True, "served ID should have LaborCampShuttle");
    }

    private static int Count<T>(IServerEntityManager entMan) where T : Component
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<T>();
        while (query.MoveNext(out _, out _))
            count++;

        return count;
    }
}
