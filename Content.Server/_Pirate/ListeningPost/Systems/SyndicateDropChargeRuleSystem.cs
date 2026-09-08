// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Components;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;

namespace Content.Server._Pirate.ListeningPost.Systems;

public sealed class SyndicateDropChargeRuleSystem : GameRuleSystem<SyndicateDropChargeRuleComponent>
{
    [Dependency] private readonly SyndicateDropConsoleSystem _dispatcher = default!;

    protected override void Started(EntityUid uid,
        SyndicateDropChargeRuleComponent comp,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (_dispatcher.AddCharges(comp.Charges) == 0)
            Log.Info($"{ToPrettyString(uid):rule} granted no charges: the dispatcher is missing or already full.");
    }
}
