using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Pirate.StarlightSpecies.Doll;

/// <summary>Starlight shell breakage, removal and regrowth adapted to Shitmed organ slots.</summary>
public sealed class DollShellSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShellComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ShellComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ShellComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<ShellComponent, DamageModifyEvent>(OnModifyDamage);
        SubscribeLocalEvent<OrganShellComponent, OrganAddedToBodyEvent>(OnAdded);
        SubscribeLocalEvent<OrganShellComponent, OrganRemovedFromBodyEvent>(OnRemoved);
        SubscribeLocalEvent<SnapShellPieceEvent>(OnSnap);
    }

    private List<EntityUid> Pieces(EntityUid body) =>
        _body.GetBodyOrgans(body).Where(o => HasComp<OrganShellComponent>(o.Id)).Select(o => o.Id).ToList();

    private void OnInit(Entity<ShellComponent> ent, ref MapInitEvent args) => Refresh(ent);

    private void OnShutdown(Entity<ShellComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.GenerateShellPieceActionEntity);
        _alerts.ClearAlert(ent.Owner, ent.Comp.ShellAlert);
    }

    private void Refresh(Entity<ShellComponent> ent)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent))
            return;
        var count = Pieces(ent).Count;
        _alerts.ShowAlert(ent.Owner, ent.Comp.ShellAlert, (short) Math.Clamp(count / 2, 0, 5));
        if (count > 0)
            _actions.AddAction(ent, ref ent.Comp.GenerateShellPieceActionEntity, ent.Comp.GenerateShellPieceAction);
        else
            _actions.RemoveAction(ent.Owner, ent.Comp.GenerateShellPieceActionEntity);
    }

    private void OnModifyDamage(EntityUid uid, ShellComponent component, DamageModifyEvent args)
    {
        Entity<ShellComponent> ent = (uid, component);
        var modifiers = new Dictionary<ProtoId<DamageTypePrototype>, float>();
        foreach (var piece in Pieces(ent))
        {
            foreach (var (type, coefficient) in Comp<OrganShellComponent>(piece).Modifiers.Coefficients)
                modifiers[type] = modifiers.GetValueOrDefault(type) + coefficient;
        }
        DamageModifierSetPrototype? baseline = null;
        if (TryComp<DamageableComponent>(ent, out var damageable))
            _prototypes.Resolve(damageable.DamageModifierSetId, out baseline);
        foreach (var (type, additive) in modifiers)
        {
            if (!args.Damage.DamageDict.TryGetValue(type, out var amount) || amount <= 0)
                continue;
            var coefficient = baseline?.Coefficients.GetValueOrDefault(type, 1f) ?? 1f;
            if (coefficient > 0)
                args.Damage.DamageDict[type] = amount * MathF.Max(0, coefficient + additive) / coefficient;
        }
    }

    private void OnDamaged(EntityUid uid, ShellComponent component, DamageChangedEvent args)
    {
        Entity<ShellComponent> ent = (uid, component);
        if (_net.IsClient || !args.DamageIncreased || args.Origin == ent.Owner || args.DamageDelta == null)
            return;
        if (!args.DamageDelta.TryGetDamageInGroup(_prototypes.Index(ent.Comp.DestroyedBy), out var damage) ||
            damage <= ent.Comp.Hardness || ent.Comp.Stability == 0 ||
            !_random.Prob(Math.Clamp(damage.Float() / ent.Comp.Stability, 0f, 100f) * 0.01f))
            return;
        var pieces = Pieces(ent);
        if (pieces.Count == 0)
            return;
        var piece = _random.Pick(pieces);
        if (!_body.RemoveOrgan(piece))
            return;
        _audio.PlayPredicted(ent.Comp.ShellBreakSound, ent, null);
        QueueDel(piece);
    }

    private void OnSnap(SnapShellPieceEvent args)
    {
        if (_net.IsClient || args.Handled || !TryComp<ShellComponent>(args.Performer, out var shell))
            return;
        var pieces = Pieces(args.Performer);
        if (pieces.Count == 0)
            return;
        var piece = _random.Pick(pieces);
        if (args.RequiresFreeHand && !_hands.CanPickupAnyHand(args.Performer, piece))
            return;
        if (args.DeShell)
        {
            foreach (var fragment in pieces)
                _body.RemoveOrgan(fragment);
        }
        else if (_body.RemoveOrgan(piece))
        {
            if (args.RequiresFreeHand)
                _hands.TryPickupAnyHand(args.Performer, piece);
        }
        else
            return;
        if (args.SelfDamage != null)
            _damage.TryChangeDamage(args.Performer, args.SelfDamage, true, origin: args.Performer);
        Refresh((args.Performer, shell));
        args.Handled = true;
    }

    private void OnRemoved(Entity<OrganShellComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (_net.IsClient || !TryComp<ShellComponent>(args.OldBody, out var shell))
            return;
        if (TryComp<BodyPartComponent>(args.OldPart, out var part) &&
            TryComp<HumanoidAppearanceComponent>(args.OldBody, out var appearance) &&
            part.ToHumanoidLayers() is { } layer)
        {
            if (shell.OriginalMarkings.Count == 0)
                shell.OriginalMarkings.AddRange(appearance.MarkingSet.Markings.Values.SelectMany(m => m).Select(m => new Marking(m)));
            foreach (var markings in appearance.MarkingSet.Markings.Values)
            {
                foreach (var marking in markings.ToArray())
                {
                    if (!_prototypes.TryIndex<MarkingPrototype>(marking.MarkingId, out var prototype) || prototype.BodyPart != layer)
                        continue;
                    ent.Comp.StoredMarkings.Add(new Marking(marking));
                    markings.Remove(marking);
                }
            }
            Dirty(args.OldBody, appearance);
            var changed = new HumanoidMarkingsChangedEvent();
            RaiseLocalEvent(args.OldBody, ref changed);
        }
        Refresh((args.OldBody, shell));
    }

    private void OnAdded(Entity<OrganShellComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (_net.IsClient || !TryComp<ShellComponent>(args.Body, out var shell))
            return;
        if (TryComp<BodyPartComponent>(args.Part, out var part) && part.ToHumanoidLayers() is { } layer)
        {
            var markings = ent.Comp.StoredMarkings.Count > 0 ? ent.Comp.StoredMarkings : shell.OriginalMarkings;
            foreach (var marking in markings)
            {
                if (_prototypes.TryIndex<MarkingPrototype>(marking.MarkingId, out var prototype) && prototype.BodyPart == layer)
                    _humanoid.AddMarking(args.Body, marking.MarkingId, marking.MarkingColors, forced: true);
            }
            ent.Comp.StoredMarkings.Clear();
        }
        Refresh((args.Body, shell));
    }

    public void Regrow(EntityUid body, EntProtoId prototype)
    {
        if (_net.IsClient)
            return;
        var slots = new List<BaseContainer>();
        foreach (var part in _body.GetBodyChildren(body))
        {
            if (_containers.TryGetContainer(part.Id, SharedBodySystem.GetOrganContainerId("shell"), out var container) && container.Count == 0)
                slots.Add(container);
        }
        if (slots.Count == 0)
            return;
        var slot = _random.Pick(slots);
        var piece = Spawn(prototype);
        if (!_containers.Insert(piece, slot))
            QueueDel(piece);
    }
}
