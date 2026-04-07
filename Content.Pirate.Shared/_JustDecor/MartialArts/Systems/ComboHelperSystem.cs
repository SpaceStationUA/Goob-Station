using Content.Shared.Actions;
using Content.Shared.Interaction.Events;
using Content.Pirate.Shared._JustDecor.MartialArts.Components;
using Content.Pirate.Shared._JustDecor.MartialArts.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared._JustDecor.MartialArts.Systems;

/// <summary>
/// Shared система для керування ComboHelper компонентом.
/// Відповідає за toggle функціональність та синхронізацію стану.
/// </summary>
public sealed class ComboHelperSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComboHelperComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ComboHelperComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ComboHelperComponent, ComboHelperToggleEvent>(OnToggle);
        SubscribeLocalEvent<GrantComboHelperComponent, UseInHandEvent>(OnGrantUse);
    }

    private void OnStartup(EntityUid uid, ComboHelperComponent component, ComponentStartup args)
    {
        if (component.ToggleAction == null)
        {
            _actions.AddAction(uid, ref component.ToggleAction, "ActionCqcComboHelperToggle");
            Dirty(uid, component);
        }
    }

    private void OnShutdown(EntityUid uid, ComboHelperComponent component, ComponentShutdown args)
    {
        if (component.ToggleAction != null)
        {
            _actions.RemoveAction(uid, component.ToggleAction);
        }
    }

    private void OnToggle(EntityUid uid, ComboHelperComponent component, ComboHelperToggleEvent args)
    {
        if (!_netManager.IsServer)
            return;

        component.Enabled = !component.Enabled;

        Dirty(uid, component);
        args.Handled = true;
    }

    private void OnGrantUse(EntityUid uid, GrantComboHelperComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_netManager.IsServer)
            return;

        if (HasComp<ComboHelperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString(component.AlreadyKnownMessage), args.User, args.User);
            return;
        }

        EnsureComp<ComboHelperComponent>(args.User);
        _popup.PopupEntity(Loc.GetString(component.LearnMessage), args.User, args.User);

        var coords = Transform(args.User).Coordinates;
        _audio.PlayPvs(component.SoundOnUse, coords);

        if (component.MultiUse)
            return;

        QueueDel(uid);

        if (component.SpawnedProto != null)
            Spawn(component.SpawnedProto, coords);
    }

    /// <summary>
    /// Встановлює прототип helper для entity.
    /// Використовується зовнішніми системами (наприклад, martial arts системами).
    /// </summary>
    public void SetHelperPrototype(EntityUid uid, string prototypeId, ComboHelperComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
        {
            Logger.WarningS("ComboHelper", $"Tried to set helper prototype on {uid} without ComboHelperComponent");
            return;
        }

        if (!_proto.HasIndex<CqcComboHelperPrototype>(prototypeId))
        {
            Logger.WarningS("ComboHelper", $"Tried to set non-existent helper prototype on {uid}: {prototypeId}");
            return;
        }

        component.Prototype = prototypeId;

        Dirty(uid, component);
    }

    /// <summary>
    /// Вмикає або вимикає helper для entity.
    /// </summary>
    public void SetEnabled(EntityUid uid, bool enabled, ComboHelperComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
        {
            Logger.WarningS("ComboHelper", $"Tried to set enabled state on {uid} without ComboHelperComponent");
            return;
        }

        if (component.Enabled == enabled)
            return;

        component.Enabled = enabled;

        Dirty(uid, component);
    }
}
