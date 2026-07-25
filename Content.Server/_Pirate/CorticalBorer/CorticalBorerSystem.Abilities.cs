// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 Coenx-flex
// SPDX-FileCopyrightText: 2025 Cojoke
// SPDX-FileCopyrightText: 2025 ark1368
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Shared._Pirate.CorticalBorer;
using Content.Shared.Body.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;

namespace Content.Server._Pirate.CorticalBorer;

public sealed partial class CorticalBorerSystem
{
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;

    private void SubscribeAbilities()
    {
        SubscribeLocalEvent<CorticalBorerComponent, CorticalInfestEvent>(OnInfest);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalInfestDoAfterEvent>(OnInfestDoAfter);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalEjectEvent>(OnEjectHost);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalTakeControlEvent>(OnTakeControl);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalChemMenuActionEvent>(OnChemicalMenu);
        SubscribeLocalEvent<CorticalBorerComponent, CorticalCheckBloodEvent>(OnCheckBlood);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, CorticalEndControlEvent>(OnEndControl);
        SubscribeLocalEvent<CorticalBorerInfestedComponent, CorticalLayEggEvent>(OnLayEgg);
    }

    private void OnChemicalMenu(Entity<CorticalBorerComponent> ent, ref CorticalChemMenuActionEvent args)
    {
        if (!TryComp<UserInterfaceComponent>(ent, out var userInterface))
            return;

        if (ent.Comp.Host is null)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-no-host"), ent, ent, PopupType.Medium);
            return;
        }

        Ui.TryToggleUi((ent, userInterface), CorticalBorerDispenserUiKey.Key, ent);
    }

    private void OnInfest(Entity<CorticalBorerComponent> ent, ref CorticalInfestEvent args)
    {
        var (uid, comp) = ent;
        var target = args.Target;
        var targetIdentity = Identity.Entity(target, EntityManager);

        if (comp.Host is not null)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-has-host"), uid, uid, PopupType.Medium);
            return;
        }

        if (HasComp<CorticalBorerInfestedComponent>(target))
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-host-already-infested", ("target", targetIdentity)),
                uid,
                uid,
                PopupType.Medium);
            return;
        }

        if (HasComp<CorticalBorerComponent>(target) || !HasComp<BloodstreamComponent>(target))
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-invalid-host", ("target", targetIdentity)),
                uid,
                uid,
                PopupType.Medium);
            return;
        }

        if (!CanUseAbility(ent, target))
            return;

        var infestAttempt = new InfestHostAttempt();
        RaiseLocalEvent(target, infestAttempt);

        if (infestAttempt.Cancelled)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-face-covered", ("target", targetIdentity)),
                uid,
                uid,
                PopupType.Medium);
            return;
        }

        Popup.PopupEntity(Loc.GetString("cortical-borer-start-infest", ("target", targetIdentity)),
            uid,
            uid,
            PopupType.Medium);

        var doAfter = new DoAfterArgs(EntityManager,
            uid,
            TimeSpan.FromSeconds(3),
            new CorticalInfestDoAfterEvent(),
            uid,
            target)
        {
            DistanceThreshold = 1.5f,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
            Hidden = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnInfestDoAfter(Entity<CorticalBorerComponent> ent, ref CorticalInfestDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        if (HasComp<CorticalBorerInfestedComponent>(target) || HasComp<CorticalBorerComponent>(target))
            return;

        _flammable.Extinguish(ent);
        InfestTarget(ent, target);
        SyncHostVision(ent);
        args.Handled = true;
    }

    private void OnEjectHost(Entity<CorticalBorerComponent> ent, ref CorticalEjectEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Host is not { } host)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-no-host"), ent, ent, PopupType.Medium);
            return;
        }

        if (!CanUseAbility(ent, host))
            return;

        TryEjectBorer(ent);
        args.Handled = true;
    }

    private void OnCheckBlood(Entity<CorticalBorerComponent> ent, ref CorticalCheckBloodEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Host is null)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-no-host"), ent, ent, PopupType.Medium);
            return;
        }

        TryToggleCheckBlood(ent);
        args.Handled = true;
    }

    private void OnTakeControl(Entity<CorticalBorerComponent> ent, ref CorticalTakeControlEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Host is not { } host)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-no-host"), ent, ent, PopupType.Medium);
            return;
        }

        if (TryComp<MobStateComponent>(host, out var mobState) && mobState.CurrentState == MobState.Dead)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-dead-host"), ent, ent, PopupType.Medium);
            return;
        }

        if (!TryComp<CorticalBorerInfestedComponent>(host, out var infested) || !CanUseAbility(ent, host))
            return;

        if (ent.Comp.ControlingHost)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-already-control"), ent, ent, PopupType.Medium);
            return;
        }

        TakeControlHost(ent, infested);
        args.Handled = true;
    }

    private void OnEndControl(Entity<CorticalBorerInfestedComponent> host, ref CorticalEndControlEvent args)
    {
        if (args.Handled)
            return;

        EndControl(host.Comp.Borer);
        args.Handled = true;
    }

    private void OnLayEgg(Entity<CorticalBorerInfestedComponent> host, ref CorticalLayEggEvent args)
    {
        if (args.Handled)
            return;

        var borer = host.Comp.Borer;
        if (!borer.Comp.CanReproduce || borer.Comp.HasLaidEgg)
            return;

        if (borer.Comp.EggCost > borer.Comp.ChemicalPoints)
        {
            Popup.PopupEntity(Loc.GetString("cortical-borer-not-enough-chem"), host, host, PopupType.Medium);
            return;
        }

        _vomit.Vomit(host, -20, -20);
        LayEgg(borer);
        borer.Comp.HasLaidEgg = true;
        UpdateChems(borer, -borer.Comp.EggCost);

        if (host.Comp.LayEggAction is { } layEggAction)
        {
            Actions.RemoveAction(host.Owner, layEggAction);
            host.Comp.RemoveAbilities.Remove(layEggAction);
            host.Comp.LayEggAction = null;
        }

        args.Handled = true;
    }
}
