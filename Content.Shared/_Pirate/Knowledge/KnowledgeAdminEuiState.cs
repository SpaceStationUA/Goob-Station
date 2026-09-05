// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Knowledge;

[Serializable, NetSerializable]
public sealed class KnowledgeAdminEuiState(
    NetEntity? target,
    string targetName,
    List<KnowledgeAdminEntry> skills) : EuiStateBase
{
    public readonly NetEntity? Target = target;
    public readonly string TargetName = targetName;
    public readonly List<KnowledgeAdminEntry> Skills = skills;
}

[Serializable, NetSerializable]
public readonly record struct KnowledgeAdminEntry(
    string Prototype,
    string Name,
    string Description,
    string Category,
    bool Hidden,
    bool Exists,
    int LearnedLevel,
    int TemporaryLevel,
    int Experience,
    int ExperienceCost);

[Serializable, NetSerializable]
public readonly record struct KnowledgeAdminEdit(int LearnedLevel, int Experience);

public static class KnowledgeAdminEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Apply(Dictionary<string, KnowledgeAdminEdit> changes) : EuiMessageBase
    {
        public readonly Dictionary<string, KnowledgeAdminEdit> Changes = changes;
    }

    [Serializable, NetSerializable]
    public sealed class Refresh : EuiMessageBase;
}
