// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.LaborCamp;

[AdminCommand(AdminFlags.Mapping)]
public sealed class LaborCampSpawnCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "laborcamp";
    public string Description => "Spawn a labor camp and its prisoner shuttle, docked. With no arguments, they appear beside you, or at labor_camp.spawn_offset on the default map.";
    public string Help => "laborcamp [x y]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0 && args.Length != 2)
        {
            shell.WriteError(Loc.GetString("labor-camp-cmd-usage"));
            return;
        }

        if (!_entities.EntitySysManager.TryGetEntitySystem<LaborCampGridSystem>(out var grids))
        {
            shell.WriteError(Loc.GetString("labor-camp-cmd-failed"));
            return;
        }

        MapId mapId;
        Vector2 position;
        var player = shell.Player?.AttachedEntity;

        if (args.Length == 2)
        {
            if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                shell.WriteError(Loc.GetString("labor-camp-cmd-usage"));
                return;
            }

            if (player is { } attached && _entities.TryGetComponent<TransformComponent>(attached, out var xform) && xform.MapID != MapId.Nullspace)
                mapId = xform.MapID;
            else if (_entities.System<GameTicker>().DefaultMap != MapId.Nullspace)
                mapId = _entities.System<GameTicker>().DefaultMap;
            else
            {
                shell.WriteError(Loc.GetString("labor-camp-cmd-no-map"));
                return;
            }

            position = new Vector2(x, y);
        }
        else if (player is { } attached
                 && _entities.TryGetComponent<TransformComponent>(attached, out var playerXform)
                 && playerXform.MapID != MapId.Nullspace)
        {
            mapId = playerXform.MapID;
            var world = _entities.System<SharedTransformSystem>().GetWorldPosition(attached);
            position = world + new Vector2(40f, 0f);
        }
        else
        {
            mapId = _entities.System<GameTicker>().DefaultMap;
            position = grids.ConfiguredOffset();
        }

        if (!grids.TrySpawnCamp(mapId, position, out _, out _, out var docked))
        {
            shell.WriteError(Loc.GetString("labor-camp-cmd-failed"));
            return;
        }

        shell.WriteLine(Loc.GetString("labor-camp-cmd-spawned", ("x", position.X), ("y", position.Y), ("map", mapId)));
        if (!docked)
            shell.WriteError(Loc.GetString("labor-camp-cmd-undocked"));
    }
}
