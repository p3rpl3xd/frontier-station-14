// SPDX-FileCopyrightText: 2026 p3rpl3xd
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.LaborCamp;
using Content.Shared.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.LaborCamp;

/// <summary>
/// Two buttons: labor camp and the main station. The shuttle map is not shown while the route is locked.
/// </summary>
[UsedImplicitly]
public sealed class LaborCampShuttleBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;

    public LaborCampShuttleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new DefaultWindow
        {
            Title = Loc.GetString("labor-camp-shuttle-title"),
            MinSize = new System.Numerics.Vector2(380, 220),
        };

        _body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10),
        };
        _window.Contents.AddChild(_body);
        _window.OnClose += Close;
        _window.OpenCentered();
        if (State != null)
            UpdateState(State);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not LaborCampShuttleState route || _body == null)
            return;

        _body.DisposeAllChildren();
        _body.AddChild(new Label
        {
            Text = Loc.GetString(route.Locked ? "labor-camp-shuttle-locked" : "labor-camp-shuttle-unlocked"),
        });

        if (route.InFtl)
            _body.AddChild(new Label { Text = Loc.GetString("labor-camp-shuttle-in-ftl") });

        AddDestination(LaborCampShuttleDestination.Camp, route.CampName, route.CampAvailable, route.CampDocked || route.InFtl);
        AddDestination(LaborCampShuttleDestination.Station, route.StationName, route.StationAvailable, route.StationDocked || route.InFtl);

        if (!route.Locked && PlayerManager.LocalEntity is { } actor)
        {
            var helm = new Button { Text = Loc.GetString("labor-camp-shuttle-open-helm") };
            helm.OnPressed += _ => UiSystem.TryToggleUi(Owner, ShuttleConsoleUiKey.Key, actor);
            _body.AddChild(helm);
        }
    }

    private void AddDestination(LaborCampShuttleDestination destination, string name, bool available, bool blocked)
    {
        var label = string.IsNullOrEmpty(name)
            ? Loc.GetString(destination == LaborCampShuttleDestination.Camp
                ? "labor-camp-shuttle-to-camp"
                : "labor-camp-shuttle-to-station")
            : Loc.GetString("labor-camp-shuttle-to-named", ("name", name));

        var button = new Button
        {
            Text = label,
            Disabled = !available || blocked,
        };
        button.OnPressed += _ => SendMessage(new LaborCampShuttleTravelMessage(destination));
        _body?.AddChild(button);
    }
}
