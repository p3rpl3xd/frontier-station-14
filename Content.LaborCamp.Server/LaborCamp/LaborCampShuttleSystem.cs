// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using Content.Server.Construction;
using Content.Server.Shuttles.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.LaborCamp;
using Content.Shared.LaborCamp.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tiles;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.LaborCamp;

/// <summary>
/// The prisoner shuttle keeps its thrusters and a normal helm.
/// While <see cref="LaborCampCVars.ShuttleLockedRoute"/> is on, only Security can open the cockpit or the helm.
/// A served sentence can still use the dock airlock. Set the cvar false and the cockpit accepts that access too.
/// </summary>
public sealed class LaborCampShuttleSystem : EntitySystem
{
    private static readonly ProtoId<AccessLevelPrototype> Security = LaborCampPrototypes.SecurityAccess;
    private static readonly ProtoId<AccessLevelPrototype> ShuttleAccess = LaborCampPrototypes.ShuttleAccess;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private bool _securityCockpit = true;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, LaborCampCVars.ShuttleLockedRoute, OnCvarChanged, true);

        SubscribeLocalEvent<ShuttleConsoleComponent, EntParentChangedMessage>(OnConsoleMoved);
        SubscribeLocalEvent<LaborCampCockpitDoorComponent, EntParentChangedMessage>(OnCockpitMoved);
        SubscribeLocalEvent<AnchorableComponent, UnanchorAttemptEvent>(OnUnanchor);
        SubscribeLocalEvent<LaborCampShuttleComponent, FloorTileAttemptEvent>(OnFloor);
        SubscribeLocalEvent<LaborCampShuttlePartComponent, GetVerbsEvent<Verb>>(OnVerbs, after: new[] { typeof(ConstructionSystem) });
    }

    /// <summary>
    /// Called once the shuttle grid exists. Locks the cockpit and keeps the hull from being taken apart.
    /// </summary>
    public void Attach(EntityUid shuttle)
    {
        EnsureComp<LaborCampShuttleComponent>(shuttle);
        ProtectGrid(shuttle);
        Seal(shuttle);
        ApplyAccess(shuttle);
    }

    private void OnCvarChanged(bool value)
    {
        _securityCockpit = value;
        var query = EntityQueryEnumerator<LaborCampShuttleComponent>();
        while (query.MoveNext(out var uid, out _))
            ApplyAccess(uid);
    }

    private void OnConsoleMoved(Entity<ShuttleConsoleComponent> ent, ref EntParentChangedMessage args)
    {
        if (Transform(ent).GridUid is not { } grid || !HasComp<LaborCampShuttleComponent>(grid))
            return;

        LockReaders(ent);
        EnsureComp<ActivatableUIRequiresAccessComponent>(ent);
    }

    private void OnCockpitMoved(Entity<LaborCampCockpitDoorComponent> ent, ref EntParentChangedMessage args)
    {
        if (Transform(ent).GridUid is not { } grid || !HasComp<LaborCampShuttleComponent>(grid))
            return;

        LockReaders(ent);
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
                EnsureComp<ActivatableUIRequiresAccessComponent>(child);
        }
    }

    /// <summary>
    /// Writes the reader on the entity and on a door electronics board, which is the reader airlocks actually check.
    /// </summary>
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

    private void Write(EntityUid uid, AccessReaderComponent reader)
    {
        if (_securityCockpit)
        {
            _access.TrySetAccesses((uid, reader), new List<HashSet<ProtoId<AccessLevelPrototype>>>
            {
                new() { Security },
            });
            return;
        }

        _access.TrySetAccesses((uid, reader), new List<HashSet<ProtoId<AccessLevelPrototype>>>
        {
            new() { ShuttleAccess },
            new() { Security },
        });
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
