// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.LaborCamp;

[AdminCommand(AdminFlags.Mapping)]
public sealed class LaborCampRefillCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "laborcamp_refill";
    public string Description => "Put a fresh ore rock back on every empty recorded labor camp mine tile.";
    public string Help => "laborcamp_refill";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Loc.GetString("labor-camp-cmd-refill-usage"));
            return;
        }

        if (!_entities.EntitySysManager.TryGetEntitySystem<LaborCampGridSystem>(out var grids))
        {
            shell.WriteError(Loc.GetString("labor-camp-cmd-failed"));
            return;
        }

        var count = grids.RefillRocks();
        shell.WriteLine(Loc.GetString("labor-camp-cmd-refilled", ("count", count)));
    }
}
