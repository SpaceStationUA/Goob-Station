// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Ranching;

[Prototype]
public sealed partial class EggRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField(required: true)]
    public EntProtoId Egg;

    [DataField(required: true)]
    public List<EntProtoId> RequiredChicken = new();

    [DataField]
    public int HappinessRequired = 15;

    [DataField]
    public int Weight;

    [DataField]
    public HashSet<ProtoId<TagPrototype>>? FoodTagsRequired;

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float>? ReagentsRequired;

    [DataField]
    public List<EntProtoId>? NoSpecialFoodRequiredChickens;

    [DataField]
    public Dictionary<EntProtoId, int>? ChickensRequireDifferentHappiness;

    [DataField]
    public EntityWhitelist? ComponentsRequired;
}
