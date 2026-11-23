// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Access.Components;
using Content.Shared.Examine;
using Content.Shared.LaborCamp.Components;
using Content.Shared.Security.Components;

namespace Content.Shared.LaborCamp.Systems;

/// <summary>
/// Examine text for a labor sentence. The official genpop examine still describes the timer.
/// </summary>
public sealed class SharedLaborCampSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LaborCampSentenceComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<LaborCampSentenceComponent> ent, ref ExaminedEvent args)
    {
        var goal = ent.Comp.PointGoal;
        var points = ent.Comp.Points;
        var hasTime = TryComp<GenpopIdCardComponent>(ent, out var genpop) && genpop.SentenceDuration > TimeSpan.Zero;
        var served = TryComp<ExpireIdCardComponent>(ent, out var expire) && expire.Expired;

        if (served)
        {
            args.PushText(Loc.GetString("labor-camp-examine-served"));
            if (LaborCampPoints.HasPointGoal(goal))
            {
                args.PushText(Loc.GetString("labor-camp-examine-points",
                    ("points", points),
                    ("goal", goal),
                    ("remaining", LaborCampPoints.Remaining(points, goal))));
            }

            return;
        }

        if (LaborCampPoints.IsPermanent(hasTime ? 1f : 0f, goal))
        {
            args.PushText(Loc.GetString("labor-camp-examine-permanent"));
            return;
        }

        if (!LaborCampPoints.HasPointGoal(goal))
        {
            args.PushText(Loc.GetString("labor-camp-examine-time-only"));
            return;
        }

        args.PushText(Loc.GetString("labor-camp-examine-points",
            ("points", points),
            ("goal", goal),
            ("remaining", LaborCampPoints.Remaining(points, goal))));
    }
}
