// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.LaborCamp;
using NUnit.Framework;

namespace Content.Tests.LaborCamp;

[TestFixture]
[Parallelizable]
public sealed class LaborCampPointsTest
{
    [Test]
    public void PermanentStayNeedsNeitherGoal()
    {
        Assert.That(LaborCampPoints.IsPermanent(0f, 0), Is.True);
        Assert.That(LaborCampPoints.IsPermanent(-1f, 0), Is.True);
        Assert.That(LaborCampPoints.IsPermanent(0f, -5), Is.True);
        Assert.That(LaborCampPoints.IsPermanent(5f, 0), Is.False);
        Assert.That(LaborCampPoints.IsPermanent(0f, 10), Is.False);
        Assert.That(LaborCampPoints.IsPermanent(5f, 10), Is.False);
        Assert.That(LaborCampPoints.IsPermanent(float.NaN, 0), Is.True);
    }

    [Test]
    public void EitherGoalReleases()
    {
        Assert.That(LaborCampPoints.ShouldRelease(false, 0, 10), Is.False);
        Assert.That(LaborCampPoints.ShouldRelease(false, 9, 10), Is.False);
        Assert.That(LaborCampPoints.ShouldRelease(false, 10, 10), Is.True);
        Assert.That(LaborCampPoints.ShouldRelease(false, 11, 10), Is.True);
        Assert.That(LaborCampPoints.ShouldRelease(true, 0, 10), Is.True);
        Assert.That(LaborCampPoints.ShouldRelease(true, 0, 0), Is.True);
        Assert.That(LaborCampPoints.ShouldRelease(false, 100, 0), Is.False);
        Assert.That(LaborCampPoints.ShouldRelease(false, 0, 0), Is.False);
    }

    [Test]
    public void RemainingClampsAtZero()
    {
        Assert.That(LaborCampPoints.Remaining(0, 10), Is.EqualTo(10));
        Assert.That(LaborCampPoints.Remaining(4, 10), Is.EqualTo(6));
        Assert.That(LaborCampPoints.Remaining(10, 10), Is.EqualTo(0));
        Assert.That(LaborCampPoints.Remaining(12, 10), Is.EqualTo(0));
        Assert.That(LaborCampPoints.Remaining(50, 0), Is.EqualTo(0));
    }

    [Test]
    public void OreValueBecomesPoints()
    {
        Assert.That(LaborCampPoints.PointsForValue(0, 1), Is.EqualTo(0));
        Assert.That(LaborCampPoints.PointsForValue(-3, 1), Is.EqualTo(0));
        Assert.That(LaborCampPoints.PointsForValue(5, 0), Is.EqualTo(0));
        Assert.That(LaborCampPoints.PointsForValue(5, -1), Is.EqualTo(0));
        Assert.That(LaborCampPoints.PointsForValue(double.NaN, 1), Is.EqualTo(0));
        Assert.That(LaborCampPoints.PointsForValue(0.2, 1), Is.EqualTo(1));
        Assert.That(LaborCampPoints.PointsForValue(5, 1), Is.EqualTo(5));
        Assert.That(LaborCampPoints.PointsForValue(5.4, 1), Is.EqualTo(5));
        Assert.That(LaborCampPoints.PointsForValue(5.5, 1), Is.EqualTo(6));
        Assert.That(LaborCampPoints.PointsForValue(5, 2), Is.EqualTo(10));
        Assert.That(LaborCampPoints.PointsForValue(1e20, 1), Is.EqualTo(int.MaxValue));
        Assert.That(LaborCampPoints.PointsForValue(double.PositiveInfinity, 1), Is.EqualTo(int.MaxValue));
    }
}
