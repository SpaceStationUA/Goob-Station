// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Suspicion.SuspicionGameRule;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared._Pirate.Suspicion;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client._Pirate.Suspicion.UserInterface;

public sealed partial class SSSStatusUISystem : EntitySystem
{
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private bool _healthUpdatePending;
    private SSSStatusUIController Hud => _ui.GetUIController<SSSStatusUIController>();

    public event Action<Entity<DamageableComponent>>? OnPlayerDamageChanged;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<SuspicionHudClosed>(_ => Hud.ResetHud());
        SubscribeNetworkEvent<SuspicionRuleTimerUpdate>(ev => Hud.UpdateTimer(ev.EndTime));
        SubscribeNetworkEvent<SuspicionRulePreroundStarted>(ev => Hud.ShowRoundStatus(SuspicionGameState.Preparing, ev.PreroundEndTime));
        SubscribeNetworkEvent<SuspicionRuleUpdateRole>(ev => Hud.UpdateRole(ev));
        SubscribeNetworkEvent<SuspicionRulePlayerSpawn>(ev => Hud.ShowRoundStatus(ev.GameState, ev.EndTime));
        SubscribeLocalEvent<SuspicionPlayerComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, SuspicionPlayerComponent participant, DamageChangedEvent args)
    {
        if (uid != _playerManager.LocalEntity || _healthUpdatePending)
            return;

        // The body and its parts may receive their damage states in the same network update.
        _healthUpdatePending = true;
        Timer.Spawn(TimeSpan.Zero, () =>
        {
            _healthUpdatePending = false;
            if (uid == _playerManager.LocalEntity && TryComp<DamageableComponent>(uid, out var health))
                OnPlayerDamageChanged?.Invoke((uid, health));
        });
    }
}
