// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using Content.Shared.CCVar;
using Content.Shared.LaborCamp;
using Content.Shared.Security.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;

namespace Content.Client.LaborCamp;

/// <summary>
/// Genpop locker window plus a point goal. The time sentence is still the official genpop message.
/// </summary>
[UsedImplicitly]
public sealed class LaborCampLockerBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private LineEdit? _name;
    private LineEdit? _minutes;
    private LineEdit? _points;
    private LineEdit? _crime;
    private Button? _done;

    public LaborCampLockerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var maxName = cfg.GetCVar(CCVars.MaxIdJobLength);

        _window = new DefaultWindow
        {
            Title = Loc.GetString("labor-camp-locker-title"),
            MinSize = new System.Numerics.Vector2(460, 360),
        };

        _name = new LineEdit { HorizontalExpand = true };
        _minutes = new LineEdit { HorizontalExpand = true, Text = "5" };
        _points = new LineEdit { HorizontalExpand = true, Text = "0" };
        _crime = new LineEdit { HorizontalExpand = true, Text = Loc.GetString("labor-camp-crime-default") };
        _done = new Button
        {
            Text = Loc.GetString("labor-camp-locker-done"),
            HorizontalAlignment = Control.HAlignment.Right,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(new Label { Text = Loc.GetString("labor-camp-locker-hint") });
        root.AddChild(Field(Loc.GetString("labor-camp-locker-name"), _name));
        root.AddChild(Field(Loc.GetString("labor-camp-locker-minutes"), _minutes));
        root.AddChild(Field(Loc.GetString("labor-camp-locker-points"), _points));
        root.AddChild(Field(Loc.GetString("labor-camp-locker-crime"), _crime));
        root.AddChild(_done);
        _window.Contents.AddChild(root);

        void Refresh()
        {
            _done.Disabled = !Valid(maxName);
        }

        _name.OnTextChanged += _ => Refresh();
        _minutes.OnTextChanged += _ => Refresh();
        _points.OnTextChanged += _ => Refresh();
        _crime.OnTextChanged += _ => Refresh();
        Refresh();

        _done.OnPressed += _ =>
        {
            if (!Valid(maxName))
                return;

            var points = int.Parse(_points.Text, CultureInfo.InvariantCulture);
            SendMessage(new LaborCampLockerPointsMessage(points));
            SendMessage(new GenpopLockerIdConfiguredMessage(
                _name.Text.Trim(),
                float.Parse(_minutes.Text, CultureInfo.InvariantCulture),
                _crime.Text.Trim()));
            Close();
        };

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    private bool Valid(int maxName)
    {
        if (_name == null || _minutes == null || _points == null || _crime == null)
            return false;

        var name = _name.Text.Trim();
        var crime = _crime.Text.Trim();
        return !string.IsNullOrWhiteSpace(name)
               && name.Length <= maxName
               && float.TryParse(_minutes.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
               && minutes >= 0
               && !float.IsNaN(minutes)
               && !float.IsInfinity(minutes)
               && int.TryParse(_points.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var points)
               && points >= 0
               && points <= LaborCampPoints.MaxGoal
               && !string.IsNullOrWhiteSpace(crime)
               && crime.Length <= GenpopLockerComponent.MaxCrimeLength;
    }

    private static BoxContainer Field(string label, Control edit)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 6, 0, 0),
        };
        box.AddChild(new Label { Text = label });
        box.AddChild(edit);
        return box;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
        _window = null;
    }
}
