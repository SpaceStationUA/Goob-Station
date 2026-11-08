// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.GameTicking.Rules.Components;
using Content.Server._Pirate.Banking;
using Content.Server.Antag;

namespace Content.Pirate.Server.GameTicking.Rules;

public sealed class AntagStartingBalanceRuleSystem : EntitySystem
{
    [Dependency] private readonly BankCardSystem _bankCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagStartingBalanceRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnAntagSelected(Entity<AntagStartingBalanceRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_bankCard.TryGiveFlatBalance(args.EntityUid, ent.Comp.Balance))
            Log.Warning($"Could not open a bank account for antag {ToPrettyString(args.EntityUid)} of {ToPrettyString(ent)}.");
    }
}
