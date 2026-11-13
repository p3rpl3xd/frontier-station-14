// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.Options.UI.Tabs;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.VoiceChat.Client;

/// <summary>
/// Adds the voice keys to the controls tab. The tab's rows are private, so this appends
/// its own rows to the container named KeybindsContainer.
/// </summary>
public static class VoiceKeybindMenu
{
    private const string Marker = "VoiceChatKeybinds";

    public static void TryInstall(IUserInterfaceManager ui, IInputManager input)
    {
        var root = ui.RootControl;
        if (root == null)
            return;

        foreach (var tab in Find<KeyRebindTab>(root))
        {
            var container = FindNamed(tab, "KeybindsContainer") as BoxContainer;
            if (container == null || HasMarker(container))
                continue;

            container.AddChild(new Control { Name = Marker, MinSize = new System.Numerics.Vector2(0, 8) });
            container.AddChild(new Label
            {
                Text = Loc.GetString("voice-keybinds-header"),
                StyleClasses = { "LabelKeyText" },
            });
            container.AddChild(new VoiceKeyRow(input, VoiceKeys.OpenWindow, Keyboard.Key.F6, "ui-options-function-voice-chat-window"));
            container.AddChild(new VoiceKeyRow(input, VoiceKeys.PushToTalk, Keyboard.Key.N, "ui-options-function-voice-push-to-talk"));
            container.AddChild(new VoiceKeyRow(input, VoiceKeys.PushToTalkRadio, Keyboard.Key.M, "ui-options-function-voice-push-to-talk-radio"));
        }
    }

    private static bool HasMarker(Control container)
    {
        foreach (var child in container.Children)
        {
            if (child.Name == Marker)
                return true;
        }

        return false;
    }

    private static Control? FindNamed(Control root, string name)
    {
        if (root.Name == name)
            return root;

        foreach (var child in root.Children)
        {
            var found = FindNamed(child, name);
            if (found != null)
                return found;
        }

        return null;
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

    private sealed class VoiceKeyRow : BoxContainer
    {
        private readonly IInputManager _input;
        private readonly BoundKeyFunction _function;
        private readonly Keyboard.Key _fallback;
        private readonly Button _bind;
        private bool _capturing;

        public VoiceKeyRow(IInputManager input, BoundKeyFunction function, Keyboard.Key fallback, string label)
        {
            _input = input;
            _function = function;
            _fallback = fallback;
            Orientation = LayoutOrientation.Horizontal;
            SeparationOverride = 6;

            AddChild(new Label
            {
                Text = Loc.GetString(label),
                HorizontalExpand = true,
            });

            _bind = new Button { MinWidth = 80 };
            _bind.OnPressed += _ => BeginCapture();
            AddChild(_bind);

            var reset = new Button { Text = Loc.GetString("ui-options-bind-reset") };
            reset.OnPressed += _ =>
            {
                foreach (var binding in _input.GetKeyBindings(_function).ToList())
                    _input.RemoveBinding(binding);
                _input.RegisterBinding(new KeyBindingRegistration
                {
                    Function = _function,
                    Type = KeyBindingType.State,
                    BaseKey = _fallback,
                }, markModified: false);
                Refresh();
            };
            AddChild(reset);
            Refresh();
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();
            _input.FirstChanceOnKeyEvent += OnKey;
            _input.OnKeyBindingAdded += _ => Refresh();
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();
            _input.FirstChanceOnKeyEvent -= OnKey;
        }

        private void BeginCapture()
        {
            _capturing = true;
            _bind.Text = Loc.GetString("ui-options-key-prompt");
        }

        private void OnKey(KeyEventArgs args, KeyEventType type)
        {
            if (!_capturing || type != KeyEventType.Up || args.Key == Keyboard.Key.Unknown)
                return;

            args.Handle();
            _capturing = false;
            foreach (var binding in _input.GetKeyBindings(_function).ToList())
                _input.RemoveBinding(binding);

            _input.RegisterBinding(new KeyBindingRegistration
            {
                Function = _function,
                Type = KeyBindingType.State,
                BaseKey = args.Key,
            });
            _input.SaveToUserData();
            Refresh();
        }

        private void Refresh()
        {
            if (_capturing)
                return;

            _bind.Text = VoiceInput.Describe(_input, _function, _fallback);
        }
    }
}
