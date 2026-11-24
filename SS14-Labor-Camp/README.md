# SS14 Labor Camp

A drag-and-drop labor camp point system for Space Station 14. Security sentences a prisoner to time, points, or both. The prisoner is released when either goal finishes. Ore turned in at a redemption machine counts toward the point goal. Meeting the goal unlocks the genpop exit and a labor-camp shuttle, and security is notified.

This pack does not replace the official genpop timer. It parents the upstream prisoner locker, prisoner ID, and genpop gates, and adds a point goal beside them.

Version 1.1.0. The constant is `Content.Shared.LaborCamp.LaborCampVersion.Version`.

## Install

From this folder, into the root of a fork (the directory that contains `Content.Server`):

```sh
./install.sh /path/to/fork
```

```powershell
powershell -File .\install.ps1 -Target C:\path\to\fork
```

An optional second argument is a git ref of this pack (`./install.sh /path/to/fork v1.0.0`).

The script copies:

- `Content.LaborCamp.Shared`, `Content.LaborCamp.Server`, `Content.LaborCamp.Client`, and `Content.LaborCamp.Tests`
- `Resources/Prototypes/_LaborCamp`, `Resources/Locale/en-US/_LaborCamp`, `Resources/Textures/_LaborCamp`
- `Resources/ConfigPresets/LaborCamp`

It adds one `Import` to `Directory.Build.targets`. The C# is compiled into the fork's existing `Content.Shared`, `Content.Client`, and `Content.Server` assemblies. There is no extra DLL, so the launcher sandbox does not need a new assembly. Do not add these folders to the solution as project references.

Running the script again overwrites the pack files and does not add the import twice. A destination file that contains the text `LABORCAMP-LOCAL` is left in place, so a local adaptation survives a reinstall. Put that marker in a comment near the top of the file.

The fork needs the upstream genpop pieces: `GenpopIdCardComponent`, `GenpopLockerComponent`, and `GenpopSystem`. A fork without them fails the build with a message that names the missing file.

## Sentencing

Use a labor locker (`LockerPrisonerLabor` or `LockerWallPrisonerLabor`). Stock genpop lockers are unchanged.

| Minutes | Points | Result |
| --- | --- | --- |
| above 0 | 0 | Time only. The official genpop timer releases the prisoner. Ore is not accepted. |
| 0 | above 0 | Points only. The timer is marked permanent so it does not also release. Ore releases the prisoner. |
| both above 0 | | Released when either finishes. |
| 0 | 0 | Permanent labor stay. End Sentence Early, or clear the ID and sentence again. |

The maximum point goal is 10,000,000. The crime line is required and is at most 48 characters. The name uses the server's ID name length cvar.

Reset Sentence on the locker resets the timer only. It does not clear points. Clear the ID and sentence again to start the points over. End Sentence Early still expires the ID, so the exit and the shuttle unlock.

## Ore and the console

`LaborCampRedemption` takes ore from the active hand, or ore sitting in one container (an ore bag). Points are the ore's appraised credit price times `labor_camp.points_per_credit`, rounded, and at least 1 for any ore that has a price. Worthless ore is left in hand. After the goal is met, further ore is not consumed.

`LaborCampConsole` shows the name, crime, points, goal, remaining points, and the time left on the genpop timer. Wear the labor ID. Neither machine needs power.

## Gates and the shuttle

This pack does not invent a second access system. A fresh labor ID has the upstream `GenpopEnter` access. When the sentence is served, the official expire step replaces the access with `GenpopLeave` and `LaborCampShuttle`.

The camp and the prisoner shuttle are two grids. A released prisoner can open the shuttle dock and ride. They cannot enter the cockpit or fly the ship. The shuttle itself stays driveable: it has thrusters, a gyroscope, and a normal helm. The camp grid is static, has no shuttle component, and any helm left on it opens only for `Security`.

| ID | Use |
| --- | --- |
| `EnergyGateGenpopEnter` | Entry. Parents `TurnstileGenpopEnter` and adds `Airtight`. Access: `GenpopEnter` or `Security`. |
| `EnergyGateGenpopLeave` | Exit. Parents `TurnstileGenpopLeave` and adds `Airtight`. Access: `GenpopLeave` or `Security`. |
| `AirlockLaborCampDock` | Camp side of the dock. Access: `Security`. While the shuttle is docked, the docking system holds both doors open. |
| `AirlockLaborCampShuttle` | Shuttle side of the dock. Access: `LaborCampShuttle` or `Security`. |
| `AirlockLaborCampCockpit` | Door between the dock and the helm. Access: `Security` while `labor_camp.shuttle_locked_route` is true. |
| `ComputerShuttleLaborCamp` | Normal shuttle helm, behind the cockpit door. Access: `Security` while the cvar is true. Does not need power. |
| `ThrusterLaborCamp` | Shuttle thruster. Fixed in place. |
| `GyroscopeLaborCamp` | Shuttle gyroscope. Fixed in place. |
| `WallLaborCamp` | Shuttle wall. Not meant to be taken apart. |
| `LaborCampRock` | Mineable rock. Regrows with a new ore roll. |
| `SignLaborCamp` | A wall sign. |

`LaborCampShuttle` lets a served sentence use the dock airlock. It does not open the cockpit or the helm while `labor_camp.shuttle_locked_route` is true (the default). Set that cvar to false and the cockpit and helm also accept `LaborCampShuttle`. A helm built onto the shuttle later gets the same access. The camp cannot be flown.

Frontier and HardLight do not ship a separate energy-barrier entity. The energy-gate IDs are the upstream genpop turnstiles, so a mapped camp is airtight enough for the scaffold room. The original `TurnstileGenpopEnter` and `TurnstileGenpopLeave` prototypes are not modified.

## Prototype IDs

Entities:

- `PrisonerIDCardLabor`
- `LockerPrisonerLabor`
- `LockerWallPrisonerLabor`
- `LaborCampRedemption`
- `LaborCampConsole`
- `EnergyGateGenpopEnter`
- `EnergyGateGenpopLeave`
- `ComputerShuttleLaborCamp`
- `AirlockLaborCampShuttle`
- `AirlockLaborCampCockpit`
- `AirlockLaborCampDock`
- `DoorElectronicsLaborCampShuttle`
- `DoorElectronicsLaborCampCockpit`
- `DoorElectronicsLaborCampDock`
- `ThrusterLaborCamp`
- `GyroscopeLaborCamp`
- `WallLaborCamp`
- `LaborCampRock`
- `SignLaborCamp`

Access level: `LaborCampShuttle` (`id-card-access-level-labor-camp-shuttle`).

No new radio channel. On Frontier the labor ID inherits the prisoner ID's expire radio, which is `Nfsd`, and the pack does not send a second message. Forks without that radio field, including HardLight, get one message on the first channel that exists among `labor_camp.radio_channel`, `Nfsd`, `Security`, and `Common`. If the inherited channel prototype is missing, the stock expire call fails its radio send and the pack uses that same fallback.

## Config

See `Resources/ConfigPresets/LaborCamp/labor_camp.toml.example`. The example is not loaded by itself. Paste the block into the server config.

| CVar | Default | Meaning |
| --- | --- | --- |
| `labor_camp.enabled` | true | Point goals, redemption, and the console. A time sentence still uses genpop when this is false. |
| `labor_camp.spawn` | false | At round start, spawn a camp on the default map. Leave this off unless you want that grid. |
| `labor_camp.grid_path` | empty | A camp grid file to load, for example `/Maps/LaborCamp.yml`. A missing file falls back to the scaffold. |
| `labor_camp.shuttle_grid_path` | empty | A shuttle grid file, loaded and docked to the camp. Empty or missing uses the scaffold shuttle. |
| `labor_camp.rock_regrow_minutes` | 12 | Minutes between mine refills. `0` turns regrowth off. Occupied tiles are skipped. |
| `labor_camp.shuttle_locked_route` | true | Only `Security` can open the cockpit and the helm. The shuttle still flies. `false` also lets a served sentence pilot it. |
| `labor_camp.points_per_credit` | 1 | Multiplier on the ore's appraised price. |
| `labor_camp.radio_channel` | `Nfsd` | First choice for the fallback radio. Ignored when the ID's own expire radio channel exists. |
| `labor_camp.spawn_offset_x` | 2500 | World offset from the default map origin. |
| `labor_camp.spawn_offset_y` | 2500 | World offset from the default map origin. |

Mapping does not require the spawn cvar. Place the prototypes on any grid.

An admin with the mapping flag can run `laborcamp` to spawn the camp and the shuttle beside themselves, or `laborcamp x y` for an explicit offset. With no player attached, `laborcamp` uses the configured offset on the default map. `laborcamp_refill` puts a fresh rock on every empty recorded mine tile.

The camp scaffold is a starter room: walls, the two gates, the camp dock, a locker, the redemption machine, the points console, a sign, lights, an oxygen miner, and a row of `LaborCampRock`. It is not a shuttle. The shuttle scaffold is a separate small grid. The dock airlock is on the north edge. A wall and `AirlockLaborCampCockpit` sit on the next row, so the dock cannot be walked around. The helm, gyroscope, and oxygen miner are south of that door. Two thrusters sit in the east and west walls with their nozzles facing out. Docking uses the same alignment as the shipyard: the named airlocks are paired, the shuttle is moved so those ports meet, and the ports are welded. If that pair cannot align, any other free Airlock dock is tried. If nothing fits, the shuttle stays parked beside the camp and is not docked.

Security can fly the shuttle anywhere with the normal helm, including FTL. A served sentence can board through the dock and cannot open the cockpit or the helm. The cockpit door, helm, thrusters, and walls are not unanchorable, not RCD-deconstructable, and have no deconstruction step. A new helm parented to the shuttle gets the same access. That still leaves a few ways past it: set `labor_camp.shuttle_locked_route` to false, use a Security ID, emag the cockpit door (the helm access check still applies), or have an admin remove the access. Blowing the door open does not grant helm access.

The labor lockers do not set the genpop ID field in YAML. Frontier calls that field `idCardProto` and HardLight requires `IdCardProto`. On startup the pack sets the C# property to `PrisonerIDCardLabor`, including on the wall locker, which has no upstream genpop parent. The name box uses HardLight's `IdCardConsoleComponent.MaxFullNameLength` when that constant exists, and Frontier's `ic.id_job_length` cvar otherwise.

## License

Code, prototypes, and locale are AGPL-3.0-or-later. See `LICENSE`.

The sign texture in `Resources/Textures/_LaborCamp/sign.rsi` is CC0-1.0. See the `LICENSE.txt` next to it.
