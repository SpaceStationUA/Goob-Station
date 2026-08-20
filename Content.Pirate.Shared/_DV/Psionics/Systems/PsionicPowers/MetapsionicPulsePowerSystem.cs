using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared.Popups;

namespace Content.Shared._DV.Psionics.Systems.PsionicPowers;

/// <summary>
/// This system allows a psionic user to spot other psionic entities via a pulse.
/// </summary>
public sealed class MetapsionicPulsePowerSystem : BasePsionicPowerSystem<MetapsionicPulsePowerComponent,  MetapsionicPulsePowerActionEvent>
{
    [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetapsionicPulsePowerComponent, PsionicPowerPostInitializedEvent>(OnPostInit);
    }

    protected override void OnPowerInit(Entity<MetapsionicPulsePowerComponent> power, ref MapInitEvent args)
    {
        base.OnPowerInit(power, ref args);
        InitMetapsionicExtras(power);
    }

    private void OnPostInit(Entity<MetapsionicPulsePowerComponent> power, ref PsionicPowerPostInitializedEvent args)
    {
        if (args.PowerType != typeof(MetapsionicPulsePowerComponent))
            return;

        InitMetapsionicExtras(power);
    }

    private void InitMetapsionicExtras(Entity<MetapsionicPulsePowerComponent> power)
    {
        // If the power was stripped (e.g. entity without psionic potential), skip the extras.
        if (!HasComp<MetapsionicPulsePowerComponent>(power))
            return;

        EnsureComp<PsionicPowerDetectorComponent>(power);
    }

    protected override void OnPowerUsed(Entity<MetapsionicPulsePowerComponent> psionic, ref MetapsionicPulsePowerActionEvent args)
    {
        // Scan around the caster, not the cursor point.
        foreach (var target in _lookupSystem.GetEntitiesInRange<PsionicComponent>(Transform(args.Performer).Coordinates, psionic.Comp.Range))
        {
            if (target.Owner == args.Performer
                || Transform(target).ParentUid == args.Performer)
                continue;

            if (!Psionic.CanBeTargeted(target)) // Cannot detect shielded psionics.
                continue;

            Popup.PopupClient(Loc.GetString("psionic-power-metapsionic-success"), args.Performer, args.Performer, PopupType.LargeCaution);
            AfterPowerUsed(psionic, args.Performer);

            args.Handled = true;
            return;
        }
        Popup.PopupClient(Loc.GetString("psionic-power-metapsionic-failure"), args.Performer, args.Performer, PopupType.Large);
        AfterPowerUsed(psionic, args.Performer);

        args.Handled = true;
    }

    protected override void OnMindBroken(Entity<MetapsionicPulsePowerComponent> psionic, ref PsionicMindBrokenEvent args)
    {
        base.OnMindBroken(psionic, ref args);
        // If the mindbreak was successful, remove the detector component too.
        if (!psionic.Comp.Deleted)
            return;

        RemComp<PsionicPowerDetectorComponent>(psionic);
    }
}
