using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Traits.Assorted;

[RegisterComponent]
public sealed partial class KnowledgeableComponent : Component
{
    public const string ComponentName = "Knowledgeable";

    [DataField]
    public int BonusPoints = 10;

    [DataField]
    public HashSet<ProtoId<TraitPrototype>> RequiredTraits = new();

    [DataField]
    public Dictionary<EntProtoId, int> MasteryGrants = new();

    public static int GetBonusPoints(
        IPrototypeManager prototypes,
        IReadOnlySet<ProtoId<TraitPrototype>> traits)
        => Find(prototypes, traits)?.BonusPoints ?? 0;

    public static IReadOnlyDictionary<EntProtoId, int>? GetMasteryGrants(
        IPrototypeManager prototypes,
        IReadOnlySet<ProtoId<TraitPrototype>> traits)
        => Find(prototypes, traits)?.MasteryGrants;

    private static KnowledgeableComponent? Find(
        IPrototypeManager prototypes,
        IReadOnlySet<ProtoId<TraitPrototype>> traits)
    {
        foreach (var traitId in traits)
        {
            if (!prototypes.TryIndex(traitId, out TraitPrototype? trait) ||
                !trait.Components.TryGetValue(ComponentName, out var entry) ||
                entry.Component is not KnowledgeableComponent component ||
                !component.RequiredTraits.IsSubsetOf(traits))
                continue;

            return component;
        }

        return null;
    }
}
