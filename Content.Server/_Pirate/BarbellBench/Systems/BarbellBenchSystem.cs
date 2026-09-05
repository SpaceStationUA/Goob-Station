using Content.Server._Pirate.BarbellBench.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Pirate.BarbellBench;
using Content.Shared._Pirate.BarbellBench.Components;
using Content.Shared._Pirate.BarbellBench.Systems;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Alert;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server._Pirate.BarbellBench.Systems;

public sealed class BarbellBenchSystem : SharedBarbellBenchSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private readonly Dictionary<EntityUid, EntityUid> _activeRepPerformers = new();
    private readonly Dictionary<EntityUid, int> _repGenerations = new();

    private TimeSpan _nextSuffocationDamage = TimeSpan.Zero;

    private const float SuffocationDamageMultiplier = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BarbellBenchComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BarbellBenchComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BarbellBenchComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BarbellBenchComponent, BarbellBenchPerformRepEvent>(OnPerformRep);
        SubscribeLocalEvent<BarbellBenchComponent, EntInsertedIntoContainerMessage>(OnBarbellInserted);
        SubscribeLocalEvent<BarbellBenchComponent, EntRemovedFromContainerMessage>(OnBarbellRemoved);

        SubscribeLocalEvent<BarbellPinnedComponent, UnbuckleAttemptEvent>(OnPinnedUnbuckleAttempt);
        SubscribeLocalEvent<BarbellPinnedComponent, UnbuckleAlertEvent>(OnPinnedUnbuckleAlert);
        SubscribeLocalEvent<BarbellPinnedComponent, ComponentShutdown>(OnPinnedShutdown);
        SubscribeLocalEvent<StaminaComponent, EnterStaminaCritEvent>(OnStaminaCrit);
        SubscribeLocalEvent<BarbellPinnedComponent, BeforeStaminaDamageEvent>(OnPinnedStaminaDamage);

        SubscribeLocalEvent<BarbellPinnedComponent, InteractHandEvent>(OnPinnedInteractHand,
            before: new[] { typeof(SharedBuckleSystem) });

        SubscribeLocalEvent<BarbellPinnedComponent, GetVerbsEvent<InteractionVerb>>(OnPinnedGetVerbs,
            before: new[] { typeof(SharedBuckleSystem) });

        SubscribeLocalEvent<BuckleComponent, EntityTerminatingEvent>(OnBuckleTerminating);
        SubscribeLocalEvent<BarbellBenchComponent, UnstrappedEvent>(OnUnstrapped);
    }

    private void OnBarbellInserted(EntityUid uid, BarbellBenchComponent component, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.BarbellSlotId || !TryComp<BarbellLiftComponent>(args.Entity, out _))
            return;

        _appearance.SetData(uid, BarbellBenchVisuals.HasBarbell, true);

        if (TryComp<StrapComponent>(uid, out var strap))
        {
            foreach (var buckledEntity in strap.BuckledEntities)
            {
                _actionsSystem.RemoveProvidedActions(buckledEntity, uid);
                EntityUid? action = null;
                _actionsSystem.AddAction(buckledEntity, ref action, SharedBarbellBenchSystem.BarbellRepActionId, uid);
                if (action is { } actionUid)
                    _actionsSystem.SetUseDelay((actionUid, null), TimeSpan.FromSeconds(component.RepDuration));
            }
        }

        if (component.OverlayEntity is { } overlay && Exists(overlay))
        {
            var meta = MetaData(args.Entity);
            _metaData.SetEntityName(overlay, meta.EntityName);
            _metaData.SetEntityDescription(overlay, meta.EntityDescription);
        }
    }

    private void OnBarbellRemoved(EntityUid uid, BarbellBenchComponent component, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.BarbellSlotId)
            return;

        CancelRep(uid, component);
        ReleasePinnedUsers(uid, args.Entity);
        _appearance.SetData(uid, BarbellBenchVisuals.HasBarbell, false);
        if (TryComp<StrapComponent>(uid, out var strap))
            foreach (var buckled in strap.BuckledEntities)
                _actionsSystem.RemoveProvidedActions(buckled, uid);
    }

    private void OnStartup(EntityUid uid, BarbellBenchComponent component, ComponentStartup args)
    {
        EnsureOverlay(uid, component);
        UpdateAppearance(uid, component);
    }

    private void OnMapInit(EntityUid uid, BarbellBenchComponent component, MapInitEvent args)
    {
        EnsureOverlay(uid, component);
    }

    private void OnShutdown(EntityUid uid, BarbellBenchComponent component, ComponentShutdown args)
    {
        CancelRep(uid, component, updateAppearance: false);
        _repGenerations.Remove(uid);

        EntityUid? barbell = null;
        if (Container.TryGetContainer(uid, component.BarbellSlotId, out var barbellContainer) &&
            barbellContainer.Count > 0)
        {
            barbell = barbellContainer.ContainedEntities[0];
        }

        ReleasePinnedUsers(uid, barbell);

        if (component.OverlayEntity is { } overlay && Exists(overlay))
            Del(overlay);
        component.OverlayEntity = null;
    }

    private void ReleasePinnedUsers(EntityUid bench, EntityUid? barbell)
    {
        if (!TryComp<StrapComponent>(bench, out var strap))
            return;

        foreach (var buckled in strap.BuckledEntities)
        {
            if (!TryComp<BarbellPinnedComponent>(buckled, out var pinned) || pinned.Bench != bench)
                continue;

            if (barbell is { } barbellUid)
                _virtualItem.DeleteInHandsMatching(buckled, barbellUid);

            RemCompDeferred<BarbellPinnedComponent>(buckled);
            RemCompDeferred<ActiveBarbellPinnedComponent>(buckled);
            _stamina.ExitStamCrit(buckled);
            _statusEffects.TryRemoveStatusEffect(buckled, SharedStunSystem.StunId);
            RemCompDeferred<KnockedDownComponent>(buckled);
        }
    }

    private void EnsureOverlay(EntityUid uid, BarbellBenchComponent component)
    {
        if (component.OverlayEntity is { } existing && Exists(existing))
            return;

        var coords = Transform(uid).Coordinates;
        var overlay = Spawn(component.OverlayPrototype, coords);

        var overlayXform = Transform(overlay);
        _transform.SetParent(overlay, overlayXform, uid);
        _transform.SetCoordinates(overlay, overlayXform, new EntityCoordinates(uid, Vector2.Zero));
        overlayXform.LocalRotation = Angle.Zero;

        component.OverlayEntity = overlay;
        Dirty(uid, component);
    }

    private bool TryGetBarbell(EntityUid bench, BarbellBenchComponent component,
        out EntityUid barbell, out BarbellLiftComponent lift)
    {
        barbell = default;
        lift = default!;

        if (!Container.TryGetContainer(bench, component.BarbellSlotId, out var container) ||
            container.Count == 0)
            return false;

        barbell = container.ContainedEntities[0];
        if (!TryComp<BarbellLiftComponent>(barbell, out var barbellLift))
            return false;

        lift = barbellLift;
        return true;
    }

    private bool TryGetActiveRepBench(EntityUid performer, out EntityUid bench)
    {
        foreach (var (candidate, activePerformer) in _activeRepPerformers)
        {
            if (activePerformer != performer)
                continue;

            bench = candidate;
            return true;
        }

        bench = default;
        return false;
    }

    private bool IsCurrentRep(EntityUid bench, int generation)
    {
        return Exists(bench) &&
               TryComp<BarbellBenchComponent>(bench, out var component) &&
               component.IsPerformingRep &&
               _repGenerations.TryGetValue(bench, out var currentGeneration) &&
               currentGeneration == generation;
    }

    private void EndRep(EntityUid bench, int generation)
    {
        if (!IsCurrentRep(bench, generation))
            return;

        if (!TryComp<BarbellBenchComponent>(bench, out var component))
            return;

        component.IsPerformingRep = false;
        Dirty(bench, component);
        UpdateAppearance(bench, component);
        _activeRepPerformers.Remove(bench);
    }

    private void CancelRep(EntityUid bench, BarbellBenchComponent component, bool updateAppearance = true)
    {
        _repGenerations[bench] = _repGenerations.TryGetValue(bench, out var generation)
            ? generation + 1
            : 1;
        _activeRepPerformers.Remove(bench);

        if (!component.IsPerformingRep || !updateAppearance)
            return;

        component.IsPerformingRep = false;
        Dirty(bench, component);
        UpdateAppearance(bench, component);
    }

    private void DeletePinnedBarbell(EntityUid uid, BarbellPinnedComponent pinned)
    {
        if (pinned.Bench is not { } bench ||
            !Exists(bench) ||
            !TryComp<BarbellBenchComponent>(bench, out var benchComp) ||
            !TryGetBarbell(bench, benchComp, out var barbell, out _))
            return;

        _virtualItem.DeleteInHandsMatching(uid, barbell);
    }

    private void OnPerformRep(EntityUid uid, BarbellBenchComponent component, BarbellBenchPerformRepEvent args)
    {
        if (component.IsPerformingRep ||
            !TryGetBarbell(uid, component, out var barbell, out var lift) ||
            !TryComp<BuckleComponent>(args.Performer, out var buckle) ||
            buckle.BuckledTo != uid)
            return;

        _activeRepPerformers[uid] = args.Performer;
        var generation = _repGenerations.TryGetValue(uid, out var previousGeneration)
            ? previousGeneration + 1
            : 1;
        _repGenerations[uid] = generation;

        _stamina.TakeStaminaDamage(args.Performer, lift.StaminaCost, source: args.Performer, with: barbell, visual: true);
        _popup.PopupEntity(Loc.GetString(lift.EmoteLocSelf), args.Performer, args.Performer, PopupType.Medium);

        component.IsPerformingRep = true;
        Dirty(uid, component);
        UpdateAppearance(uid, component);

        var sound = new SoundCollectionSpecifier(component.RepSoundCollection);
        Timer.Spawn(TimeSpan.FromSeconds(component.RepSoundDelay), () =>
        {
            if (IsCurrentRep(uid, generation))
                _audio.PlayPvs(sound, uid);
        });

        Timer.Spawn(TimeSpan.FromSeconds(component.RepDuration), () =>
        {
            EndRep(uid, generation);
        });

        args.Handled = true;
    }

    private void OnStaminaCrit(EntityUid uid, StaminaComponent component, ref EnterStaminaCritEvent args)
    {
        if (!TryGetActiveRepBench(uid, out var benchUid))
            return;

        if (!TryComp<BuckleComponent>(uid, out var buckle) || buckle.BuckledTo == null)
            return;

        if (buckle.BuckledTo != benchUid || !TryComp<BarbellBenchComponent>(benchUid, out var bench))
            return;

        if (!TryGetBarbell(benchUid, bench, out var barbell, out _))
            return;

        if (HasComp<SiliconComponent>(uid))
            return;

        var pinnedComp = EnsureComp<BarbellPinnedComponent>(uid);
        pinnedComp.Bench = benchUid;
        pinnedComp.PinnedAt = _gameTiming.CurTime;
        pinnedComp.PinDurationSeconds = bench.PinDurationSeconds;
        Dirty(uid, pinnedComp);

        if (TryComp<StaminaComponent>(uid, out var staminaComp))
        {
            staminaComp.StunTime = TimeSpan.FromSeconds(bench.PinDurationSeconds);
            Dirty(uid, staminaComp);
        }

        _virtualItem.TrySpawnVirtualItemInHand(barbell, uid, out _, dropOthers: true);
        _virtualItem.TrySpawnVirtualItemInHand(barbell, uid, out _, dropOthers: false);

        _alerts.ClearAlertCategory(uid, SharedBuckleSystem.BuckledAlertCategory);

        _popup.PopupEntity(Loc.GetString("barbell-bench-pinned"), uid, uid, PopupType.LargeCaution);

        EnsureComp<ActiveBarbellPinnedComponent>(uid);
    }

    private void OnPinnedUnbuckleAttempt(EntityUid uid, BarbellPinnedComponent component, ref UnbuckleAttemptEvent args)
    {
        if (HasComp<SiliconComponent>(uid))
        {
            RemComp<BarbellPinnedComponent>(uid);
            RemComp<ActiveBarbellPinnedComponent>(uid);
            return;
        }

        if (args.User == uid)
        {
            var stunPassed = false;
            if (TryComp<KnockedDownComponent>(uid, out var knockedDown))
            {
                stunPassed = knockedDown.NextUpdate <= _gameTiming.CurTime;
            }

            var staminaRecovered = false;
            if (TryComp<StaminaComponent>(uid, out var staminaComp))
            {
                var currentStamina = _stamina.GetStaminaDamage(uid, staminaComp);
                staminaRecovered = currentStamina < staminaComp.CritThreshold && !staminaComp.Critical;
            }

            if (stunPassed || staminaRecovered)
            {
                DeletePinnedBarbell(uid, component);

                RemComp<BarbellPinnedComponent>(uid);
                RemComp<ActiveBarbellPinnedComponent>(uid);
                return;
            }

            args.Cancelled = true;
            if (args.Popup)
            {
                _popup.PopupEntity(Loc.GetString("barbell-bench-cannot-unbuckle"), uid, uid, PopupType.MediumCaution);
            }
            return;
        }

        DeletePinnedBarbell(uid, component);

        RemComp<BarbellPinnedComponent>(uid);
        RemComp<ActiveBarbellPinnedComponent>(uid);

        _stamina.ExitStamCrit(uid);
        _statusEffects.TryRemoveStatusEffect(uid, SharedStunSystem.StunId);
        RemComp<KnockedDownComponent>(uid);
    }

    private void OnPinnedInteractHand(EntityUid uid, BarbellPinnedComponent component, ref InteractHandEvent args)
    {
        if (args.User == uid)
        {
            var stunPassed = false;
            if (TryComp<KnockedDownComponent>(uid, out var knockedDown))
            {
                stunPassed = knockedDown.NextUpdate <= _gameTiming.CurTime;
            }

            if (!stunPassed)
            {
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("barbell-bench-pinned"), uid, uid, PopupType.MediumCaution);
            }
        }
    }

    private void OnPinnedUnbuckleAlert(EntityUid uid, BarbellPinnedComponent component, ref UnbuckleAlertEvent args)
    {
        args.Handled = true;
    }

    private void OnPinnedGetVerbs(EntityUid uid, BarbellPinnedComponent component, ref GetVerbsEvent<InteractionVerb> args)
    {
        var stunPassed = false;
        if (TryComp<KnockedDownComponent>(uid, out var knockedDown))
        {
            stunPassed = knockedDown.NextUpdate <= _gameTiming.CurTime;
        }

        if (!stunPassed)
        {
            var verbsToRemove = args.Verbs.Where(v => v.Category == VerbCategory.Unbuckle).ToList();
            foreach (var verb in verbsToRemove)
            {
                args.Verbs.Remove(verb);
            }
        }
    }

    private void OnPinnedShutdown(EntityUid uid, BarbellPinnedComponent component, ComponentShutdown args)
    {
        RemComp<ActiveBarbellPinnedComponent>(uid);
    }

    private void OnPinnedStaminaDamage(EntityUid uid, BarbellPinnedComponent component, ref BeforeStaminaDamageEvent args)
    {
        if (args.Value < 0f)
        {
            args.Value *= 0.02f;
        }
    }

    protected override void OnUnstrapped(Entity<BarbellBenchComponent> bench, ref UnstrappedEvent args)
    {
        base.OnUnstrapped(bench, ref args);

        CancelRep(bench.Owner, bench.Comp);
    }

    private void OnBuckleTerminating(EntityUid uid, BuckleComponent component, EntityTerminatingEvent args)
    {
        foreach (var (bench, performer) in _activeRepPerformers.ToArray())
        {
            if (performer != uid)
                continue;

            if (TryComp<BarbellBenchComponent>(bench, out var benchComp))
                CancelRep(bench, benchComp);
            else
                _activeRepPerformers.Remove(bench);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;

        if (curTime >= _nextSuffocationDamage)
        {
            _nextSuffocationDamage = curTime + TimeSpan.FromSeconds(1f);

            var query = EntityQueryEnumerator<ActiveBarbellPinnedComponent, BarbellPinnedComponent>();
            while (query.MoveNext(out var uid, out _, out var pinned))
            {
                if (TryComp<StaminaComponent>(uid, out var staminaComp))
                {
                    var currentStamina = _stamina.GetStaminaDamage(uid, staminaComp);
                    if (currentStamina < staminaComp.CritThreshold && !staminaComp.Critical)
                    {
                        DeletePinnedBarbell(uid, pinned);

                        RemCompDeferred<BarbellPinnedComponent>(uid);
                        RemCompDeferred<ActiveBarbellPinnedComponent>(uid);
                        _popup.PopupEntity(Loc.GetString("barbell-bench-recovered"), uid, uid, PopupType.Medium);
                        continue;
                    }
                }

                var stunPassed = false;
                if (TryComp<KnockedDownComponent>(uid, out var knockedDown))
                {
                    stunPassed = knockedDown.NextUpdate <= curTime;
                }

                if (stunPassed && pinned.Bench != null && Exists(pinned.Bench))
                {
                    if (TryComp<StrapComponent>(pinned.Bench, out var strap))
                    {
                        _alerts.ShowAlert(uid, strap.BuckledAlertType);
                    }
                }

                var pinDuration = TimeSpan.FromSeconds(pinned.PinDurationSeconds);
                var suffocationActive = (curTime - pinned.PinnedAt) < pinDuration;

                if (pinned.Bench is { } pinnedBench &&
                    Exists(pinnedBench) &&
                    TryComp<BarbellBenchComponent>(pinnedBench, out var bench) &&
                    TryGetBarbell(pinnedBench, bench, out _, out _))
                {
                    if (suffocationActive && TryComp<RespiratorComponent>(uid, out var respirator))
                    {
                        _respirator.UpdateSaturation(uid, -2f, respirator);

                        _damageable.TryChangeDamage(uid, respirator.Damage * SuffocationDamageMultiplier, interruptsDoAfters: false);
                    }

                    if (!stunPassed)
                        _alerts.ClearAlertCategory(uid, SharedBuckleSystem.BuckledAlertCategory);

                    continue;
                }

                RemCompDeferred<BarbellPinnedComponent>(uid);
                RemCompDeferred<ActiveBarbellPinnedComponent>(uid);
                _popup.PopupEntity(Loc.GetString("barbell-bench-recovered"), uid, uid, PopupType.Medium);
            }
        }
    }

}
