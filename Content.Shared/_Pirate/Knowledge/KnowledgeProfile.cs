// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Character-specific mastery increases applied on top of a species profile.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct KnowledgeProfile
{
    public Dictionary<EntProtoId, int> Mastery;

    public KnowledgeProfile(Dictionary<EntProtoId, int> mastery)
    {
        Mastery = mastery;
    }

    public KnowledgeProfile() : this(new Dictionary<EntProtoId, int>())
    {
    }

    public KnowledgeProfile(KnowledgeProfile other)
        : this(other.Mastery is null
            ? new Dictionary<EntProtoId, int>()
            : new Dictionary<EntProtoId, int>(other.Mastery))
    {
    }

    public static KnowledgeProfile Verify(Dictionary<string, int>? mastery, IPrototypeManager prototypes)
    {
        var profile = new KnowledgeProfile();
        if (mastery is null)
            return profile;

        foreach (var (id, change) in mastery)
        {
            if (prototypes.HasIndex<EntityPrototype>(id))
                profile.Mastery[id] = change;
        }

        return profile;
    }

    public readonly bool MemberwiseEquals(KnowledgeProfile other)
    {
        if (Mastery is null || other.Mastery is null)
            return Mastery is null && other.Mastery is null;

        if (Mastery.Count != other.Mastery.Count)
            return false;

        foreach (var (id, change) in Mastery)
        {
            if (!other.Mastery.TryGetValue(id, out var otherChange) || otherChange != change)
                return false;
        }

        return true;
    }
}
