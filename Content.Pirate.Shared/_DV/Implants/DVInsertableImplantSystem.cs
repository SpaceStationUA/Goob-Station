using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._DV.Implants;

/// <summary>
///     Allows items with <see cref="DVInsertableImplantComponent"/> (e.g. psionic crystals) to be
///     slotted into an empty implanter by hand, turning it into a single-use injector for that implant.
/// </summary>
public sealed class DVInsertableImplantSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DVInsertableImplantComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<DVInsertableImplantComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // Container manipulation must be server-authoritative.
        if (!_net.IsServer)
            return;

        if (!TryComp<ImplanterComponent>(args.Target, out var implanter))
            return;

        // Only empty implanters can be loaded.
        if (implanter.ImplanterSlot.HasItem)
            return;

        // The implanter slot is locked to prevent manual insertion/ejection, so we insert
        // straight into the underlying container to bypass that check.
        if (implanter.ImplanterSlot.ContainerSlot is not { } container
            || !_container.Insert(ent.Owner, container))
            return;

        // The implanter is now a single-use injector holding the implant.
        implanter.ImplantOnly = true;
        implanter.CurrentMode = ImplanterToggleMode.Inject;
        Dirty(args.Target.Value, implanter);

        _metaData.SetEntityName(args.Target.Value, Loc.GetString(ent.Comp.ImplanterName));
        _metaData.SetEntityDescription(args.Target.Value, Loc.GetString(ent.Comp.ImplanterDescription));

        _popup.PopupEntity(Loc.GetString("dv-implanter-crystal-inserted"), args.User, args.User);

        args.Handled = true;
    }
}
