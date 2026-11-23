// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.LaborCamp;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.LaborCamp;

/// <summary>
/// Current points, the goal, and the time still left on the official genpop timer.
/// </summary>
[UsedImplicitly]
public sealed class LaborCampConsoleBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;

    public LaborCampConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new DefaultWindow
        {
            Title = Loc.GetString("labor-camp-console-title"),
            MinSize = new System.Numerics.Vector2(420, 280),
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
        };
        var refresh = new Button
        {
            Text = Loc.GetString("labor-camp-console-refresh"),
            HorizontalAlignment = Control.HAlignment.Right,
        };
        refresh.OnPressed += _ => SendMessage(new LaborCampConsoleRefreshMessage());
        root.AddChild(_body);
        root.AddChild(refresh);
        _window.Contents.AddChild(root);
        Show(Loc.GetString("labor-camp-console-no-id"));

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not LaborCampConsoleState labor)
            return;

        if (_body == null)
            return;

        _body.DisposeAllChildren();
        if (!labor.HasId)
        {
            Show(Loc.GetString("labor-camp-console-no-id"));
            return;
        }

        AddLine(Loc.GetString("labor-camp-console-name", ("name", labor.Name)));
        AddLine(Loc.GetString("labor-camp-console-crime", ("crime", labor.Crime)));

        if (labor.Served)
            AddLine(Loc.GetString("labor-camp-console-served"));

        if (labor.HasPointGoal)
        {
            AddLine(Loc.GetString("labor-camp-console-points", ("points", labor.Points)));
            AddLine(Loc.GetString("labor-camp-console-goal", ("goal", labor.PointGoal)));
            AddLine(Loc.GetString("labor-camp-console-remaining",
                ("remaining", LaborCampPoints.Remaining(labor.Points, labor.PointGoal))));
        }
        else
        {
            AddLine(Loc.GetString("labor-camp-console-no-goal"));
        }

        if (labor.HasTimeGoal)
        {
            var minutes = labor.TimeRemainingSeconds / 60;
            var seconds = labor.TimeRemainingSeconds % 60;
            AddLine(Loc.GetString("labor-camp-console-time", ("minutes", minutes), ("seconds", seconds)));
        }
        else if (labor.Permanent)
        {
            AddLine(Loc.GetString("labor-camp-console-permanent"));
        }
        else if (!labor.Served)
        {
            AddLine(Loc.GetString("labor-camp-console-no-time"));
        }
    }

    private void Show(string text)
    {
        _body?.DisposeAllChildren();
        AddLine(text);
    }

    private void AddLine(string text)
    {
        _body?.AddChild(new Label
        {
            Text = text,
            Margin = new Thickness(0, 2),
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
        _window = null;
        _body = null;
    }
}
