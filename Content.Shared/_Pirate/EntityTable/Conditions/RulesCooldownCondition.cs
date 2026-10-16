// SPDX-License-Identifier: MIT

using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.EntityTable.Conditions;

public sealed partial class RulesCooldownCondition : EntityTableCondition
{
    // GameTicker appends " (Pending)" until a delayed rule starts.
    private const string PendingSuffix = " (Pending)";

    [DataField(required: true)]
    public List<EntProtoId> Rules = new();

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(15);

    protected override bool EvaluateImplementation(EntityTableSelector root,
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        var gameTicker = entMan.System<SharedGameTicker>();
        var now = gameTicker.RoundDuration();

        foreach (var (time, entry) in gameTicker.AllPreviousGameRules)
        {
            if (now - time >= Cooldown)
                continue;

            var id = entry.EndsWith(PendingSuffix, StringComparison.Ordinal)
                ? entry.Substring(0, entry.Length - PendingSuffix.Length)
                : entry;
            foreach (var rule in Rules)
            {
                if (rule.Id == id)
                    return false;
            }
        }

        return true;
    }
}
