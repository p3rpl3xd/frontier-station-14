// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.VoiceChat.Client.UI;

public sealed partial class VoiceChatWindowController : UIController
{
    private DefaultWindow? _window;

    public void Toggle()
    {
        Ensure();
        if (_window!.IsOpen)
            _window.Close();
        else
            _window.OpenCentered();
    }

    public void Open()
    {
        Ensure();
        _window!.OpenCentered();
    }

    private void Ensure()
    {
        if (_window != null)
            return;

        _window = new DefaultWindow
        {
            Title = Loc.GetString("ui-escape-voice-chat"),
            MinWidth = 420,
            MinHeight = 280,
        };
        _window.Contents.AddChild(new VoiceConnectPanel());
    }
}
