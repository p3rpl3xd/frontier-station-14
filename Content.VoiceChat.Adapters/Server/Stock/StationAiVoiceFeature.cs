// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
#if VOICECHAT_SERVER_COMPONENTS
using Content.Server.SurveillanceCamera;
#else
using Content.Shared.SurveillanceCamera.Components;
#endif
using Content.VoiceChat.Server;
using Content.VoiceChat.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.VoiceChat.Server;

public sealed partial class StationAiVoiceFeature : EntitySystem, IVoiceFeature
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStationAiSystem _stationAi = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private VoiceFeatures _features = default!;

    private readonly HashSet<Entity<StationAiVisionComponent>> _cameras = new();

    public override void Initialize()
    {
        base.Initialize();
        _features.Add(this);
    }

    public void Contribute(VoiceContribution contribution)
    {
        if (contribution.Ghost || !HasComp<StationAiHeldComponent>(contribution.SpeakerUid))
            return;

        contribution.SuppressDirect = true;
        if (!_stationAi.TryGetCore(contribution.SpeakerUid, out var core) ||
            core.Comp is not { Remote: true, RemoteEntity: { } eye })
        {
            return;
        }

        if (FindCamera(eye, contribution.DirectRange) is not { } camera)
            return;

        contribution.SuppressDirect = false;
        contribution.DirectEmitter = camera;
        contribution.DirectRoute = VoiceRoute.Camera;
    }

    public void AdjustName(EntityUid speaker, ref string name)
    {
        if (HasComp<StationAiHeldComponent>(speaker) && name == Prototype(speaker)?.Name)
            name = Loc.GetString("job-name-station-ai");
    }

    public bool TrySecondaryListener(EntityUid listener, out MapCoordinates secondary)
    {
        if (!HasComp<StationAiHeldComponent>(listener))
        {
            secondary = default;
            return false;
        }

        secondary = _transform.GetMapCoordinates(listener);
        return secondary.MapId != MapId.Nullspace;
    }

    public bool AllowsContainer(EntityUid speaker, EntityUid container)
    {
        return HasComp<StationAiHolderComponent>(container);
    }

    private EntityUid? FindCamera(EntityUid eye, float range)
    {
        var origin = _transform.GetMapCoordinates(eye);
        if (origin.MapId == MapId.Nullspace)
            return null;

        _cameras.Clear();
        _lookup.GetEntitiesInRange(origin, range, _cameras);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var camera in _cameras)
        {
            if (!camera.Comp.Enabled ||
                !TryComp<SurveillanceCameraComponent>(camera, out var surveillance) ||
                !surveillance.Active ||
                camera.Comp.NeedsPower && !_power.IsPowered(camera))
            {
                continue;
            }

            var xform = Transform(camera);
            if (camera.Comp.NeedsAnchoring && !xform.Anchored)
                continue;

            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(xform), origin.Position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = camera;
        }

        return best;
    }
}
