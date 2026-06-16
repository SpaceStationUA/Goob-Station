using Content.Shared.Examine;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.Humanoid;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Shadowkin;
using Content.Shared.Rejuvenate;
using Content.Shared.Alert;
using Content.Shared.Rounding;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Server.Shadowkin;

public sealed class ShadowkinSystem : EntitySystem
{
    //[Dependency] private readonly StaminaSystem _stamina = default!; PIRATE
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    //public const string ShadowkinSleepActionId = "ShadowkinActionSleep"; PIRATE REMOVE
    public override void Initialize()
    {
        base.Initialize();
        //SubscribeLocalEvent<ShadowkinComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<ShadowkinComponent, PsionicMindBrokenEvent>(OnMindbreak);
        SubscribeLocalEvent<ShadowkinComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<ShadowkinComponent, EyeColorInitEvent>(OnEyeColorChange);
    }

    /*private void OnInit(EntityUid uid, ShadowkinComponent component, ComponentStartup args) PIRATE REMOVE
    {
        _actionsSystem.AddAction(uid, ref component.ShadowkinSleepAction, ShadowkinSleepActionId, uid);
    } */

    private void OnEyeColorChange(EntityUid uid, ShadowkinComponent component, EyeColorInitEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
            || humanoid.EyeColor == component.OldEyeColor)
            return;

        component.OldEyeColor = humanoid.EyeColor;
        Dirty(uid, humanoid);
    }

    private void OnMindbreak(EntityUid uid, ShadowkinComponent component, ref PsionicMindBrokenEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            component.OldEyeColor = humanoid.EyeColor;
            humanoid.EyeColor = component.BlackEyeColor;
            Dirty(uid, humanoid);
        }

        //if (TryComp<StaminaComponent>(uid, out var stamina)) PIRATE
        //    _stamina.TakeStaminaDamage(uid, stamina.CritThreshold, stamina, uid);
    }

    private void OnRejuvenate(EntityUid uid, ShadowkinComponent component, RejuvenateEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            humanoid.EyeColor = component.OldEyeColor;
            Dirty(uid, humanoid);
        }
    }
}
