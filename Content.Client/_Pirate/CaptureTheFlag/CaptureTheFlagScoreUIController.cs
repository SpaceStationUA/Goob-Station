// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared._Pirate.CaptureTheFlag;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.CaptureTheFlag;

public sealed class CaptureTheFlagScoreUIController : UIController, IOnStateChanged<GameplayState>,
    IOnSystemChanged<CaptureTheFlagScoreSystem>
{
    private CaptureTheFlagScoreEvent? _score;
    private CaptureTheFlagScoreboard? _widget;
    private bool? _redTeam;

    private void OnAssignment(bool? red)
    {
        _redTeam = red;
        if (_score != null)
            _widget?.SetScore(_score, _redTeam);
    }

    private void OnScore(CaptureTheFlagScoreEvent score)
    {
        if (_score == null)
            UIManager.OnScreenChanged += OnScreenChanged;

        _score = score;
        if (_widget == null)
            AttachWidget();
        else
            _widget.SetScore(score, _redTeam);
    }

    private void AttachWidget()
    {
        _widget?.Orphan();
        _widget = null;
        if (_score == null || UIManager.GetActiveUIWidgetOrNull<GhostGui>()?.Parent is not LayoutContainer viewport)
            return;

        _widget = new CaptureTheFlagScoreboard();
        _widget.SetScore(_score, _redTeam);
        viewport.AddChild(_widget);
        LayoutContainer.SetAnchorPreset(_widget, LayoutContainer.LayoutPreset.CenterTop);
        LayoutContainer.SetMarginTop(_widget, 14);
        LayoutContainer.SetMarginBottom(_widget, 14);
        LayoutContainer.SetGrowHorizontal(_widget, LayoutContainer.GrowDirection.Both);
    }

    private void OnScreenChanged((UIScreen? Old, UIScreen? New) screens) => AttachWidget();

    public void OnStateEntered(GameplayState state)
    {
        if (_score != null && _widget == null)
            AttachWidget();
    }

    public void OnStateExited(GameplayState state) => Reset();

    public void OnSystemLoaded(CaptureTheFlagScoreSystem system)
    {
        system.ScoreChanged += OnScore;
        system.AssignmentChanged += OnAssignment;
        system.ScoreClosed += Reset;
    }

    public void OnSystemUnloaded(CaptureTheFlagScoreSystem system)
    {
        system.ScoreChanged -= OnScore;
        system.AssignmentChanged -= OnAssignment;
        system.ScoreClosed -= Reset;
        Reset();
    }

    private void Reset()
    {
        _redTeam = null;
        if (_score == null)
            return;

        UIManager.OnScreenChanged -= OnScreenChanged;
        _score = null;
        _widget?.Orphan();
        _widget = null;
    }
}

internal sealed class CaptureTheFlagScoreboard : PanelContainer
{
    private readonly Label _red = new() { MouseFilter = MouseFilterMode.Ignore, FontColorOverride = Color.FromHex("#FF8998") };
    private readonly Label _blue = new() { MouseFilter = MouseFilterMode.Ignore, FontColorOverride = Color.FromHex("#8DCDFF") };
    private readonly Label _tier = new() { MouseFilter = MouseFilterMode.Ignore, FontColorOverride = Color.FromHex("#EBDBC1") };

    private readonly Label _center = new() { MouseFilter = MouseFilterMode.Ignore, FontColorOverride = Color.FromHex("#C1CEDD") };

    public CaptureTheFlagScoreboard()
    {
        MouseFilter = MouseFilterMode.Ignore;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#17202CF2"),
            BorderColor = Color.FromHex("#56657D"),
            BorderThickness = new Thickness(1),
        };

        var scores = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 16,
            MouseFilter = MouseFilterMode.Ignore,
        };
        scores.AddChild(_red);
        scores.AddChild(_blue);
        scores.AddChild(_tier);
        var rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(10, 3),
            SeparationOverride = 0,
            MouseFilter = MouseFilterMode.Ignore,
        };
        scores.HorizontalAlignment = HAlignment.Center;
        _center.HorizontalAlignment = HAlignment.Center;
        rows.AddChild(scores);
        rows.AddChild(_center);
        AddChild(rows);
    }

    public void SetScore(CaptureTheFlagScoreEvent score, bool? redTeam)
    {
        _red.Text = Loc.GetString("ctf-hud-red", ("score", score.Red));
        _blue.Text = Loc.GetString("ctf-hud-blue", ("score", score.Blue));
        if (redTeam is {} red)
        {
            var own = red ? _red : _blue;
            own.Text = Loc.GetString("ctf-hud-yours", ("team", own.Text!));
        }
        _tier.Text = Loc.GetString("ctf-hud-tier", ("tier", score.Tier));
        var center = score.Center switch
        {
            CaptureTheFlagRelayState.Red => "ctf-center-red",
            CaptureTheFlagRelayState.Blue => "ctf-center-blue",
            CaptureTheFlagRelayState.TakingRed => "ctf-center-taking-red",
            CaptureTheFlagRelayState.TakingBlue => "ctf-center-taking-blue",
            CaptureTheFlagRelayState.Contested => "ctf-center-contested",
            _ => "ctf-center-neutral",
        };
        _center.Text = Loc.GetString("ctf-hud-center", ("state", Loc.GetString(center)),
            ("red", score.RedReserve), ("blue", score.BlueReserve));
    }
}
