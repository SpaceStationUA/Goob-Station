// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Pirate.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Newbies;

/// <summary>Prevents inexperienced players from mixing water with sodium or potassium.</summary>
public sealed class NoviceChemicalProtectionSystem : EntitySystem
{
    private static readonly TimeSpan RequiredPlaytime = TimeSpan.FromHours(24);

    [Dependency] private readonly NovicePlaytimeSystem _playtime = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, ChemicalMixAttemptEvent>(OnMixAttempt);
    }

    private void OnMixAttempt(Entity<ActorComponent> ent, ref ChemicalMixAttemptEvent args)
    {
        if (args.Cancelled ||
            (!HasWater(args.Source) && !HasWater(args.Target)) ||
            (!HasReactiveMetal(args.Source) && !HasReactiveMetal(args.Target)) ||
            !_playtime.IsRestricted(ent.Comp.PlayerSession, RequiredPlaytime))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("novice-protection-reactive-chemical-mixing"), ent, ent);
    }

    private static bool HasWater(Solution solution) =>
        solution.GetTotalPrototypeQuantity("Water") > FixedPoint2.Zero;

    private static bool HasReactiveMetal(Solution solution) =>
        solution.GetTotalPrototypeQuantity("Sodium", "Potassium") > FixedPoint2.Zero;
}
