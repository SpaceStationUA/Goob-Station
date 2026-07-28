using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.PAI;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.PAI;

public sealed class PAIMedInjectorSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly Dictionary<string, MedDef> RegularMeds = new()
    {
        { "bicaridine", new("pai-med-bicaridine", "pai-med-bicaridine-desc", "Bicaridine", 3f) },
        { "kelotane", new("pai-med-kelotane", "pai-med-kelotane-desc", "Kelotane", 3f) },
        { "dylovene", new("pai-med-dylovene", "pai-med-dylovene-desc", "Dylovene", 3f) },
        // { "dexalin", new("pai-med-dexalin", "pai-med-dexalin-desc", "Dexalin", 3f) },
        // { "arithrazine", new("pai-med-arithrazine", "pai-med-arithrazine-desc", "Arithrazine", 3f) },
    };

    private static readonly Dictionary<string, MedDef> SyndicateMeds = new()
    {
        { "tricordrazine", new("pai-med-tricordrazine", "pai-med-tricordrazine-desc", "Tricordrazine", 3f) },
        { "ephedrine", new("pai-med-ephedrine", "pai-med-ephedrine-desc", "Ephedrine", 3f) },
        { "bicaridine", new("pai-med-bicaridine", "pai-med-bicaridine-desc", "Bicaridine", 3f) },
        { "kelotane", new("pai-med-kelotane", "pai-med-kelotane-desc", "Kelotane", 3f) },
        { "dylovene", new("pai-med-dylovene", "pai-med-dylovene-desc", "Dylovene", 3f) },
        // { "dexalin", new("pai-med-dexalin", "pai-med-dexalin-desc", "Dexalin", 3f) },
        // { "arithrazine", new("pai-med-arithrazine", "pai-med-arithrazine-desc", "Arithrazine", 3f) },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PAIMedInjectorComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<PAIMedInjectorComponent, PAIMedInjectorInjectMessage>(OnInject);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PAIMedInjectorComponent>();
        while (query.MoveNext(out var uid, out var injector))
        {
            // Recharge capacity
            if (injector.CurrentCapacity < injector.MaxCapacity && now >= injector.NextRecharge)
            {
                injector.CurrentCapacity = Math.Min(injector.MaxCapacity, injector.CurrentCapacity + injector.RechargeAmount);
                injector.NextRecharge = now + TimeSpan.FromSeconds(injector.RechargeTime);
                Dirty(uid, injector);
            }

            // Push live UI updates while the window is open
            if (_ui.IsUiOpen(uid, PAIMedInjectorUiKey.Key))
                UpdateUi(uid, injector);
        }
    }

    private Dictionary<string, MedDef> GetMeds(EntityUid uid)
    {
        return _tag.HasTag(uid, "Syndicate") ? SyndicateMeds : RegularMeds;
    }

    private EntityUid? FindCarrier(EntityUid uid)
    {
        EntityUid? current = uid;
        while (_container.TryGetContainingContainer((current.Value, null, null), out var parentContainer))
        {
            current = parentContainer.Owner;
            if (HasComp<MobStateComponent>(current.Value))
                return current;
        }
        return null;
    }

    private void OnUIOpened(Entity<PAIMedInjectorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnInject(Entity<PAIMedInjectorComponent> ent, ref PAIMedInjectorInjectMessage args)
    {
        var uid = ent.Owner;
        var injector = ent.Comp;
        var now = _timing.CurTime;
        var meds = GetMeds(uid);

        if (!meds.TryGetValue(args.MedId, out var med))
            return;

        var carrier = FindCarrier(uid);
        if (carrier == null)
        {
            _popup.PopupClient(Loc.GetString("pai-med-injector-no-carrier"), uid, uid);
            return;
        }

        if (injector.CurrentCapacity < med.Units)
        {
            _popup.PopupClient(Loc.GetString("pai-med-injector-no-capacity"), uid, uid);
            return;
        }

        if (injector.LastUsed.TryGetValue(args.MedId, out var lastUsed)
            && (now - lastUsed).TotalSeconds < injector.MedCooldown)
        {
            var remaining = (int)(injector.MedCooldown - (now - lastUsed).TotalSeconds);
            _popup.PopupClient(Loc.GetString("pai-med-injector-cooldown", ("time", remaining)), uid, uid);
            return;
        }

        if (!HasComp<BloodstreamComponent>(carrier.Value))
        {
            _popup.PopupClient(Loc.GetString("pai-med-injector-no-bloodstream"), uid, uid);
            return;
        }

        if (!_solution.TryGetInjectableSolution(carrier.Value, out var injectable, out _))
        {
            _popup.PopupClient(Loc.GetString("pai-med-injector-no-bloodstream"), uid, uid);
            return;
        }

        _solution.TryAddReagent(injectable.Value, med.Reagent, FixedPoint2.New(med.Units), out _);

        injector.CurrentCapacity -= med.Units;
        injector.LastUsed[args.MedId] = now;
        Dirty(uid, injector);

        _audio.PlayPredicted(new SoundPathSpecifier("/Audio/Items/hypospray.ogg"), uid, uid);
        _popup.PopupClient(Loc.GetString("pai-med-injector-success", ("med", Loc.GetString(med.LocName))), uid, uid);

        UpdateUi(uid, injector);
    }

    private void UpdateUi(EntityUid uid, PAIMedInjectorComponent? injector = null)
    {
        if (!Resolve(uid, ref injector))
            return;
        if (!_ui.HasUi(uid, PAIMedInjectorUiKey.Key))
            return;

        var now = _timing.CurTime;
        var carrier = FindCarrier(uid);
        var meds = GetMeds(uid);
        var states = new List<MedButtonState>();

        var hasBloodstream = carrier != null && HasComp<BloodstreamComponent>(carrier.Value);

        foreach (var (id, med) in meds)
        {
            var state = new MedButtonState(id, Loc.GetString(med.LocName), Loc.GetString(med.LocDesc), med.Units);
            state.Available = true;

            if (carrier == null)
                state.Available = false;
            else if (!hasBloodstream)
                state.Available = false;
            else if (injector.CurrentCapacity < med.Units)
                state.Available = false;
            else if (injector.LastUsed.TryGetValue(id, out var lastUsed))
            {
                var elapsed = (now - lastUsed).TotalSeconds;
                if (elapsed < injector.MedCooldown)
                {
                    state.Available = false;
                    state.CooldownRemaining = (float)(injector.MedCooldown - elapsed);
                }
            }

            states.Add(state);
        }

        _ui.SetUiState(uid, PAIMedInjectorUiKey.Key,
            new PAIMedInjectorBoundUserInterfaceState(
                injector.CurrentCapacity,
                injector.MaxCapacity,
                carrier != null,
                states));
    }

    private record struct MedDef(string LocName, string LocDesc, string Reagent, float Units);
}
