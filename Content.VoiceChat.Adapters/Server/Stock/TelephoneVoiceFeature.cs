// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Telephone;
using Content.Shared.Holopad;
using Content.Shared.Interaction;
using Content.Shared.Telephone;
using Content.VoiceChat.Server;
using Content.VoiceChat.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
namespace Content.VoiceChat.Server;

public sealed partial class TelephoneVoiceFeature : EntitySystem, IVoiceFeature
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private TelephoneSystem _telephone = default!;
    [Dependency] private VoiceFeatures _features = default!;

    public override void Initialize()
    {
        base.Initialize();
        _features.Add(this);
    }

    public void Contribute(VoiceContribution contribution)
    {
        if (contribution.Ghost)
            return;

        var speaker = contribution.SpeakerUid;
        var speakerXform = Transform(speaker);
        var origin = _transform.GetMapCoordinates(speaker, speakerXform);
        if (origin.MapId == MapId.Nullspace)
            return;

        var query = EntityQueryEnumerator<TelephoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var telephone, out var xform))
        {
            if (telephone.LinkedTelephones.Count == 0 || telephone.Muted || telephone.CurrentState != TelephoneState.InCall || xform.MapID != origin.MapId)
                continue;

            var entity = new Entity<TelephoneComponent>(uid, telephone);
            if (!_telephone.IsTelephonePowered(entity))
                continue;

            var range = telephone.ListeningRange;
            if (Vector2.DistanceSquared(_transform.GetWorldPosition(xform), origin.Position) > range * range)
                continue;

            if (speakerXform.ParentUid != uid && !_interaction.InRangeUnobstructed(speaker, uid, range))
                continue;

            contribution.KeepAwake.Add(uid);

            foreach (var linked in telephone.LinkedTelephones)
            {
#if VOICECHAT_UID_TELEPHONE
                var linkedUid = linked;
                if (linkedUid == uid ||
                    TerminatingOrDeleted(linkedUid) ||
                    !TryComp<TelephoneComponent>(linkedUid, out var linkedPhone) ||
                    linkedPhone.CurrentState != TelephoneState.InCall ||
                    !_telephone.IsTelephonePowered((linkedUid, linkedPhone)))
                {
                    continue;
                }

                var emitter = RelayEmitter(linkedUid, linkedPhone);
#else
                if (linked.Owner == uid || TerminatingOrDeleted(linked) || linked.Comp.CurrentState != TelephoneState.InCall || !_telephone.IsTelephonePowered(linked))
                    continue;

                var linkedPhone = linked.Comp;
                var emitter = RelayEmitter(linked);
#endif
                if (TerminatingOrDeleted(emitter))
                    continue;

                var emitterOrigin = _transform.GetMapCoordinates(emitter);
                if (emitterOrigin.MapId == MapId.Nullspace)
                    continue;

                var relayRange = linkedPhone.SpeakerVolume == TelephoneVolume.Speak ? contribution.DirectRange : 3f;
                var relayRoute = HasComp<HolopadComponent>(
#if VOICECHAT_UID_TELEPHONE
                    linkedUid
#else
                    linked
#endif
                    ) ? VoiceRoute.Holopad : VoiceRoute.Telephone;
                contribution.Relays.Add(new VoiceRelay(emitter, emitterOrigin, relayRange, relayRoute));
                contribution.KeepAwake.Add(
#if VOICECHAT_UID_TELEPHONE
                    linkedUid
#else
                    linked.Owner
#endif
                    );
            }
        }
    }

    public void KeepAlive(EntityUid entity)
    {
        if (!TryComp<TelephoneComponent>(entity, out var telephone) || telephone.CurrentState != TelephoneState.InCall)
            return;

        _telephone.KeepCallAlive((entity, telephone));
    }

#if VOICECHAT_UID_TELEPHONE
    private EntityUid RelayEmitter(EntityUid telephone, TelephoneComponent component)
    {
        if (TryComp<HolopadComponent>(telephone, out var holopad) &&
            holopad.Hologram is { } hologram &&
            !TerminatingOrDeleted(hologram))
        {
            return hologram;
        }

        if (component.Speaker is { } speaker && !TerminatingOrDeleted(speaker))
            return speaker;

        return telephone;
    }
#else
    private EntityUid RelayEmitter(Entity<TelephoneComponent> telephone)
    {
        if (TryComp<HolopadComponent>(telephone, out var holopad) &&
            holopad.Hologram is { } hologram &&
            !TerminatingOrDeleted(hologram))
        {
            return hologram;
        }

        if (telephone.Comp.Speaker?.Owner is { } speaker && !TerminatingOrDeleted(speaker))
            return speaker;

        return telephone.Owner;
    }
#endif
}
