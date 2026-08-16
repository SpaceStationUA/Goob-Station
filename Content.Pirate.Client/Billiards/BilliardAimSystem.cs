// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Billiards;

public sealed class BilliardAimSystem : EntitySystem
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private BilliardAimOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new BilliardAimOverlay(
            EntityManager,
            _playerManager,
            _inputManager,
            _uiManager,
            _stateManager);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlayManager.RemoveOverlay(_overlay);

        base.Shutdown();
    }
}
