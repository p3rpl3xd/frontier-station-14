# Changelog

## 1.1.0

- The prisoner shuttle is a second grid, loaded from `labor_camp.shuttle_grid_path` or built as a small scaffold, and docked to the camp.
- A served sentence can board the shuttle dock. The cockpit door and the helm open only for Security while `labor_camp.shuttle_locked_route` is true. The shuttle still flies. The camp grid is static and cannot be piloted.
- Mine rocks are recorded at spawn and grow back on empty tiles. `labor_camp.rock_regrow_minutes` defaults to 12, and `0` turns it off. `laborcamp_refill` forces a refill.
- Labor lockers assign `PrisonerIDCardLabor` in code so the pack loads on both Frontier (`idCardProto`) and HardLight (`IdCardProto`).

## 1.0.0

- Labor sentences of time, points, or both, on top of the official genpop timer.
- Ore redemption, a points console, genpop entry and exit gates, and shuttle access that unlocks when either goal finishes.
- A config-driven camp spawn and the `laborcamp` mapping command, with a small scaffold grid when no map file is set.
- Install scripts that copy the pack into a fork and leave files marked `LABORCAMP-LOCAL` alone.
