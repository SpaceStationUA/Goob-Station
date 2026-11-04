// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.GameStates;

using Content.Shared.Body.Part;
using Content.Pirate.Shared.Genetics;
using Content.Shared.Body.Components;
using Content.Shared.EntityEffects;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Relays entity effects to all body parts of a given type, or all parts.
/// Target must be a body.
/// </summary>
public sealed partial class RelayBodyParts : EntityEffectBase<RelayBodyParts>
{
    /// <summary>
    /// The body part type to run effects on.
    /// It will run on all of them if there are multiple.
    /// If this is null it will run on all body parts.
    /// </summary>
    [DataField]
    public BodyPartType? PartType;

    /// <summary>
    /// Optional part symmetry to require.
    /// </summary>
    [DataField]
    public BodyPartSymmetry? Symmetry;

    /// <summary>
    /// Text to use for the guidebook entry for reagents.
    /// </summary>
    [DataField]
    public LocId? GuidebookText;

    [DataField(required: true)]
    public EntityEffect[] Effects = default!;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => GuidebookText is {} key ? Loc.GetString(key, ("chance", Probability)) : null;
}

public sealed partial class RelayBodyPartsEffectSystem : EntityEffectSystem<BodyComponent, RelayBodyParts>
{
    [Dependency] private GeneticsBodySystem _part = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    protected override void Effect(Entity<BodyComponent> ent, ref EntityEffectEvent<RelayBodyParts> args)
    {
        var effect = args.Effect;
        var effects = effect.Effects;
        var partType = effect.PartType;
        var symmetry = effect.Symmetry;
        foreach (var part in _part.GetBodyParts(ent.Owner, partType, symmetry))
        {
            _effects.ApplyEffects(part, effects, args.Scale, args.User);
        }
    }
}
