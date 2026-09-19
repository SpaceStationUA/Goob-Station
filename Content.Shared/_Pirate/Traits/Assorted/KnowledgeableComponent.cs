using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Assorted;

[RegisterComponent]
public sealed partial class KnowledgeableComponent : Component
{
    public const string ComponentName = "Knowledgeable";

    [DataField]
    public int BonusPoints = 10;

    public static int GetBonusPoints(
        IPrototypeManager prototypes,
        IReadOnlySet<ProtoId<TraitPrototype>> traits)
    {
        foreach (var traitId in traits)
        {
            if (!prototypes.TryIndex(traitId, out TraitPrototype? trait) ||
                !trait.Components.TryGetValue(ComponentName, out var entry) ||
                entry.Component is not KnowledgeableComponent component)
                continue;

            return component.BonusPoints;
        }

        return 0;
    }
}
