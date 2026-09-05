// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects;

/// <summary>
/// A reusable group of entity effects.
/// </summary>
[Prototype]
public sealed partial class EntityEffectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public EntityEffect[] Effects = default!;

    [DataField]
    public EntityCondition[]? Conditions;

    [DataField]
    public LocId? GuidebookText;
}

public sealed partial class SharedEntityEffectsSystem
{
    [Dependency] private readonly IPrototypeManager _entityEffectPrototypes = default!;

    /// <summary>
    /// Applies a reusable effect prototype if its shared conditions pass.
    /// </summary>
    public bool TryApplyEffect(
        EntityUid target,
        ProtoId<EntityEffectPrototype> id,
        float scale = 1f,
        EntityUid? user = null)
    {
        var prototype = _entityEffectPrototypes.Index(id);
        if (!_condition.TryConditions(target, prototype.Conditions))
            return false;

        ApplyEffects(target, prototype.Effects, scale, user);
        return true;
    }
}
