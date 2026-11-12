// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.VoiceChat.Shared;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.VoiceChat.Client.UI;

public sealed partial class VoiceSpeakerListUIController : UIController
{
    private const float Margin = 10f;

    [Dependency] private IConfigurationManager _cfg = default!;
    [UISystemDependency] private readonly VoiceChatSystem? _voice = default;

    private VoiceSpeakerList? _list;
    private UIScreen? _attachedScreen;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(VoiceCVars.VoiceChatSpeakerList, enabled => _enabled = enabled, true);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_enabled || _voice == null)
        {
            Detach();
            return;
        }

        var screen = UIManager.ActiveScreen;
        if (screen != _attachedScreen || _list?.Parent == null)
            Attach(screen);

        if (_list == null)
            return;

        _list.SetInset(Margin, Margin);
        _list.Update(args.DeltaSeconds, _voice);
    }

    private void Attach(UIScreen? screen)
    {
        Detach();
        if (screen == null)
            return;

        _list ??= new VoiceSpeakerList();
        screen.AddChild(_list);
        LayoutContainer.SetAnchorPreset(_list, LayoutContainer.LayoutPreset.Wide);
        _attachedScreen = screen;
    }

    private void Detach()
    {
        _list?.Clear();
        _list?.Orphan();
        _attachedScreen = null;
    }
}
