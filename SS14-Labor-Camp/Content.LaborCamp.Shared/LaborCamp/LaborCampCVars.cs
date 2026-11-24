// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.LaborCamp;

/// <summary>
/// Server config for the labor camp pack. None of these edit a map.
/// </summary>
[CVarDefs]
public sealed class LaborCampCVars
{
    /// <summary>
    /// Master switch for point sentencing and ore redemption.
    /// Time sentences on the genpop locker still work when this is false.
    /// </summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("labor_camp.enabled", true, CVar.SERVER);

    /// <summary>
    /// Spawn the scaffold camp grid when a round starts.
    /// Off by default so installing the pack does not change the map.
    /// </summary>
    public static readonly CVarDef<bool> Spawn =
        CVarDef.Create("labor_camp.spawn", false, CVar.SERVER);

    /// <summary>
    /// Optional grid file (a ResPath such as /Maps/_LaborCamp/labor_camp.yml).
    /// Empty uses the built-in scaffold. A missing file falls back to the scaffold.
    /// </summary>
    public static readonly CVarDef<string> GridPath =
        CVarDef.Create("labor_camp.grid_path", "", CVar.SERVER);

    /// <summary>
    /// Optional shuttle grid file (a ResPath such as /Maps/_LaborCamp/labor_shuttle.yml).
    /// Empty, missing, or unloadable uses the built-in scaffold shuttle, docked to the camp.
    /// </summary>
    public static readonly CVarDef<string> ShuttleGridPath =
        CVarDef.Create("labor_camp.shuttle_grid_path", "", CVar.SERVER);

    /// <summary>
    /// Minutes between mine refills. Missing rocks grow back with a new ore roll.
    /// 0 turns regrowth off. Occupied tiles are skipped.
    /// </summary>
    public static readonly CVarDef<float> RockRegrowMinutes =
        CVarDef.Create("labor_camp.rock_regrow_minutes", 12f, CVar.SERVER);

    /// <summary>
    /// When true, the prisoner shuttle can FTL only between the camp and the round's largest station grid.
    /// Thrusters and manual piloting do not move it. Set false to fly it like any other shuttle.
    /// LaborCampShuttle and Security can open the cockpit either way.
    /// </summary>
    public static readonly CVarDef<bool> ShuttleLockedRoute =
        CVarDef.Create("labor_camp.shuttle_locked_route", true, CVar.SERVER);

    /// <summary>
    /// Multiplier on the ore's appraised credit value. 1 means one credit is one point.
    /// </summary>
    public static readonly CVarDef<float> PointsPerCredit =
        CVarDef.Create("labor_camp.points_per_credit", 1f, CVar.SERVER);

    /// <summary>
    /// First radio channel to try when the ID itself does not already alert security.
    /// Missing channels fall through Nfsd, then Security, then Common.
    /// </summary>
    public static readonly CVarDef<string> RadioChannel =
        CVarDef.Create("labor_camp.radio_channel", "Nfsd", CVar.SERVER);

    public static readonly CVarDef<float> SpawnOffsetX =
        CVarDef.Create("labor_camp.spawn_offset_x", 2500f, CVar.SERVER);

    public static readonly CVarDef<float> SpawnOffsetY =
        CVarDef.Create("labor_camp.spawn_offset_y", 2500f, CVar.SERVER);
}
