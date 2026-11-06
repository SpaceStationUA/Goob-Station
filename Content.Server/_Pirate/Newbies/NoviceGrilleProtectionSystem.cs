// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Newbies;

/// <summary>Prevents players with less than 24 hours from building or repairing electrifiable grilles.</summary>
public sealed class NoviceGrilleProtectionSystem : EntitySystem
{
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(24);

    [Dependency] private readonly NovicePlaytimeSystem _playtime = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, ConstructAttemptEvent>(OnConstructAttempt);
        SubscribeLocalEvent<ActorComponent, ConstructEntityAttemptEvent>(OnConstructEntityAttempt);
        SubscribeLocalEvent<ActorComponent, ConstructMaterialAttemptEvent>(OnRepairAttempt);
    }

    private void OnConstructAttempt(Entity<ActorComponent> ent, ref ConstructAttemptEvent args)
    {
        if (args.Cancelled ||
            !_prototypes.TryIndex<ConstructionPrototype>(args.Prototype, out var recipe) ||
            recipe.Graph.Id is not ("Grille" or "GrilleDiagonal" or "ClockGrille") ||
            !_playtime.IsRestricted(ent.Comp.PlayerSession, RequiredPlaytime))
            return;

        args.Cancelled = true;
        if (args.LogError)
            ShowRestriction(ent);
    }

    private void OnConstructEntityAttempt(Entity<ActorComponent> ent, ref ConstructEntityAttemptEvent args)
    {
        if (args.Cancelled || !IsGrille(args.Prototype) ||
            !_playtime.IsRestricted(ent.Comp.PlayerSession, RequiredPlaytime))
            return;

        args.Cancelled = true;
        if (args.ShowPopup)
            ShowRestriction(ent);
    }

    private bool IsGrille(string prototype)
    {
        foreach (var parent in _prototypes.EnumerateParents<EntityPrototype>(prototype, includeSelf: true))
        {
            if (parent.ID == "Grille")
                return true;
        }

        return false;
    }

    private void OnRepairAttempt(Entity<ActorComponent> ent, ref ConstructMaterialAttemptEvent args)
    {
        if (args.Cancelled ||
            args.Graph is not ("Grille" or "ClockGrille") ||
            args.Node is not ("grilleBroken" or "clockGrilleBroken") ||
            !TryComp<StackComponent>(args.Material, out var stack) || stack.StackTypeId.Id != "MetalRod" ||
            !_playtime.IsRestricted(ent.Comp.PlayerSession, RequiredPlaytime))
            return;

        args.Cancelled = true;
        ShowRestriction(ent);
    }

    private void ShowRestriction(EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("novice-protection-grille-construction"), user, user);
    }
}
