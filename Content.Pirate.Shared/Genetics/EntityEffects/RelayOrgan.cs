// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.GameStates;

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Applies an effect to a single organ or bodypart of a given category.
/// The target entity must be the body.
/// </summary>
public sealed partial class RelayOrgan : EntityEffectBase<RelayOrgan>
{
    [DataField(required: true)]
    public string Category = default!;

    [DataField(required: true)]
    public EntityEffect[] Effects = default!;

    /// <summary>
    /// Text to use for the guidebook entry for reagents.
    /// </summary>
    [DataField]
    public LocId? GuidebookText;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => GuidebookText is {} key ? Loc.GetString(key, ("chance", Probability)) : null;
}

public sealed partial class RelayOrganEffectSystem : EntityEffectSystem<BodyComponent, RelayOrgan>
{
    [Dependency] private GeneticsBodySystem _body = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    protected override void Effect(Entity<BodyComponent> ent, ref EntityEffectEvent<RelayOrgan> args)
    {
        var category = args.Effect.Category;
        if (_body.GetOrgan(ent.Owner, category) is not {} organ)
            return;

        _effects.ApplyEffects(organ, args.Effect.Effects, args.Scale, args.User);
    }
}
