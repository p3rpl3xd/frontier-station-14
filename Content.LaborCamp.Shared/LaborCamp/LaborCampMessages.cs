// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared.LaborCamp;

/// <summary>
/// Sent before the stock genpop configure message so the server can remember the point goal.
/// The official <c>GenpopLockerIdConfiguredMessage</c> still creates the ID and the timer.
/// </summary>
[Serializable, NetSerializable]
public sealed class LaborCampLockerPointsMessage : BoundUserInterfaceMessage
{
    public int Points;

    public LaborCampLockerPointsMessage(int points)
    {
        Points = points;
    }
}

[Serializable, NetSerializable]
public enum LaborCampConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class LaborCampConsoleRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class LaborCampConsoleState : BoundUserInterfaceState
{
    public bool HasId;
    public string Name = string.Empty;
    public string Crime = string.Empty;
    public int Points;
    public int PointGoal;
    public bool HasPointGoal;
    public bool Permanent;
    public bool Served;
    public bool HasTimeGoal;
    public int TimeRemainingSeconds;
}
