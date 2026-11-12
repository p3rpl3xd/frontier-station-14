// SPDX-FileCopyrightText: 2026 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Aiden <aiden@djkraz.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.VoiceChat.Shared;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.VoiceChat.Client.UI;

public sealed partial class VoiceConnectPanel : BoxContainer
{
    [Dependency] private IClipboardManager _clipboard = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private VoiceChatManager _voice = default!;

    private static readonly Color ConnectedColor = Color.FromHex("#5CD65C");

    private readonly Label _status;
    private readonly Button _open;
    private readonly Button _copy;
    private readonly Label _pageUrl;
    private readonly Label _code;
    private readonly Slider _volume;
    private readonly CheckBox _hearSelf;

    public VoiceConnectPanel()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;

        _status = new Label();
        _open = new Button { Text = Loc.GetString("voice-link-open"), HorizontalExpand = true };
        _copy = new Button { Text = Loc.GetString("voice-link-copy"), HorizontalExpand = true };
        _open.OnPressed += _ => _voice.OpenLink();
        _copy.OnPressed += _ =>
        {
            if (_voice.LinkUrl is { } url)
                _clipboard.SetText(url);
        };

        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        buttons.AddChild(_open);
        buttons.AddChild(_copy);

        _pageUrl = new Label();
        _code = new Label { Align = Label.AlignMode.Center };

        AddChild(_status);
        AddChild(new Label { Text = Loc.GetString("voice-link-computer-title") });
        AddChild(new Label { Text = Loc.GetString("voice-link-computer-text") });
        AddChild(buttons);
        AddChild(new Label { Text = Loc.GetString("voice-link-phone-title") });
        AddChild(new Label { Text = Loc.GetString("voice-link-phone-text") });
        AddChild(_pageUrl);
        AddChild(_code);
        AddChild(new Label { Text = Loc.GetString("voice-link-expiry") });

        _volume = new Slider { MinValue = 0, MaxValue = 2, HorizontalExpand = true };
        _volume.SetValueWithoutEvent(_cfg.GetCVar(VoiceCVars.VoiceChatVolume));
        _volume.OnValueChanged += range => _cfg.SetCVar(VoiceCVars.VoiceChatVolume, range.Value);
        AddChild(new Label { Text = Loc.GetString("ui-options-voice-chat-volume") });
        AddChild(_volume);

        _hearSelf = new CheckBox { Text = Loc.GetString("ui-options-hear-self") };
        _hearSelf.Pressed = _cfg.GetCVar(VoiceCVars.VoiceChatHearSelf);
        _hearSelf.OnToggled += args => _cfg.SetCVar(VoiceCVars.VoiceChatHearSelf, args.Pressed);
        AddChild(_hearSelf);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _voice.LinkChanged += UpdateState;
        _voice.WebConnectedChanged += OnWebConnectedChanged;
        _voice.RequestLink();
        UpdateState();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _voice.LinkChanged -= UpdateState;
        _voice.WebConnectedChanged -= OnWebConnectedChanged;
    }

    private void OnWebConnectedChanged(bool connected)
    {
        UpdateState();
    }

    private void UpdateState()
    {
        var ready = _voice.LinkUrl != null;
        _status.Text = Loc.GetString(_voice.WebConnected ? "voice-link-connected" : "voice-link-disconnected");
        _status.FontColorOverride = _voice.WebConnected ? ConnectedColor : null;
        _pageUrl.Text = _voice.PageUrl ?? Loc.GetString("voice-link-loading");
        _code.Text = _voice.LinkCode ?? "--------";
        _open.Disabled = !ready;
        _copy.Disabled = !ready;
    }
}
