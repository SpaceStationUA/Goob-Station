// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Grab;
using Content.Shared._Pirate.Knowledge;
using Content.Shared._Shitmed.Cybernetics;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Body.Chips;

public sealed class OrganChipSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly INetManager _network = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public static readonly VerbCategory ChipsCategory = new(
        "verb-categories-organ-chips",
        "/Textures/_Pirate/Objects/Specific/brain_chips.rsi/icon.png");

    private EntityQuery<BypassInteractionChecksComponent> _bypassQuery;
    private EntityQuery<OrganChipComponent> _chipQuery;
    private EntityQuery<OrganChipContainerComponent> _containerQuery;
    private EntityQuery<KnowledgeGrantOnWearComponent> _grantQuery;

    public override void Initialize()
    {
        base.Initialize();

        _bypassQuery = GetEntityQuery<BypassInteractionChecksComponent>();
        _chipQuery = GetEntityQuery<OrganChipComponent>();
        _containerQuery = GetEntityQuery<OrganChipContainerComponent>();
        _grantQuery = GetEntityQuery<KnowledgeGrantOnWearComponent>();

        SubscribeLocalEvent<BrainComponent, MapInitEvent>(OnBrainMapInit);
        SubscribeLocalEvent<OrganChipContainerComponent, ComponentStartup>(OnContainerStartup);
        SubscribeLocalEvent<OrganChipContainerComponent, ComponentShutdown>(OnContainerShutdown);

        SubscribeLocalEvent<OrganChipContainerComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<OrganChipContainerComponent, EntInsertedIntoContainerMessage>(OnChipInserted);
        SubscribeLocalEvent<OrganChipContainerComponent, EntRemovedFromContainerMessage>(OnChipRemoved);

        SubscribeLocalEvent<OrganChipContainerComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<OrganChipContainerComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);

        SubscribeLocalEvent<OrganChipContainerComponent, InteractUsingEvent>(OnOrganInteractUsing);
        SubscribeLocalEvent<OrganChipContainerComponent, GetVerbsEvent<InteractionVerb>>(OnOrganGetVerbs);
        SubscribeLocalEvent<BodyComponent, InteractUsingEvent>(OnBodyInteractUsing);
        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<InteractionVerb>>(OnBodyGetVerbs);

        SubscribeLocalEvent<OrganChipContainerComponent, OrganChipInsertDoAfterEvent>(OnInsertDoAfter);
        SubscribeLocalEvent<OrganChipContainerComponent, OrganChipRemoveDoAfterEvent>(OnRemoveDoAfter);

        // Reapply contributions when the EMP recovers.
        SubscribeLocalEvent<OrganChipComponent, EmpPulseEvent>(OnChipEmpPulse);
        SubscribeLocalEvent<OrganChipComponent, EmpDisabledRemovedEvent>(OnChipEmpRecovered);

        // Reconcile after knowledge-store transfers that do not emit chip events.
        SubscribeLocalEvent<KnowledgeStoreMovedEvent>(OnStoreMoved);
    }

    private void OnStoreMoved(ref KnowledgeStoreMovedEvent args)
    {
        ReconcileInstalledChipModifiers(args.Source);
        ReconcileInstalledChipModifiers(args.Destination);
    }

    #region Container lifecycle

    private void OnBrainMapInit(Entity<BrainComponent> ent, ref MapInitEvent args)
    {
        EnsureContainer((ent.Owner, EnsureComp<OrganChipContainerComponent>(ent.Owner)));
    }

    private void OnContainerStartup(Entity<OrganChipContainerComponent> ent, ref ComponentStartup args)
    {
        EnsureContainer(ent);
    }

    private void OnContainerShutdown(Entity<OrganChipContainerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Container is { } container)
            _containers.ShutdownContainer(container);
    }

    private Container EnsureContainer(Entity<OrganChipContainerComponent> ent)
    {
        return ent.Comp.Container ??=
            _containers.EnsureContainer<Container>(ent.Owner, OrganChipContainerComponent.ContainerId);
    }

    #endregion

    #region Insertion validation

    private void OnInsertAttempt(Entity<OrganChipContainerComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || args.Container != ent.Comp.Container)
            return;

        if (!CanInsertChip(ent, args.EntityUid, out _))
            args.Cancel();
    }

    public bool CanInsertChip(Entity<OrganChipContainerComponent> ent, EntityUid chip, out string? reason)
    {
        reason = null;

        if (!_chipQuery.TryComp(chip, out var comp))
        {
            reason = "organ-chip-not-a-chip";
            return false;
        }

        if (TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(chip))
        {
            reason = "organ-chip-incompatible";
            return false;
        }

        if (_whitelist.IsWhitelistFail(comp.Whitelist, ent.Owner))
        {
            reason = "organ-chip-incompatible";
            return false;
        }

        var container = EnsureContainer(ent);
        if (container.Count >= ent.Comp.Limit)
        {
            reason = "organ-chip-no-room";
            return false;
        }

        if (Prototype(chip)?.ID is not { } id)
            return true;

        foreach (var installed in container.ContainedEntities)
        {
            if (Prototype(installed)?.ID != id)
                continue;

            reason = "organ-chip-duplicate";
            return false;
        }

        return true;
    }

    private void OnChipInserted(Entity<OrganChipContainerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container != ent.Comp.Container || !_chipQuery.TryComp(args.Entity, out var chip))
            return;

        chip.Organ = ent.Owner;
        Dirty(args.Entity, chip);

        var body = GetOrganBody(ent.Owner);
        var ev = new OrganChipInsertedEvent(ent.Owner, body);
        RaiseLocalEvent(args.Entity, ref ev);

        if (body is { } bodyUid)
            ReconcileInstalledChipModifiers(bodyUid);
    }

    private void OnChipRemoved(Entity<OrganChipContainerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container != ent.Comp.Container || !_chipQuery.TryComp(args.Entity, out var chip))
            return;

        var body = GetOrganBody(ent.Owner);
        var ev = new OrganChipRemovedEvent(ent.Owner, body);
        RaiseLocalEvent(args.Entity, ref ev);

        chip.Organ = null;
        Dirty(args.Entity, chip);

        if (body is { } bodyUid)
            ReconcileInstalledChipModifiers(bodyUid);
    }

    private void OnOrganAdded(Entity<OrganChipContainerComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (TerminatingOrDeleted(args.Body))
            return;

        var ev = new OrganChipInsertedEvent(ent.Owner, args.Body);
        RelayChips(ent, ref ev);
        ReconcileInstalledChipModifiers(args.Body);
    }

    private void OnOrganRemoved(Entity<OrganChipContainerComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        var ev = new OrganChipRemovedEvent(ent.Owner, args.OldBody);
        RelayChips(ent, ref ev);

        // Clear through the organ; the body link may already be gone.
        if (ent.Comp.Container is { } container && _knowledge.GetContainer(ent.Owner) is { } store)
        {
            foreach (var chip in container.ContainedEntities.ToArray())
                _knowledge.RemoveAllTemporaryModifiers(store, chip);
        }

        if (!TerminatingOrDeleted(args.OldBody))
            ReconcileInstalledChipModifiers(args.OldBody);
    }

    private void OnChipEmpPulse(Entity<OrganChipComponent> ent, ref EmpPulseEvent args)
    {
        // Cybernetics event ordering is undefined; keep both handlers idempotent.
        SetChipDisabled(ent.Owner, true);
        args.Affected = true;
        args.Disabled = true;
        RefreshChipContribution(ent);
    }

    private void OnChipEmpRecovered(Entity<OrganChipComponent> ent, ref EmpDisabledRemovedEvent args)
    {
        SetChipDisabled(ent.Owner, false);
        RefreshChipContribution(ent);
    }

    private void SetChipDisabled(EntityUid chip, bool disabled)
    {
        if (!TryComp<CyberneticsComponent>(chip, out var cybernetics) || cybernetics.Disabled == disabled)
            return;

        cybernetics.Disabled = disabled;
        Dirty(chip, cybernetics);
    }

    private void RefreshChipContribution(Entity<OrganChipComponent> ent)
    {
        if (ent.Comp.Organ is not { } organ || GetOrganBody(organ) is not { } body)
            return;

        ApplyChipModifiers(ent.Owner, body);
    }

    private void RelayChips<T>(Entity<OrganChipContainerComponent> ent, ref T args) where T : notnull
    {
        if (ent.Comp.Container is not { } container)
            return;

        foreach (var chip in container.ContainedEntities.ToArray())
            RaiseLocalEvent(chip, ref args);
    }

    #endregion

    #region Reconciliation

    // Rebuild chip contributions for the body's current organs.
    public void ReconcileInstalledChipModifiers(EntityUid body)
    {
        if (!_network.IsServer || TerminatingOrDeleted(body))
            return;

        var installed = new HashSet<EntityUid>();
        Entity<KnowledgeContainerComponent>? brainStore = null;

        // A bodyless holder can still have stale chip contributions.
        if (HasComp<BodyComponent>(body))
        {
            foreach (var brain in _body.GetBodyOrganEntityComps<BrainComponent>(body))
            {
                brainStore ??= _knowledge.GetContainer(brain.Owner);

                if (!_containerQuery.TryComp(brain.Owner, out var container) || container.Container is not { } chips)
                    continue;

                foreach (var chip in chips.ContainedEntities)
                    installed.Add(chip);
            }
        }
        else if (_containerQuery.TryComp(body, out var ownChips) && ownChips.Container is { } ownContainer)
        {
            brainStore = _knowledge.GetContainer(body);
            foreach (var chip in ownContainer.ContainedEntities)
                installed.Add(chip);
        }

        // Allow organ events before the body-store link is established.
        if ((_knowledge.GetContainer(body) ?? brainStore) is not { } store)
            return;

        _knowledge.PruneDeletedModifierSources(store);

        foreach (var uid in store.Comp.Knowledge.Values.ToArray())
        {
            if (!TryComp<KnowledgeTemporaryModifierSourcesComponent>(uid, out var sources))
                continue;

            foreach (var source in sources.EntitySources.Keys.ToArray())
            {
                if (!_chipQuery.HasComp(source) || installed.Contains(source))
                    continue;

                sources.EntitySources.Remove(source);
                if (TryComp<KnowledgeComponent>(uid, out var knowledge))
                    _knowledge.RecalculateTemporaryLevel((uid, knowledge));
            }
        }

        foreach (var chip in installed)
            ApplyChipModifiers(chip, body, store);
    }

    private void ApplyChipModifiers(EntityUid chip, EntityUid body, Entity<KnowledgeContainerComponent>? store = null)
    {
        if (!_grantQuery.TryComp(chip, out var grant))
            return;

        if (store is { } resolved)
            _knowledge.ApplyWearModifiers(body, resolved, (chip, grant));
        else
            _knowledge.ApplyWearModifiers(body, (chip, grant));
    }

    #endregion

    #region Interaction

    private void OnOrganInteractUsing(Entity<OrganChipContainerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_chipQuery.TryComp(args.Used, out var chip))
            return;

        if (_whitelist.IsWhitelistFail(chip.Whitelist, ent.Owner))
        {
            _popup.PopupClient(
                Loc.GetString("organ-chip-incompatible", ("chip", Name(args.Used)), ("organ", Name(ent.Owner))),
                ent.Owner,
                args.User);
            return;
        }

        args.Handled = true;
        StartInsertingChip(ent, args.Used, args.User);
    }

    private void OnBodyInteractUsing(Entity<BodyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_chipQuery.TryComp(args.Used, out var chip))
            return;

        if (FindChipHost(ent.Owner, chip) is not { } host)
            return;

        args.Handled = true;
        StartInsertingChip(host, args.Used, args.User);
    }

    private void OnOrganGetVerbs(Entity<OrganChipContainerComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        AddChipVerbs(ent, ref args);
    }

    private void OnBodyGetVerbs(Entity<BodyComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        foreach (var brain in _body.GetBodyOrganEntityComps<BrainComponent>(ent.Owner))
        {
            if (_containerQuery.TryComp(brain.Owner, out var container))
                AddChipVerbs((brain.Owner, container), ref args);
        }
    }

    private void AddChipVerbs(Entity<OrganChipContainerComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract || ent.Comp.Container is not { } container)
            return;

        var organName = Name(ent.Owner);
        if (container.Count == 0)
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("organ-chip-verb-none", ("organ", organName)),
                Category = ChipsCategory,
                Disabled = true,
            });
            return;
        }

        var user = args.User;
        var isSelf = GetOrganBody(ent.Owner) == user;
        var isAdmin = _bypassQuery.HasComp(user);
        var known = isSelf || isAdmin;

        var index = 0;
        foreach (var chip in container.ContainedEntities)
        {
            if (!_chipQuery.TryComp(chip, out var comp))
                continue;

            var target = chip;
            var canRemove = comp.CanRemove;
            if (!comp.CanSelfRemove)
                canRemove &= !isSelf;
            canRemove |= isAdmin;

            index++;
            args.Verbs.Add(new InteractionVerb
            {
                Text = known
                    ? Loc.GetString("organ-chip-verb-remove-known", ("chip", Name(chip)))
                    : Loc.GetString("organ-chip-verb-remove-unknown", ("organ", organName), ("index", index)),
                Category = ChipsCategory,
                Disabled = !canRemove,
                Act = () => StartRemovingChip(ent, target, user),
            });
        }
    }

    private void StartInsertingChip(Entity<OrganChipContainerComponent> ent, EntityUid chip, EntityUid user)
    {
        if (GetDelay(ent.Owner, chip, user, out var body, popup: true) is not { } delay)
            return;

        if (!CanInsertChip(ent, chip, out var reason))
        {
            _popup.PopupClient(
                Loc.GetString(reason ?? "organ-chip-incompatible",
                    ("chip", Name(chip)),
                    ("organ", Name(ent.Owner))),
                user,
                user);
            return;
        }

        PopupStart(ent.Owner, user, body, "insert");

        // Target the operation site, not the chip held by the operator.
        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            delay,
            new OrganChipInsertDoAfterEvent(GetNetEntity(chip)),
            eventTarget: ent.Owner,
            target: body ?? ent.Owner,
            used: chip)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = false,
        });
    }

    private void StartRemovingChip(Entity<OrganChipContainerComponent> ent, EntityUid chip, EntityUid user)
    {
        if (!_chipQuery.TryComp(chip, out var comp) ||
            GetDelay(ent.Owner, chip, user, out var body, popup: true) is not { } delay)
            return;

        if (!CanRemoveChip(chip, user, body))
        {
            _popup.PopupClient(
                Loc.GetString(comp.CanRemove ? "organ-chip-no-self-removal" : "organ-chip-not-removable"),
                user,
                user);
            return;
        }

        PopupStart(ent.Owner, user, body, "remove");

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            delay,
            new OrganChipRemoveDoAfterEvent(GetNetEntity(chip)),
            eventTarget: ent.Owner,
            target: body ?? ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = false,
        });
    }

    private void PopupStart(EntityUid organ, EntityUid user, EntityUid? body, string verb)
    {
        var organName = Name(organ);

        if (body == user)
        {
            _popup.PopupClient(
                Loc.GetString($"organ-chip-{verb}-start-self", ("organ", organName)),
                user,
                user,
                PopupType.Medium);
            return;
        }

        if (body is { } target)
        {
            _popup.PopupClient(
                Loc.GetString($"organ-chip-{verb}-start-other",
                    ("target", Identity.Name(target, EntityManager)),
                    ("organ", organName)),
                user,
                user,
                PopupType.Large);
            _popup.PopupEntity(
                Loc.GetString($"organ-chip-{verb}-start-victim",
                    ("user", Identity.Name(user, EntityManager)),
                    ("organ", organName)),
                user,
                target,
                PopupType.LargeCaution);
            return;
        }

        _popup.PopupClient(
            Loc.GetString($"organ-chip-{verb}-start-loose", ("organ", organName)),
            user,
            user);
    }

    private TimeSpan? GetDelay(EntityUid organ, EntityUid chip, EntityUid user, out EntityUid? body, bool popup)
    {
        body = GetOrganBody(organ);

        if (!_chipQuery.TryComp(chip, out var comp))
            return null;

        if (!HasOperatingAuthority(organ, user, body, popup))
            return null;

        return body is { } target && target != user ? comp.LongDelay : comp.ShortDelay;
    }

    public bool HasOperatingAuthority(EntityUid organ, EntityUid user, EntityUid? body, bool popup = false)
    {
        if (TerminatingOrDeleted(organ) || TerminatingOrDeleted(user))
            return false;

        // Admins bypass range and grab checks.
        if (_bypassQuery.HasComp(user))
            return true;

        // Check the patient when installed, otherwise the loose organ.
        var site = body ?? organ;
        if (TerminatingOrDeleted(site) || !_interaction.InRangeUnobstructed(user, site))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("organ-chip-out-of-reach"), user, user);
            return false;
        }

        if (body is not { } target || target == user)
            return true;

        if (!TryComp<PullerComponent>(user, out var puller) || puller.Pulling != target ||
            GetGrabStage(user) < GrabStage.Hard)
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("organ-chip-need-hard-grab"), target, user);
            return false;
        }

        return true;
    }

    public bool CanRemoveChip(EntityUid chip, EntityUid user, EntityUid? body)
    {
        if (!_chipQuery.TryComp(chip, out var comp))
            return false;

        if (_bypassQuery.HasComp(user))
            return true;

        return comp.CanRemove && (comp.CanSelfRemove || body != user);
    }

    private GrabStage GetGrabStage(EntityUid puller)
    {
        var ev = new GetGrabStageEvent();
        RaiseLocalEvent(puller, ref ev);
        return ev.Stage;
    }

    private void OnInsertDoAfter(Entity<OrganChipContainerComponent> ent, ref OrganChipInsertDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Container is not { } container ||
            !TryGetEntity(args.Chip, out var chipUid))
            return;

        args.Handled = true;
        var chip = chipUid.Value;
        var user = args.User;

        // Revalidate movement, grabs, and chip ownership on completion.
        if (GetDelay(ent.Owner, chip, user, out _, popup: true) is null)
            return;

        if (!_hands.IsHolding(user, chip))
        {
            _popup.PopupClient(Loc.GetString("organ-chip-lost-chip"), user, user);
            return;
        }

        if (!CanInsertChip(ent, chip, out var reason))
        {
            _popup.PopupClient(
                Loc.GetString(reason ?? "organ-chip-incompatible",
                    ("chip", Name(chip)),
                    ("organ", Name(ent.Owner))),
                user,
                user);
            return;
        }

        if (!_containers.Insert(chip, container))
        {
            _popup.PopupClient(Loc.GetString("organ-chip-no-room", ("organ", Name(ent.Owner))), user, user);
            return;
        }

        _popup.PopupClient(Loc.GetString("organ-chip-insert-success", ("organ", Name(ent.Owner))), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} installed organ chip {ToPrettyString(chip):chip} into {ToPrettyString(ent.Owner):target}");
    }

    private void OnRemoveDoAfter(Entity<OrganChipContainerComponent> ent, ref OrganChipRemoveDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Container is not { } container ||
            !TryGetEntity(args.Chip, out var chipUid))
            return;

        args.Handled = true;
        var chip = chipUid.Value;
        var user = args.User;

        if (GetDelay(ent.Owner, chip, user, out var body, popup: true) is null)
            return;

        if (!CanRemoveChip(chip, user, body))
        {
            _popup.PopupClient(
                Loc.GetString(_chipQuery.Comp(chip).CanRemove
                    ? "organ-chip-no-self-removal"
                    : "organ-chip-not-removable"),
                user,
                user);
            return;
        }

        if (!container.Contains(chip) || !_containers.Remove(chip, container))
            return;

        _hands.TryPickupAnyHand(user, chip);
        _popup.PopupClient(Loc.GetString("organ-chip-remove-success", ("organ", Name(ent.Owner))), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} removed organ chip {ToPrettyString(chip):chip} from {ToPrettyString(ent.Owner):target}");
    }

    #endregion

    #region Public API

    public EntityUid? GetOrganBody(EntityUid organ)
        => CompOrNull<OrganComponent>(organ)?.Body;

    public Entity<OrganChipContainerComponent>? FindChipHost(EntityUid mob, OrganChipComponent chip)
    {
        if (!HasComp<BodyComponent>(mob))
        {
            return _containerQuery.TryComp(mob, out var own) && !_whitelist.IsWhitelistFail(chip.Whitelist, mob)
                ? (mob, own)
                : null;
        }

        foreach (var organ in _body.GetBodyOrganEntityComps<OrganChipContainerComponent>(mob))
        {
            if (!_whitelist.IsWhitelistFail(chip.Whitelist, organ.Owner))
                return (organ.Owner, organ.Comp1);
        }

        return null;
    }

    public bool InstallChip(EntityUid mob, [ForbidLiteral] EntProtoId<OrganChipComponent> id)
    {
        var chip = PredictedSpawnNextToOrDrop(id, mob);
        if (!_chipQuery.TryComp(chip, out var comp))
        {
            Log.Error($"Organ chip prototype {id} is missing OrganChipComponent.");
            PredictedDel(chip);
            return false;
        }

        if (FindChipHost(mob, comp) is not { } host)
        {
            Log.Error($"Tried to install chip {id} into {ToPrettyString(mob)}, which has no compatible organ.");
            PredictedDel(chip);
            return false;
        }

        if (!CanInsertChip(host, chip, out var reason) || !_containers.Insert(chip, EnsureContainer(host)))
        {
            // Full containers and duplicate requests are expected overlap.
            var message =
                $"Did not install chip {id} into {ToPrettyString(host.Owner)} of {ToPrettyString(mob)}: {reason ?? "insertion refused"}.";
            if (reason is "organ-chip-duplicate" or "organ-chip-no-room")
                Log.Debug(message);
            else
                Log.Error(message);

            PredictedDel(chip);
            return false;
        }

        return true;
    }

    #endregion
}

[Serializable, NetSerializable]
public sealed partial class OrganChipInsertDoAfterEvent : DoAfterEvent
{
    public NetEntity Chip;

    public OrganChipInsertDoAfterEvent(NetEntity chip)
    {
        Chip = chip;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class OrganChipRemoveDoAfterEvent : DoAfterEvent
{
    public NetEntity Chip;

    public OrganChipRemoveDoAfterEvent(NetEntity chip)
    {
        Chip = chip;
    }

    public override DoAfterEvent Clone() => this;
}
