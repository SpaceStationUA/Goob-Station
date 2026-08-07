// SPDX-FileCopyrightText: 2026 CMU
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Gameplay;
using Content.Client.Launcher;
using Robust.Client;
using Robust.Client.Player;
using Robust.Client.State;

namespace Content.Pirate.Client.Lobby;

public sealed class LauncherConnectingRecoverySystem : EntitySystem
{
    [Dependency] private readonly IBaseClient _baseClient = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        _baseClient.RunLevelChanged += OnRunLevelChanged;
        _playerManager.LocalPlayerAttached += OnLocalPlayerAttached;
    }

    public override void Shutdown()
    {
        _baseClient.RunLevelChanged -= OnRunLevelChanged;
        _playerManager.LocalPlayerAttached -= OnLocalPlayerAttached;

        base.Shutdown();
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (args.NewLevel == ClientRunLevel.InGame)
            EnsureGameplayStateForAttachedPlayer();
    }

    private void OnLocalPlayerAttached(EntityUid uid)
    {
        EnsureGameplayStateForAttachedPlayer();
    }

    private void EnsureGameplayStateForAttachedPlayer()
    {
        if (_baseClient.RunLevel != ClientRunLevel.InGame ||
            _playerManager.LocalEntity == null ||
            _stateManager.CurrentState is GameplayState)
        {
            return;
        }

        if (_stateManager.CurrentState is not LauncherConnecting)
            return;

        Log.Debug($"LauncherConnectingRecoverySystem: local player is attached while state is {_stateManager.CurrentState.GetType().Name}; requesting GameplayState");
        _stateManager.RequestStateChange<GameplayState>();
    }
}
