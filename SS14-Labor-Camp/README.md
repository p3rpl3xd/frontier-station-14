# SS14 Labor Camp

A drag-and-drop labor camp point system for Space Station 14. Security sentences a prisoner to time, points, or both. The prisoner is released when either goal finishes. Ore turned in at a redemption machine counts toward the point goal. Meeting the goal unlocks the genpop exit and a labor-camp shuttle, and security is notified.

This pack does not replace the official genpop timer. It parents the upstream prisoner locker, prisoner ID, and genpop gates, and adds a point goal beside them.

Version 1.0.0. The constant is `Content.Shared.LaborCamp.LaborCampVersion.Version`.

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

| ID | Use |
| --- | --- |
| `EnergyGateGenpopEnter` | Entry. Parents `TurnstileGenpopEnter` and adds `Airtight`. Access: `GenpopEnter` or `Security`. |
| `EnergyGateGenpopLeave` | Exit. Parents `TurnstileGenpopLeave` and adds `Airtight`. Access: `GenpopLeave` or `Security`. |
| `AirlockLaborCampShuttle` | Dock door. Access: `LaborCampShuttle` or `Security`. |
| `ComputerShuttleLaborCamp` | Shuttle console. Same access. Does not need power. |
| `SignLaborCamp` | A wall sign. |

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
- `SignLaborCamp`

Access level: `LaborCampShuttle` (`id-card-access-level-labor-camp-shuttle`).

No new radio channel. On Frontier the labor ID inherits the prisoner ID's expire radio, which is `Nfsd`, and the pack does not send a second message. Forks without that radio field, including HardLight, get one message on the first channel that exists among `labor_camp.radio_channel`, `Nfsd`, `Security`, and `Common`. If the inherited channel prototype is missing, the stock expire call fails its radio send and the pack uses that same fallback.

## Config

See `Resources/ConfigPresets/LaborCamp/labor_camp.toml.example`. The example is not loaded by itself. Paste the block into the server config.

| CVar | Default | Meaning |
| --- | --- | --- |
| `labor_camp.enabled` | true | Point goals, redemption, and the console. A time sentence still uses genpop when this is false. |
| `labor_camp.spawn` | false | At round start, spawn a camp on the default map. Leave this off unless you want that grid. |
| `labor_camp.grid_path` | empty | A grid file to load, for example `/Maps/LaborCamp.yml`. A missing file falls back to the scaffold. |
| `labor_camp.points_per_credit` | 1 | Multiplier on the ore's appraised price. |
| `labor_camp.radio_channel` | `Nfsd` | First choice for the fallback radio. Ignored when the ID's own expire radio channel exists. |
| `labor_camp.spawn_offset_x` | 2500 | World offset from the default map origin. |
| `labor_camp.spawn_offset_y` | 2500 | World offset from the default map origin. |

Mapping does not require the spawn cvar. Place the prototypes on any grid.

An admin with the mapping flag can run `laborcamp` to spawn a camp beside themselves, or `laborcamp x y` for an explicit offset. With no player attached, `laborcamp` uses the configured offset on the default map.

The scaffold is a starter room: walls, the two gates, a shuttle airlock, a locker, the redemption machine, the console, a shuttle console, a sign, lights, an oxygen miner, a sample of ore, and a thruster and gyroscope that do not require power. It is not a finished ship. The thruster direction is the stock sprite direction.

## License

Code, prototypes, and locale are AGPL-3.0-or-later. See `LICENSE`.

The sign texture in `Resources/Textures/_LaborCamp/sign.rsi` is CC0-1.0. See the `LICENSE.txt` next to it.
