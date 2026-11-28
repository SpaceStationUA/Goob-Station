// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Client._Pirate.Suspicion.UserInterface.Widgets;
using Content.Client.Gameplay;
using Robust.Shared.Timing;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Client._Pirate.Suspicion.RadarOverlay;

namespace Content.Client._Pirate.Suspicion.UserInterface;

public sealed class SSSStatusUIController : UIController, IOnSystemChanged<SSSStatusUISystem>, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [UISystemDependency] private readonly MobThresholdSystem? _threshold = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private SSSStatusGui? _widget;
    private bool _active;
    private string _role = string.Empty;
    private Color _roleColor;
    private TimeSpan _end;
    private int _seconds = -1;

    internal void UpdateTimer(TimeSpan endTime) => _end = endTime;

    internal void ShowRoundStatus(SuspicionGameState state, TimeSpan endTime)
    {
        _end = endTime;
        SetRole(Loc.GetString(state == SuspicionGameState.Preparing
            ? "suspicion-status-ui-role-preround"
            : "suspicion-status-ui-role-observing"), Color.Gray);
    }

    internal void UpdateRole(SuspicionRuleUpdateRole ev)
    {
        var roleName = Loc.GetString(ev.NewRole switch
        {
            SuspicionRole.Traitor => "roles-antag-suspicion-traitor-name",
            SuspicionRole.Detective => "roles-antag-suspicion-detective-name",
            SuspicionRole.Innocent => "roles-antag-suspicion-innocent-name",
            SuspicionRole.Wildcard => ev.NewSubRole switch
            {
                SuspicionSubRole.Jester => "roles-antag-suspicion-jester-name",
                _ => "roles-antag-suspicion-wildcard-unknown",
            },
            _ => "roles-antag-suspicion-unknown",
        });
        SetRole(roleName, Color.FromName(ev.NewRole.GetRoleColor()));
    }

    private void SetRole(string role, Color color)
    {
        _role = role;
        _roleColor = color;
        if (!_active)
        {
            _active = true;
            UIManager.OnScreenChanged += ScreenChanged;
            AttachWidget();
        }
        _widget?.SetRole(role, color);
        UpdateLocalHealth();
    }

    private void ScreenChanged((UIScreen? Old, UIScreen? New) screens) => AttachWidget();

    private void AttachWidget()
    {
        _widget?.Orphan();
        _widget = null;
        // Both layouts put the ghost controls in the game viewport, including separated chat.
        if (UIManager.GetActiveUIWidgetOrNull<GhostGui>()?.Parent is not LayoutContainer viewport)
            return;

        _widget = new SSSStatusGui { UpdateClock = UpdateClock };
        viewport.AddChild(_widget);
        LayoutContainer.SetAnchorAndMarginPreset(_widget, LayoutContainer.LayoutPreset.BottomRight, margin: 85);
        _widget.SetRole(_role, _roleColor);
        _seconds = -1;
        UpdateClock();
        UpdateLocalHealth();
    }

    private void UpdateClock()
    {
        if (_widget == null)
            return;
        _widget.TimerText.Visible = _end != TimeSpan.Zero;
        if (!_widget.TimerText.Visible)
            return;

        var seconds = Math.Max(0, (int)Math.Ceiling((_end - _timing.CurTime).TotalSeconds));
        if (seconds == _seconds)
            return;
        _seconds = seconds;
        _widget.TimerText.Text = $"{seconds / 60}:{seconds % 60:00}";
    }

    private void UpdateLocalHealth()
    {
        if (_players.LocalEntity is {} body && EntityManager.TryGetComponent<DamageableComponent>(body, out var health))
            UpdateHealth((body, health));
    }

    private void UpdateHealth(Entity<DamageableComponent> ent)
    {
        if (_widget == null || !EntityManager.TryGetComponent<MobStateComponent>(ent, out var state))
            return;
        if (state.CurrentState == MobState.Dead)
        {
            _widget.SetHealthBar(Loc.GetString("suspicion-status-ui-health-dead"), 0, 100);
            return;
        }
        if (_threshold == null || !EntityManager.TryGetComponent<MobThresholdsComponent>(ent, out var thresholds))
            return;
        var max = _threshold.GetThresholdForState(ent, MobState.Critical, thresholds);
        var hp = (float)Math.Max(0, Math.Ceiling((max - _threshold.CheckVitalDamage(ent, ent.Comp)).Double()));
        _widget.SetHealthBar($"♥ {hp}", hp, max.Float());
    }

    public void OnSystemLoaded(SSSStatusUISystem system) => system.OnPlayerDamageChanged += UpdateHealth;

    public void OnSystemUnloaded(SSSStatusUISystem system)
    {
        system.OnPlayerDamageChanged -= UpdateHealth;
        ResetHud();
    }

    public void OnStateEntered(GameplayState state)
    {
        if (_active && _widget == null)
            AttachWidget();
    }

    public void OnStateExited(GameplayState state) => ResetHud();

    internal void ResetHud()
    {
        if (!_active)
            return;
        UIManager.OnScreenChanged -= ScreenChanged;
        _widget?.Orphan();
        _widget = null;
        _active = false;
        _seconds = -1;
        _end = TimeSpan.Zero;
        if (EntityManager.TrySystem<RadarOverlaySystem>(out var radar))
            radar.Clear();
    }
}
