// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.VoiceChat.Client;

/// <summary>
/// State binds are ignored unless the function is in the active input context, and command binds
/// are executed as console commands named after the function. Voice keys are real key functions
/// on the common context, which human and ghost inherit.
/// </summary>
public static class VoiceInput
{
    public static void Install(IInputManager input, VoiceChatManager voice, IUserInterfaceManager? ui = null)
    {
        if (input.Contexts.Exists("common"))
        {
            var common = input.Contexts.GetContext("common");
            Add(common, VoiceKeys.OpenWindow);
            Add(common, VoiceKeys.PushToTalk);
            Add(common, VoiceKeys.PushToTalkRadio);
            Add(common, VoiceKeys.RadioCycle);
        }

        EnsureState(input, VoiceKeys.OpenWindow, Keyboard.Key.F6);
        EnsureState(input, VoiceKeys.PushToTalk, Keyboard.Key.N);
        EnsureState(input, VoiceKeys.PushToTalkRadio, Keyboard.Key.M);
        // = is free. ] is already admin chat, and a command bind would be run as a console command.
        EnsureState(input, VoiceKeys.RadioCycle, Keyboard.Key.Equal);

        input.SetInputCommand(VoiceKeys.PushToTalk,
            InputCmdHandler.FromDelegate(
                _ => voice.SendPushToTalk(true, false),
                _ => voice.SendPushToTalk(false, false)));
        input.SetInputCommand(VoiceKeys.PushToTalkRadio,
            InputCmdHandler.FromDelegate(
                _ => voice.SendPushToTalk(true, true),
                _ => voice.SendPushToTalk(false, true)));

        if (ui != null)
        {
            input.SetInputCommand(VoiceKeys.OpenWindow, InputCmdHandler.FromDelegate(_ =>
                ui.GetUIController<UI.VoiceChatWindowController>().Toggle()));
        }
    }

    public static string Describe(IInputManager input, BoundKeyFunction function, Keyboard.Key fallback)
    {
        foreach (var binding in input.GetKeyBindings(function))
        {
            if (binding.BindingType == KeyBindingType.Command)
                continue;

            return binding.GetKeyString();
        }

        return fallback.ToString();
    }

    private static void Add(IInputCmdContext context, BoundKeyFunction function)
    {
        if (!context.FunctionExists(function))
            context.AddFunction(function);
    }

    private static void EnsureState(IInputManager input, BoundKeyFunction function, Keyboard.Key key)
    {
        var existing = input.GetKeyBindings(function).ToArray();
        var usable = false;
        foreach (var binding in existing)
        {
            if (binding.BindingType == KeyBindingType.Command)
                input.RemoveBinding(binding, markModified: false);
            else
                usable = true;
        }

        if (usable)
            return;

        input.RegisterBinding(new KeyBindingRegistration
        {
            Function = function,
            Type = KeyBindingType.State,
            BaseKey = key,
        }, markModified: false);
    }
}
