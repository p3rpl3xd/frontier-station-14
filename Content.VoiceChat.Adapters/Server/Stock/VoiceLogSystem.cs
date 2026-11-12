// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Content.VoiceChat.Shared;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
#if VOICECHAT_UPSTREAM
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Events;
#else
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
#endif
using Content.Shared.Administration;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.VoiceChat.Server;

public sealed partial class VoiceLogSystem : EntitySystem, IVoiceRecorder
{
    [Dependency] private VoiceLogManager _logs = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private VoiceFeatures _features = default!;

    private static readonly SpriteSpecifier VerbIcon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"));

    private readonly ConcurrentQueue<Action> _completed = new();
    private bool _enabled;
    private int _keepRounds;

    public override void Initialize()
    {
        base.Initialize();

        _features.Recorder = this;
        Subs.CVar(_cfg, VoiceCVars.VoiceLogEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, VoiceCVars.VoiceLogRounds, value => _keepRounds = value, true);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_completed.TryDequeue(out var action))
        {
            action();
        }
    }

    public void Record(ICommonSession session, VoiceWebFrame frame, VoiceLogFlags flags, string name, string channel)
    {
        if (!_enabled)
            return;

        _logs.Record(
            _gameTicker.RoundId,
            session.UserId.UserId,
            session.Name,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            frame.Sequence,
            flags,
            frame.Raw,
            name,
            channel);
    }

    public void OpenViewer(ICommonSession admin, Guid? user = null)
    {
        _eui.OpenEui(new VoiceLogsEui(user), admin);
    }

    public void OnMainThread<T>(Task<T> task, Action<T> callback)
    {
        _ = Forward(task, callback);
    }

    private async Task Forward<T>(Task<T> task, Action<T> callback)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            _completed.Enqueue(() => callback(result));
        }
        catch (Exception e)
        {
            Log.Error($"Voice log read failed: {e}");
        }
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (_enabled)
            _logs.StartRound(ev.Id, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _keepRounds);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var admin) ||
            !TryComp<ActorComponent>(args.Target, out var target) ||
            !_admin.HasAdminFlag(admin.PlayerSession, AdminFlags.Logs))
        {
            return;
        }

        var adminSession = admin.PlayerSession;
        var user = target.PlayerSession.UserId.UserId;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("voice-logs-verb"),
            Category = VerbCategory.Admin,
            Icon = VerbIcon,
            Act = () => OpenViewer(adminSession, user),
        });
    }
}
