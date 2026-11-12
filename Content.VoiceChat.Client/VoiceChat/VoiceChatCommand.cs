// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.VoiceChat.Client.UI;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.VoiceChat.Client;

public sealed partial class VoiceChatCommand : LocalizedCommands
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    public override string Command => "voicechat";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _ui.GetUIController<VoiceChatWindowController>().Toggle();
    }
}
