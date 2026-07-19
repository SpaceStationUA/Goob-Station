// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Ranching;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Ranching;

public sealed class RanchingInteractionSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly RanchingAgeingSystem _ageing = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly List<(EntityUid Owner, EntProtoId Replacement)> _pendingReplacements = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TameableComponent, MapInitEvent>(OnTameableMapInit);
        SubscribeLocalEvent<TameableComponent, InteractionSuccessEvent>(OnTameableSuccess);
        SubscribeLocalEvent<TameableComponent, InteractionFailureEvent>(OnTameableFailure);

        SubscribeLocalEvent<PolymorphOnItemsGivenComponent, InteractUsingEvent>(OnPolymorphItemGiven);
        SubscribeLocalEvent<PlateableChickenComponent, InteractUsingEvent>(OnPlateChicken);
        SubscribeLocalEvent<ReplaceOnItemEquippedComponent, ClothingDidEquippedEvent>(OnItemEquipped);
        SubscribeLocalEvent<DealDamageOnPulledComponent, BeingPulledAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<BounceableComponent, StartCollideEvent>(OnBounce);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var (owner, replacement) in _pendingReplacements)
        {
            if (!TerminatingOrDeleted(owner))
                _ageing.TransformEntity(owner, replacement);
        }

        _pendingReplacements.Clear();
    }

    private void OnTameableMapInit(Entity<TameableComponent> ent, ref MapInitEvent args)
    {
        var minimum = Math.Min(ent.Comp.MinPetsRequired, ent.Comp.MaxPetsRequired);
        var maximum = Math.Max(ent.Comp.MinPetsRequired, ent.Comp.MaxPetsRequired);
        ent.Comp.PetsRequired = _random.Next(minimum, maximum + 1);
    }

    private void OnTameableSuccess(Entity<TameableComponent> ent, ref InteractionSuccessEvent args)
    {
        ent.Comp.Pets++;
        if (ent.Comp.Pets < ent.Comp.PetsRequired)
            return;

        if (ent.Comp.ClearFactions)
            _factions.ClearFactions(ent.Owner);

        _factions.AddFaction(ent.Owner, ent.Comp.Faction);
        RemComp<TameableComponent>(ent);
    }

    private void OnTameableFailure(Entity<TameableComponent> ent, ref InteractionFailureEvent args)
    {
        ent.Comp.Pets = Math.Max(0, ent.Comp.Pets - 1);
    }

    private void OnPolymorphItemGiven(Entity<PolymorphOnItemsGivenComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.ReplacementEntities.Count == 0 || ent.Comp.Amount <= 0 ||
            _stacks.GetCount(args.Used) < ent.Comp.Amount ||
            _whitelist.IsWhitelistFail(ent.Comp.Whitelist, args.Used))
            return;

        if (!_stacks.TryUse(args.Used, ent.Comp.Amount))
            return;

        args.Handled = true;
        var replacement = _random.Pick(ent.Comp.ReplacementEntities);
        RemComp<PolymorphOnItemsGivenComponent>(ent);
        _ageing.TransformEntity(ent.Owner, replacement);
    }

    private void OnPlateChicken(Entity<PlateableChickenComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<PlateableChickenOreComponent>(args.Used, out var ore))
            return;

        args.Handled = true;
        EntityManager.AddComponents(ent.Owner, ore.Components);
        RemComp<PlateableChickenComponent>(ent);

        if (!_stacks.TryUse(args.Used, 1))
            QueueDel(args.Used);
    }

    private void OnItemEquipped(Entity<ReplaceOnItemEquippedComponent> ent, ref ClothingDidEquippedEvent args)
    {
        if ((args.Clothing.Comp.Slots & ent.Comp.Slots) == 0 ||
            !_tags.HasAllTags(args.Clothing.Owner, ent.Comp.RequiredTags))
            return;

        _pendingReplacements.Add((ent.Owner, ent.Comp.Ent));
    }

    private void OnPullAttempt(Entity<DealDamageOnPulledComponent> ent, ref BeingPulledAttemptEvent args)
    {
        _damage.TryChangeDamage(ent.Owner, ent.Comp.Damage, origin: args.Puller);
    }

    private void OnBounce(Entity<BounceableComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<BounceableComponent>(args.OtherEntity) || ent.Comp.NextValidBounceTime > _timing.CurTime)
            return;

        ent.Comp.NextValidBounceTime = _timing.CurTime + ent.Comp.GraceTime;
        ent.Comp.TimesBounced++;

        if (ent.Owner.Id < args.OtherEntity.Id)
            return;

        _audio.PlayPvs(ent.Comp.BounceSound, ent.Owner);
        _effects.ApplyEffects(ent.Owner, ent.Comp.Effects, user: ent.Owner);

        if (ent.Comp.TimesBounced < ent.Comp.BouncesRequired)
            return;

        ent.Comp.TimesBounced = 0;
        SpawnNextToOrDrop(ent.Comp.EntityToSpawn, ent.Owner);
    }
}
