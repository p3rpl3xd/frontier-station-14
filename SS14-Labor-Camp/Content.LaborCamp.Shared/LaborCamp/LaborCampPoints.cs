// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.LaborCamp;

/// <summary>
/// Pure sentencing rules for the labor camp. Time is the upstream genpop timer.
/// Points are this pack. A stay ends when either goal finishes. Neither goal is permanent.
/// </summary>
public static class LaborCampPoints
{
    public const int MaxGoal = 10_000_000;

    /// <summary>
    /// Prisoner name cap. Frontier's <c>ic.id_job_length</c> default and HardLight's
    /// <c>IdCardConsoleComponent.MaxFullNameLength</c> are both 30.
    /// The client uses this constant. The Steam launcher sandbox forbids the reflection
    /// that would read whichever field the host actually has.
    /// </summary>
    public const int MaxPrisonerNameLength = 30;

    public static bool HasPointGoal(int pointGoal) => pointGoal > 0;

    public static bool HasTimeGoal(float minutes) => minutes > 0f && !float.IsNaN(minutes) && !float.IsInfinity(minutes);

    /// <summary>
    /// No time and no points. Security has to end the stay by hand.
    /// </summary>
    public static bool IsPermanent(float minutes, int pointGoal) => !HasTimeGoal(minutes) && !HasPointGoal(pointGoal);

    public static bool GoalMet(int points, int pointGoal) => HasPointGoal(pointGoal) && points >= pointGoal;

    /// <summary>
    /// Released when the timer finishes or the point goal is met. A goal of zero is not a goal.
    /// </summary>
    public static bool ShouldRelease(bool timeFinished, int points, int pointGoal)
        => timeFinished || GoalMet(points, pointGoal);

    public static int Remaining(int points, int pointGoal)
    {
        if (!HasPointGoal(pointGoal))
            return 0;

        var left = (long)pointGoal - points;
        if (left <= 0)
            return 0;

        return left > int.MaxValue ? int.MaxValue : (int)left;
    }

    /// <summary>
    /// Turns an appraised ore value into points. Worthless ore is zero.
    /// Any positive value is at least one point.
    /// </summary>
    public static int PointsForValue(double price, double pointsPerCredit)
    {
        if (price <= 0 || pointsPerCredit <= 0 || double.IsNaN(price) || double.IsNaN(pointsPerCredit))
            return 0;

        var scaled = price * pointsPerCredit;
        if (double.IsNaN(scaled) || double.IsNegativeInfinity(scaled))
            return 0;

        if (double.IsPositiveInfinity(scaled) || scaled >= int.MaxValue)
            return int.MaxValue;

        var rounded = Math.Round(scaled, MidpointRounding.AwayFromZero);
        if (rounded >= int.MaxValue)
            return int.MaxValue;

        var points = (int)rounded;
        return Math.Max(points, 1);
    }
}
