// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public KnowledgeProfile Knowledge = new();

    public HumanoidCharacterProfile WithKnowledge(KnowledgeProfile knowledge)
        => new(this) { Knowledge = new KnowledgeProfile(knowledge) };

    private void CopyPirateKnowledge(HumanoidCharacterProfile other)
    {
        Knowledge = new KnowledgeProfile(other.Knowledge);
    }

    private bool PirateKnowledgeEquals(HumanoidCharacterProfile other)
        => Knowledge.MemberwiseEquals(other.Knowledge);

    private void EnsurePirateKnowledgeValid(IDependencyCollection collection, IPrototypeManager prototypes)
    {
        var systems = collection.Resolve<IEntitySystemManager>();
        var knowledge = systems.GetEntitySystem<SharedKnowledgeSystem>();
        var parent = prototypes.Index<SpeciesPrototype>(Species).Knowledge;
        knowledge.EnsureProfileValid(parent, ref Knowledge);
    }

    private void AddPirateKnowledgeHash(ref HashCode hash)
    {
        if (Knowledge.Mastery is null)
            return;

        foreach (var (id, mastery) in Knowledge.Mastery.OrderBy(pair => pair.Key.Id))
        {
            hash.Add(id);
            hash.Add(mastery);
        }
    }
}
