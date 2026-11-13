// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.VoiceChat.Client.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.VoiceChat.Client;

/// <summary>
/// Puts the headset channel button on the chat bar. Forks that already construct it are left alone.
/// If the chat bar cannot be found, the voice window still hosts one.
/// </summary>
public static class VoiceRadioHost
{
    public static void Ensure(IUserInterfaceManager ui)
    {
        var root = ui.RootControl;
        if (root == null)
            return;

        foreach (var chat in Find<ChatInputBox>(root))
        {
            if (HasButton(chat))
                continue;

            var parent = chat.ChannelSelector.Parent;
            if (parent == null)
                continue;

            var button = new VoiceRadioButton { HorizontalExpand = false };
            button.AddStyleClass("chatSelectorOptionButton");
            button.AddStyleClass("ChatSelectorOptionButton");
            parent.AddChild(button);
            var index = 0;
            foreach (var child in parent.Children)
            {
                if (child == chat.ChannelSelector)
                    break;
                index++;
            }

            button.SetPositionInParent(Math.Min(index + 1, parent.ChildCount - 1));
        }
    }

    private static bool HasButton(Control root)
    {
        if (root is VoiceRadioButton)
            return true;

        foreach (var child in root.Children)
        {
            if (HasButton(child))
                return true;
        }

        return false;
    }

    private static IEnumerable<T> Find<T>(Control root) where T : Control
    {
        if (root is T match)
            yield return match;

        foreach (var child in root.Children)
        {
            foreach (var found in Find<T>(child))
                yield return found;
        }
    }
}
