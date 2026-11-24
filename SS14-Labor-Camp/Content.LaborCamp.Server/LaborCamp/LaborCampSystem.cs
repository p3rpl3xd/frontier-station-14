// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Reflection;
using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Systems;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.LaborCamp;
using Content.Shared.LaborCamp.Components;
using Content.Shared.Radio;
using Content.Shared.Security.Components;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.LaborCamp;

/// <summary>
/// Point sentences on top of the official genpop timer. Ore redemption, the points console,
/// and the security alert live here. The timer, the locker, and the leave-gate access swap stay upstream.
/// </summary>
public sealed class LaborCampSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> OreTag = LaborCampPrototypes.OreTag;
    private static readonly ProtoId<RadioChannelPrototype> NfsdChannel = "Nfsd";
    private static readonly ProtoId<RadioChannelPrototype> SecurityChannel = "Security";
    private static readonly ProtoId<RadioChannelPrototype> CommonChannel = "Common";

    [Dependency] private IAdminLogManager _admin = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private TagSystem _tags = default!;

    private bool _enabled = true;
    private float _pointsPerCredit = 1f;
    private string _radioChannel = "Nfsd";
    private float _notifyAccum;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LaborCampLockerComponent, ComponentStartup>(OnLockerStartup);
        SubscribeLocalEvent<LaborCampLockerComponent, LaborCampLockerPointsMessage>(OnPoints);
        SubscribeLocalEvent<LaborCampRedemptionComponent, InteractUsingEvent>(OnRedeem);
        SubscribeLocalEvent<LaborCampConsoleComponent, AfterActivatableUIOpenEvent>(OnConsoleOpen);
        SubscribeLocalEvent<LaborCampConsoleComponent, LaborCampConsoleRefreshMessage>(OnConsoleRefresh);

        Subs.CVar(_cfg, LaborCampCVars.Enabled, value => _enabled = value, true);
        Subs.CVar(_cfg, LaborCampCVars.PointsPerCredit, value => _pointsPerCredit = value, true);
        Subs.CVar(_cfg, LaborCampCVars.RadioChannel, value => _radioChannel = value, true);

        Log.Info($"Labor camp points {LaborCampVersion.Version} ready.");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Genpop already subscribes to its configure message, and a component event allows only one
        // subscriber. Watch for the ID it creates instead of subscribing again.
        var pending = EntityQueryEnumerator<LaborCampLockerComponent, GenpopLockerComponent>();
        while (pending.MoveNext(out var uid, out var labor, out var genpop))
        {
            if (!labor.HasPending)
                continue;

            labor.PendingSeconds += frameTime;
            if (genpop.LinkedId is { } id && id != EntityUid.Invalid && id != labor.PendingPreviousId)
            {
                var points = labor.PendingPoints;
                var actor = labor.PendingActor;
                labor.HasPending = false;
                var minutes = TryComp<GenpopIdCardComponent>(id, out var card)
                    ? (float)card.SentenceDuration.TotalMinutes
                    : 0f;
                ApplySentence(id, points, minutes);
                var sentenced = Loc.GetString("labor-camp-sentenced",
                    ("minutes", minutes),
                    ("points", Math.Clamp(points, 0, LaborCampPoints.MaxGoal)));
                if (actor != EntityUid.Invalid)
                    _popup.PopupEntity(sentenced, uid, actor);
                else
                    _popup.PopupEntity(sentenced, uid);
                continue;
            }

            if (labor.PendingSeconds > 2f)
                labor.HasPending = false;
        }

        // The official timer calls ExpireId on its own. This notices that and alerts security once.
        _notifyAccum += frameTime;
        if (_notifyAccum < 1f)
            return;

        _notifyAccum = 0f;
        var query = EntityQueryEnumerator<LaborCampSentenceComponent, ExpireIdCardComponent>();
        while (query.MoveNext(out var uid, out var sentence, out var expire))
        {
            if (!expire.Expired || sentence.SecurityNotified)
                continue;

            NotifySecurity((uid, sentence), expire);
        }
    }

    /// <summary>
    /// Records a point goal on an ID the genpop locker just created.
    /// </summary>
    public void ApplySentence(EntityUid id, int points, float minutes)
    {
        var sentence = EnsureComp<LaborCampSentenceComponent>(id);
        sentence.PointGoal = Math.Clamp(points, 0, LaborCampPoints.MaxGoal);
        sentence.Points = 0;
        sentence.SecurityNotified = false;
        Dirty(id, sentence);

        // The locker already does this. Doing it here covers the public helper and the command path.
        if (!LaborCampPoints.HasTimeGoal(minutes))
            _idCard.SetPermanent(id, true);

        _admin.Add(LogType.Action, LogImpact.Medium,
            $"Labor sentence on {ToPrettyString(id)}: {minutes} minutes, {sentence.PointGoal} points.");
    }

    /// <summary>
    /// Adds the appraised value of one ore entity to the ID. Deletes the ore when it is worth points.
    /// </summary>
    public bool TryAwardOre(EntityUid id, EntityUid ore, out int awarded)
    {
        awarded = 0;
        if (!_enabled || !TryComp<LaborCampSentenceComponent>(id, out var sentence))
            return false;

        if (TryComp<ExpireIdCardComponent>(id, out var expire) && expire.Expired)
            return false;

        if (!LaborCampPoints.HasPointGoal(sentence.PointGoal))
            return false;

        if (!_tags.HasTag(ore, OreTag))
            return false;

        awarded = LaborCampPoints.PointsForValue(_pricing.GetPrice(ore), _pointsPerCredit);
        if (awarded <= 0)
        {
            awarded = 0;
            return false;
        }

        var sum = (long)sentence.Points + awarded;
        sentence.Points = sum > int.MaxValue ? int.MaxValue : (int)sum;
        Dirty(id, sentence);
        QueueDel(ore);

        if (LaborCampPoints.ShouldRelease(false, sentence.Points, sentence.PointGoal))
            Release((id, sentence));
        else
            UpdateConsoles(id);

        return true;
    }

    /// <summary>
    /// Frontier serializes this field as <c>idCardProto</c>. HardLight requires <c>IdCardProto</c> and rejects the other name.
    /// Setting the property skips both YAML names. The wall locker has no upstream genpop parent, so the component is added here.
    /// </summary>
    private void OnLockerStartup(Entity<LaborCampLockerComponent> ent, ref ComponentStartup args)
    {
        var genpop = EnsureComp<GenpopLockerComponent>(ent);
        genpop.IdCardProto = LaborCampPrototypes.PrisonerId;
    }

    private void OnPoints(Entity<LaborCampLockerComponent> ent, ref LaborCampLockerPointsMessage args)
    {
        if (args.Points < 0)
            return;

        if (!_enabled)
        {
            ent.Comp.HasPending = false;
            if (args.Points > 0)
                _popup.PopupEntity(Loc.GetString("labor-camp-disabled"), ent, args.Actor);
            return;
        }

        ent.Comp.PendingPoints = Math.Min(args.Points, LaborCampPoints.MaxGoal);
        ent.Comp.HasPending = true;
        ent.Comp.PendingSeconds = 0f;
        ent.Comp.PendingActor = args.Actor;
        ent.Comp.PendingPreviousId = TryComp<GenpopLockerComponent>(ent, out var genpop) ? genpop.LinkedId : null;
    }

    private void OnRedeem(Entity<LaborCampRedemptionComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_enabled)
        {
            _popup.PopupEntity(Loc.GetString("labor-camp-disabled"), ent, args.User);
            return;
        }

        if (!_idCard.TryFindIdCard(args.User, out var card))
        {
            _popup.PopupEntity(Loc.GetString("labor-camp-redeem-no-id"), ent, args.User);
            return;
        }

        if (!TryComp<LaborCampSentenceComponent>(card, out var sentence) || !LaborCampPoints.HasPointGoal(sentence.PointGoal))
        {
            _popup.PopupEntity(Loc.GetString("labor-camp-redeem-no-goal"), ent, args.User);
            return;
        }

        if (TryComp<ExpireIdCardComponent>(card, out var expire) && expire.Expired)
        {
            _popup.PopupEntity(Loc.GetString("labor-camp-redeem-served"), ent, args.User);
            return;
        }

        var ore = CollectOre(args.Used);
        if (ore.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("labor-camp-redeem-not-ore"), ent, args.User);
            return;
        }

        var awarded = 0;
        foreach (var piece in ore)
        {
            if (TryComp<ExpireIdCardComponent>(card, out expire) && expire.Expired)
                break;

            if (!TryAwardOre(card.Owner, piece, out var points))
                continue;

            awarded += points;
        }

        if (awarded <= 0)
        {
            _popup.PopupEntity(Loc.GetString("labor-camp-redeem-worthless"), ent, args.User);
            return;
        }

        var goal = sentence.PointGoal;
        var total = sentence.Points;
        _popup.PopupEntity(Loc.GetString("labor-camp-redeem-awarded",
            ("points", awarded),
            ("total", total),
            ("goal", goal),
            ("remaining", LaborCampPoints.Remaining(total, goal))), ent, args.User);

        if (LaborCampPoints.GoalMet(total, goal))
            _popup.PopupEntity(Loc.GetString("labor-camp-redeem-released"), ent, args.User, PopupType.Medium);

        _admin.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User)} redeemed ore for {awarded} labor points on {ToPrettyString(card)} ({total}/{goal}).");
    }

    private List<EntityUid> CollectOre(EntityUid item)
    {
        var found = new List<EntityUid>();
        if (_tags.HasTag(item, OreTag))
        {
            found.Add(item);
            return found;
        }

        if (!TryComp<ContainerManagerComponent>(item, out var manager))
            return found;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities.ToArray())
            {
                if (_tags.HasTag(contained, OreTag))
                    found.Add(contained);
            }
        }

        return found;
    }

    private void Release(Entity<LaborCampSentenceComponent> ent)
    {
        if (TryComp<ExpireIdCardComponent>(ent, out var expire) && !expire.Expired)
        {
            try
            {
                _idCard.ExpireId((ent, expire));
            }
            catch (UnknownPrototypeException ex)
            {
                // ExpireId swaps access before it radios. A fork whose inherited channel does not exist
                // throws there. The sentence is still served, and NotifySecurity picks a channel that does.
                Log.Warning($"Labor camp expire radio failed: {ex.Message}");
            }
        }

        if (TryComp<ExpireIdCardComponent>(ent, out expire))
            NotifySecurity(ent, expire);

        UpdateConsoles(ent);
    }

    private void NotifySecurity(Entity<LaborCampSentenceComponent> ent, ExpireIdCardComponent expire)
    {
        if (ent.Comp.SecurityNotified || !expire.Expired)
            return;

        ent.Comp.SecurityNotified = true;
        Dirty(ent);

        // Frontier's prisoner ID radios on its own when it expires. Those fields are not on every fork,
        // so this looks them up by name and stays silent when the stock alert already went out.
        if (StockRadioWillSend(expire))
            return;

        if (ResolveChannel() is not { } channel)
        {
            Log.Warning("Labor camp sentence ended, but no radio channel exists to notify security.");
            return;
        }

        var name = TryComp<IdCardComponent>(ent, out var card) && !string.IsNullOrWhiteSpace(card.FullName)
            ? card.FullName
            : Identity.Name(ent, EntityManager);
        var crime = TryComp<GenpopIdCardComponent>(ent, out var genpop) ? genpop.Crime : string.Empty;
        var message = Loc.GetString("labor-camp-radio-served", ("name", name), ("crime", crime));
        _radio.SendRadioMessage(ent, message, channel, ent);
    }

    /// <summary>
    /// True when this fork's genpop ID already radios on expiry. HardLight has no such fields.
    /// </summary>
    private bool StockRadioWillSend(ExpireIdCardComponent expire)
    {
        var type = expire.GetType();
        if (type.GetField("ExpireMessageRadio")?.GetValue(expire) == null)
            return false;

        var channel = type.GetField("RadioChannel")?.GetValue(expire)?.ToString();
        if (string.IsNullOrEmpty(channel))
            return false;

        return _proto.HasIndex<RadioChannelPrototype>(channel);
    }

    private ProtoId<RadioChannelPrototype>? ResolveChannel()
    {
        ProtoId<RadioChannelPrototype>[] candidates =
        {
            _radioChannel,
            NfsdChannel,
            SecurityChannel,
            CommonChannel,
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate.Id))
                continue;

            if (_proto.TryIndex(candidate, out RadioChannelPrototype? _))
                return candidate;
        }

        return null;
    }

    private void OnConsoleOpen(Entity<LaborCampConsoleComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        PushConsole(ent, args.User);
    }

    private void OnConsoleRefresh(Entity<LaborCampConsoleComponent> ent, ref LaborCampConsoleRefreshMessage args)
    {
        PushConsole(ent, args.Actor);
    }

    private void PushConsole(Entity<LaborCampConsoleComponent> ent, EntityUid user)
    {
        if (!_idCard.TryFindIdCard(user, out var card))
        {
            ent.Comp.DisplayedId = null;
            _ui.SetUiState(ent.Owner, LaborCampConsoleUiKey.Key, new LaborCampConsoleState());
            return;
        }

        ent.Comp.DisplayedId = card.Owner;
        _ui.SetUiState(ent.Owner, LaborCampConsoleUiKey.Key, BuildState(card.Owner));
    }

    private void UpdateConsoles(EntityUid id)
    {
        var state = BuildState(id);
        var query = EntityQueryEnumerator<LaborCampConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.DisplayedId != id)
                continue;

            _ui.SetUiState(uid, LaborCampConsoleUiKey.Key, state);
        }
    }

    private LaborCampConsoleState BuildState(EntityUid id)
    {
        var state = new LaborCampConsoleState
        {
            HasId = true,
            Name = TryComp<IdCardComponent>(id, out var card) && !string.IsNullOrWhiteSpace(card.FullName)
                ? card.FullName
                : Identity.Name(id, EntityManager),
            Crime = TryComp<GenpopIdCardComponent>(id, out var genpop) ? genpop.Crime : string.Empty,
        };

        if (TryComp<LaborCampSentenceComponent>(id, out var sentence))
        {
            state.Points = sentence.Points;
            state.PointGoal = sentence.PointGoal;
            state.HasPointGoal = LaborCampPoints.HasPointGoal(sentence.PointGoal);
        }

        if (TryComp<ExpireIdCardComponent>(id, out var expire))
        {
            state.Served = expire.Expired;
            state.Permanent = expire.Permanent && !expire.Expired;
            if (!expire.Permanent && !expire.Expired && TryComp<GenpopIdCardComponent>(id, out genpop) && genpop.SentenceDuration > TimeSpan.Zero)
            {
                state.HasTimeGoal = true;
                var seconds = (expire.ExpireTime - _timing.CurTime).TotalSeconds;
                state.TimeRemainingSeconds = seconds <= 0 ? 0 : (int)Math.Ceiling(seconds);
            }
        }

        return state;
    }
}
