// Pirate: Starlight slime storage using native storage/stripping APIs.
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Strip;
using Content.Shared.Strip.Components;
using Content.Shared.Verbs;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StarlightSpecies.Slime;

[RegisterComponent, NetworkedComponent]
public sealed partial class PrivateStorageComponent : Component
{
    [DataField] public TimeSpan AccessDelay = TimeSpan.FromSeconds(7);
    [DataField] public LocId AccessPopup = "action-storage-accessing-outsider";
}

[Serializable, NetSerializable]
public sealed partial class PrivateStorageDoAfterEvent : SimpleDoAfterEvent;

public sealed class PrivateStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PrivateStorageComponent, GetVerbsEvent<ActivationVerb>>(OnVerbs);
        SubscribeLocalEvent<PrivateStorageComponent, ActivateInWorldEvent>(OnActivate,
            after: [typeof(SharedStrippableSystem)]);
        SubscribeLocalEvent<PrivateStorageComponent, PrivateStorageDoAfterEvent>(OnDoAfter);
    }

    private void OnVerbs(EntityUid uid, PrivateStorageComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<StorageComponent>(uid))
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString(_ui.IsUiOpen(uid, StorageComponent.StorageUiKey.Key, args.User)
                ? "comp-storage-verb-close-storage" : "comp-storage-verb-open-storage"),
            Act = () => Toggle(uid, comp, args.User),
        });
    }

    private void OnActivate(EntityUid uid, PrivateStorageComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || args.User != uid)
            return;
        Toggle(uid, comp, args.User);
        args.Handled = true;
    }

    private void Toggle(EntityUid uid, PrivateStorageComponent comp, EntityUid user)
    {
        if (!_interaction.InRangeUnobstructed(user, uid))
            return;
        if (_ui.IsUiOpen(uid, StorageComponent.StorageUiKey.Key, user))
        {
            _ui.CloseUi(uid, StorageComponent.StorageUiKey.Key, user);
            return;
        }
        if (user == uid)
        {
            _storage.OpenStorageUI(uid, user, silent: false);
            return;
        }

        var beforeStrip = new BeforeStripEvent(comp.AccessDelay);
        RaiseLocalEvent(user, ref beforeStrip);
        var args = new DoAfterArgs(EntityManager, user, beforeStrip.Time,
            new PrivateStorageDoAfterEvent(), uid, target: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            Hidden = beforeStrip.Stealth,
        };
        if (_doAfter.TryStartDoAfter(args) && !beforeStrip.Stealth)
            _popup.PopupEntity(Loc.GetString(comp.AccessPopup, ("user", user)), uid, uid, PopupType.Medium);
    }

    private void OnDoAfter(Entity<PrivateStorageComponent> ent, ref PrivateStorageDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_interaction.InRangeUnobstructed(args.User, ent.Owner))
            return;
        _storage.OpenStorageUI(ent, args.User, silent: false);
        args.Handled = true;
    }
}
