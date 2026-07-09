using Content.Shared.Actions;
using Content.Shared.Charges.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Spider;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.TerrorSpider.EntitySystems;

public sealed partial class EggInjectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EggInjectionEvent>(OnEggInjection);
        SubscribeLocalEvent<SpiderComponent, EggInjectionDoAfterEvent>(OnEggInjectionDoAfter);
        SubscribeLocalEvent<TerrorPrincessComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EggsLayingEvent>(OnEggsLaying);

        Subs.BuiEvents<TerrorPrincessComponent>(EggsLayingUiKey.Key, subs =>
            subs.Event<EggsLayingBuiMsg>(OnEggsLaying));
    }

    private void OnMapInit(Entity<TerrorPrincessComponent> ent, ref MapInitEvent args) =>
        ent.Comp.LayEggAction = _actions.AddAction(ent.Owner, ent.Comp.LayEggActionId);

    private void OnEggsLaying(EggsLayingEvent ev)
    {
        if (ev.Handled || !_timing.IsFirstTimePredicted)
            return;

        if (TryComp(ev.Performer, out ActorComponent? actor))
            _uiSystem.OpenUi(ev.Performer, EggsLayingUiKey.Key, actor.PlayerSession);
    }

    private void OnEggsLaying(EntityUid uid, TerrorPrincessComponent component, EggsLayingBuiMsg args)
    {
        if (!component.Eggs.Contains(args.Egg) || !TryComp(uid, out ActorComponent? actor))
            return;

        if (component.LayEggAction == null || !_charges.TryUseCharge(component.LayEggAction.Value))
            return;

        PredictedSpawnAtPosition(args.Egg, Transform(uid).Coordinates);
        _uiSystem.CloseUi(uid, EggsLayingUiKey.Key, actor.PlayerSession);
    }

    private void OnEggInjectionDoAfter(Entity<SpiderComponent> ent, ref EggInjectionDoAfterEvent args)
    {
        if (args.Cancelled
            || args.Handled
            || !_timing.IsFirstTimePredicted
            || args.Target == null
            || !TryComp<WrapEntityHolderComponent>(args.Target.Value, out var wrapEntityHolder))
            return;

        if (wrapEntityHolder.Hold == null)
        {
            _popup.PopupPredicted(Loc.GetString("terror-spider-egg-inject-cocoon-empty"), ent, ent);
            return;
        }

        if (!HasComp<HasEggHolderComponent>(wrapEntityHolder.Hold.Value))
        {
            args.Handled = true;
            EnsureComp<EggHolderComponent>(wrapEntityHolder.Hold.Value);
            EnsureComp<HasEggHolderComponent>(wrapEntityHolder.Hold.Value);
            RaiseLocalEvent(ent.Owner, new EggsInjectedEvent());
        }
        else
        {
            _popup.PopupPredicted(Loc.GetString("terror-spider-egg-inject-already-has-eggs"), ent, ent);
        }
    }

    private void OnEggInjection(EggInjectionEvent ev)
    {
        if (ev.Handled || !TryComp<WrapEntityHolderComponent>(ev.Target, out var wrapEntityHolder))
            return;

        if (wrapEntityHolder.Hold == null)
        {
            _popup.PopupPredicted(Loc.GetString("terror-spider-egg-inject-cocoon-empty"), ev.Performer, ev.Performer);
            return;
        }

        ev.Handled = true;

        if (HasComp<HasEggHolderComponent>(wrapEntityHolder.Hold.Value))
        {
            _popup.PopupPredicted(Loc.GetString("terror-spider-egg-inject-already-has-eggs"), ev.Performer, ev.Performer);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager,
            ev.Performer,
            TimeSpan.FromSeconds(ev.InjectionDelay),
            new EggInjectionDoAfterEvent(),
            ev.Performer,
            ev.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f
        };

        _doAfter.TryStartDoAfter(doAfter);
    }
}
