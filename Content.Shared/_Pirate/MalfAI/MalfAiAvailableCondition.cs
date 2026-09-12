// SPDX-License-Identifier: MIT

using Content.Shared.EntityTable;
using Content.Shared.EntityTable.Conditions;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.MalfAI;

public sealed partial class MalfAiAvailableCondition : EntityTableCondition
{
    protected override bool EvaluateImplementation(EntityTableSelector root, IEntityManager entMan,
        IPrototypeManager proto, EntityTableContext ctx)
    {
        var ev = new MalfAiCandidateCheckEvent();
        entMan.EventBus.RaiseEvent(EventSource.Local, ev);
        return ev.Available;
    }
}

public sealed class MalfAiCandidateCheckEvent : EntityEventArgs
{
    public bool Available;
}
