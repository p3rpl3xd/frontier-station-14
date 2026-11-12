// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Eui;
using Content.VoiceChat.Shared;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.VoiceChat.Client.Logs;

[UsedImplicitly]
public sealed partial class VoiceLogsEui : BaseEui
{
    private readonly VoiceLogsWindow _window;

    public VoiceLogsEui()
    {
        _window = new VoiceLogsWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.RoundSelected += round => SendMessage(new VoiceLogsSelectRoundMessage(round));
        _window.RefreshRequested += OnRefresh;
        _window.TrackRequested += (round, user) => SendMessage(new VoiceLogsRequestTrackMessage(round, user));
        _window.AudioRequested += (round, user, first, last) => SendMessage(new VoiceLogsRequestAudioMessage(round, user, first, last));
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.StopAll();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is VoiceLogsEuiState logs)
            _window.SetState(logs);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case VoiceLogsTrackMessage track:
                _window.SetTrack(track);
                break;
            case VoiceLogsAudioMessage audio:
                _window.ReceiveAudio(audio);
                break;
        }
    }

    private void OnRefresh()
    {
        SendMessage(new VoiceLogsRefreshMessage());
        foreach (var user in _window.TrackUsers)
        {
            SendMessage(new VoiceLogsRequestTrackMessage(_window.RoundId, user));
        }
    }
}
