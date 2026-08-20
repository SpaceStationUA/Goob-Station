using Content.Shared.Examine;
using Content.Shared._DV.Psionics.Components;
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
        SubscribeLocalEvent<ShadowkinComponent, MindBrokenAddedEvent>(OnMindBrokenAdded);
        SubscribeLocalEvent<ShadowkinComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<ShadowkinComponent, EyeColorInitEvent>(OnEyeColorChange);
    }

    /*private void OnInit(EntityUid uid, ShadowkinComponent component, ComponentStartup args) PIRATE REMOVE
    {
        _actionsSystem.AddAction(uid, ref component.ShadowkinSleepAction, ShadowkinSleepActionId, uid);
    } */

    private void OnEyeColorChange(Entity<ShadowkinComponent> ent, ref EyeColorInitEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        // Remember the real eye color, but never capture the mindbroken black as the original.
        if (humanoid.EyeColor != ent.Comp.OldEyeColor && humanoid.EyeColor != ent.Comp.BlackEyeColor)
        {
            ent.Comp.OldEyeColor = humanoid.EyeColor;
            Dirty(ent, humanoid);
        }
    }

    private void OnMindbreak(Entity<ShadowkinComponent> ent, ref PsionicMindBrokenEvent args)
    {
        SetBlackEyes(ent, ent.Comp);

        //if (TryComp<StaminaComponent>(uid, out var stamina)) PIRATE
        //    _stamina.TakeStaminaDamage(uid, stamina.CritThreshold, stamina, uid);
    }

    private void OnMindBrokenAdded(Entity<ShadowkinComponent> ent, ref MindBrokenAddedEvent args)
    {
        SetBlackEyes(ent, ent.Comp);
    }

    private void OnRejuvenate(Entity<ShadowkinComponent> ent, ref RejuvenateEvent args)
    {
        // Permanently mindbroken shadowkin keep their black eyes forever - an admin heal
        // must not undo the mindbroken look. Everything else (desc, components) stays.
        if (HasComp<MindBrokenComponent>(ent))
        {
            SetBlackEyes(ent, ent.Comp);
            return;
        }

        if (TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
        {
            humanoid.EyeColor = ent.Comp.OldEyeColor;
            Dirty(ent, humanoid);
        }
    }

    /// <summary>
    /// Turns the shadowkin's eyes permanently black, remembering their original color.
    /// </summary>
    private void SetBlackEyes(Entity<ShadowkinComponent> ent, ShadowkinComponent component, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(ent, ref humanoid, false))
            return;

        if (humanoid.EyeColor == component.BlackEyeColor)
            return;

        component.OldEyeColor = humanoid.EyeColor;
        humanoid.EyeColor = component.BlackEyeColor;
        Dirty(ent, humanoid);
    }
}
