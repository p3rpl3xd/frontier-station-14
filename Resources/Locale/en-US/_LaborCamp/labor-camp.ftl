id-card-access-level-labor-camp-shuttle = Labor Camp Shuttle

ent-PrisonerIDCardLabor = labor camp prisoner ID
    .desc = A prisoner ID that can carry a labor point goal as well as a genpop timer.
ent-LockerPrisonerLabor = labor camp prisoner locker
    .desc = A genpop locker that sentences a prisoner to time, points, or both.
    .suffix = Labor
ent-LockerWallPrisonerLabor = labor camp prisoner wall locker
    .desc = A wall-mounted genpop locker that sentences a prisoner to time, points, or both.
    .suffix = Labor
ent-LaborCampRedemption = labor camp ore redemption
    .desc = Feed it ore. The ore's credit value is added to the prisoner's labor points, then the ore is consumed.
ent-LaborCampConsole = labor camp points console
    .desc = Shows a prisoner's labor points, the point goal, and the time left on the genpop timer.
ent-EnergyGateGenpopEnter = labor camp entry gate
    .desc = The genpop entry gate. Prisoner IDs with enter access can pass. Security can pass.
    .suffix = Genpop Enter
ent-EnergyGateGenpopLeave = labor camp exit gate
    .desc = The genpop exit gate. It opens when a sentence is served. Security can pass.
    .suffix = Genpop Leave
ent-ComputerShuttleLaborCamp = labor camp shuttle console
    .desc = Sends the prisoner shuttle to the labor camp or the main station. It does not pick a free destination.
    .suffix = Labor Camp
ent-ThrusterLaborCamp = labor camp thruster
    .desc = A thruster fixed to the prisoner shuttle. It does not push the ship off its route.
    .suffix = Labor Camp
ent-GyroscopeLaborCamp = labor camp gyroscope
    .desc = A gyroscope fixed to the prisoner shuttle. It does not turn the ship off its route.
    .suffix = Labor Camp
ent-WallLaborCamp = labor camp wall
    .desc = A wall of the prisoner shuttle. It is not meant to be taken apart.
    .suffix = Labor Camp
ent-AirlockLaborCampShuttle = labor camp shuttle airlock
    .desc = Dock airlock on the prisoner shuttle. Opens for a served sentence, or for security.
    .suffix = Labor Camp
ent-AirlockLaborCampDock = labor camp dock
    .desc = Camp side of the prisoner shuttle dock. The helm is on the shuttle.
    .suffix = Labor Camp
ent-LaborCampRock = labor camp rock
    .desc = A mineable rock in the labor camp. It grows back with a new ore roll.
ent-SignLaborCamp = labor camp sign
    .desc = A sign for a labor camp outpost.

labor-camp-grid-name = Labor Camp
labor-camp-shuttle-grid-name = Labor Camp Shuttle

labor-camp-id-expire = You have served your sentence. The exit gate and the shuttle are unlocked.
labor-camp-radio-served = {$name} has finished a labor sentence ({$crime}). The exit gate and shuttle are unlocked.

labor-camp-examine-points = Labor points: {$points} / {$goal} ({$remaining} remaining). The stay ends when this goal or the timer finishes.
labor-camp-examine-served = Labor sentence served.
labor-camp-examine-permanent = No time and no point goal. This is a permanent labor stay.
labor-camp-examine-time-only = No labor point goal. The genpop timer is the only way this stay ends.

labor-camp-locker-title = Labor sentence
labor-camp-locker-hint = Minutes are the genpop timer. Points are the labor goal. The prisoner is released when either finishes. Leave both at 0 for a permanent stay.
labor-camp-locker-name = Convict name
labor-camp-locker-minutes = Sentence minutes (0 for none)
labor-camp-locker-points = Point goal (0 for none)
labor-camp-locker-crime = Crime
labor-camp-locker-done = Done
labor-camp-crime-default = Theft
labor-camp-sentenced = Sentence set. Minutes: {$minutes}. Point goal: {$points}. Released when either finishes. Both zero is permanent.
labor-camp-disabled = Labor camp points are turned off. A time sentence still works.

labor-camp-redeem-no-id = No prisoner ID. Wear the labor ID.
labor-camp-redeem-no-goal = This ID has no point goal.
labor-camp-redeem-served = This sentence is already over.
labor-camp-redeem-not-ore = That is not ore.
labor-camp-redeem-worthless = That ore has no value.
labor-camp-redeem-awarded = Redeemed for {$points} points. Total {$total} / {$goal}, {$remaining} remaining.
labor-camp-redeem-released = Point goal met. The exit gate and shuttle access are unlocked.

labor-camp-console-title = Labor camp points
labor-camp-console-no-id = No prisoner ID. Wear the labor ID and refresh.
labor-camp-console-name = Prisoner: {$name}
labor-camp-console-crime = Crime: {$crime}
labor-camp-console-points = Points: {$points}
labor-camp-console-goal = Goal: {$goal}
labor-camp-console-remaining = Remaining: {$remaining}
labor-camp-console-no-goal = No point goal.
labor-camp-console-time = Time left: {$minutes}m {$seconds}s
labor-camp-console-no-time = No time sentence.
labor-camp-console-permanent = Permanent stay. Points are the only way out, if a point goal is set.
labor-camp-console-served = Sentence served. The exit gate and shuttle are unlocked.
labor-camp-console-refresh = Refresh

labor-camp-shuttle-denied = The cockpit is locked. Only security can fly this shuttle.

labor-camp-cmd-usage = Usage: laborcamp [x y]
labor-camp-cmd-failed = Could not spawn a labor camp.
labor-camp-cmd-no-map = No map to spawn the labor camp on.
labor-camp-cmd-spawned = Spawned a labor camp and its shuttle at {$x}, {$y} on map {$map}.
labor-camp-cmd-undocked = The shuttle spawned, but it did not dock. It was left beside the camp.
labor-camp-cmd-refill-usage = Usage: laborcamp_refill
labor-camp-cmd-refilled = Refilled {$count} labor camp rocks.
