// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Knowledge;

[Serializable, NetSerializable]
public readonly record struct KnowledgeInfo(
    string Name,
    string Description,
    string Level,
    Color Color,
    SpriteSpecifier? Sprite,
    int LearnedLevel,
    int NetLevel,
    int Experience,
    int ExperienceCost);

[ByRefEvent]
public record struct KnowledgeAddedEvent(Entity<KnowledgeContainerComponent> Container, EntityUid Holder);

[ByRefEvent]
public record struct KnowledgeRemovedEvent(Entity<KnowledgeContainerComponent> Container, EntityUid Holder);

/// <summary>
/// Relayed when XP changes so open character windows can refresh immediately.
/// </summary>
[ByRefEvent]
public record struct KnowledgeExperienceChangedEvent;

[Serializable, NetSerializable]
public sealed class SkillPopupEvent(string popup) : EntityEventArgs
{
    public readonly string Popup = popup;
}
