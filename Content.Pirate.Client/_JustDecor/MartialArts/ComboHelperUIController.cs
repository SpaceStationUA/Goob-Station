using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Goobstation.Common.MartialArts;
using Content.Pirate.Shared._JustDecor.MartialArts.Components;
using Robust.Shared.Player;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Client._JustDecor.MartialArts;

/// <summary>
/// UI контролер для керування ComboHelperWidget.
/// Слухає зміни стану компонента та оновлює віджет відповідно.
/// </summary>
public sealed class ComboHelperUIController : UIController, IOnStateEntered<GameplayState>
{
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayLoad.OnScreenLoad += OnScreenLoad;
        gameplayLoad.OnScreenUnload += OnScreenUnload;

        EntityManager.EventBus.SubscribeLocalEvent<ComboHelperComponent, AfterAutoHandleStateEvent>(OnHandleState);
        EntityManager.EventBus.SubscribeLocalEvent<ComboHelperComponent, ComponentAdd>(OnComponentAdd);
        EntityManager.EventBus.SubscribeLocalEvent<ComboHelperComponent, ComponentRemove>(OnComponentRemove);
        EntityManager.EventBus.SubscribeLocalEvent<MartialArtsKnowledgeComponent, AfterAutoHandleStateEvent>(OnMartialArtsState);
        EntityManager.EventBus.SubscribeLocalEvent<MartialArtsKnowledgeComponent, ComponentAdd>(OnMartialArtsAdded);
        EntityManager.EventBus.SubscribeLocalEvent<MartialArtsKnowledgeComponent, ComponentRemove>(OnMartialArtsRemoved);

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnComponentAdd(EntityUid uid, ComboHelperComponent component, ComponentAdd args)
    {
        if (uid != _player.LocalEntity)
            return;

        UpdateWidget();
    }

    private void OnComponentRemove(EntityUid uid, ComboHelperComponent component, ComponentRemove args)
    {
        if (uid != _player.LocalEntity)
            return;
        RemoveWidget();
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent ev)
    {
        UpdateWidget();
    }

    private void OnHandleState(EntityUid uid, ComboHelperComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (uid != _player.LocalEntity)
            return;

        UpdateWidget();
    }

    private void OnMartialArtsAdded(EntityUid uid, MartialArtsKnowledgeComponent component, ComponentAdd args)
    {
        if (uid != _player.LocalEntity)
            return;

        UpdateWidget();
    }

    private void OnMartialArtsState(EntityUid uid, MartialArtsKnowledgeComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (uid != _player.LocalEntity)
            return;

        UpdateWidget();
    }

    private void OnMartialArtsRemoved(EntityUid uid, MartialArtsKnowledgeComponent component, ComponentRemove args)
    {
        if (uid != _player.LocalEntity)
            return;

        UpdateWidget();
    }

    private void OnScreenLoad()
    {
        UpdateWidget();
    }

    private void OnScreenUnload()
    {
        RemoveWidget();
    }

    public void OnStateEntered(GameplayState state)
    {
        UpdateWidget();
    }

    private void UpdateWidget()
    {
        if (_player.LocalEntity is not { } player)
        {
            RemoveWidget();
            return;
        }

        if (!EntityManager.TryGetComponent<ComboHelperComponent>(player, out var component))
        {
            RemoveWidget();
            return;
        }

        var screen = UIManager.ActiveScreen;
        if (screen == null)
        {
            RemoveWidget();
            return;
        }

        var widget = screen.GetOrAddWidget<ComboHelperWidget>();
        if (widget == null)
            return;

        widget.SetPositionLast();
        widget.UpdateFromComponent(component);
    }

    private void RemoveWidget()
    {
        var screen = UIManager.ActiveScreen;
        if (screen == null)
            return;

        if (screen.TryGetWidget<ComboHelperWidget>(out _))
        {
            screen.RemoveWidget<ComboHelperWidget>();
        }
    }
}
