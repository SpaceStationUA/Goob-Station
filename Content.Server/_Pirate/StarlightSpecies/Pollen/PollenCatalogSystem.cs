// Pirate: resolve Starlight pollen through native seeds and produce instead of PlantData entities.
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.StarlightSpecies.Pollen;

public sealed class PollenCatalogSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    // Source produce with EmitPollen; unavailable Starlight-only plants are excluded at lookup.
    private static readonly HashSet<string> Produce =
    [
        "FoodLemoon", "FoodLime", "FoodOrange", "WheatBushel", "OatBushel", "Sugarcane",
        "Nettle", "DeathNettle", "FoodBanana", "FoodCarrot", "FoodCabbage", "FoodGarlic",
        "FoodPineapple", "FoodPotato", "FoodTomato", "FoodApple", "FoodGoldenApple", "FoodCorn",
        "FoodOnion", "FoodOnionRed", "FoodMushroom", "FoodChillyPepper", "FoodAloe", "FoodPoppy",
        "FoodAmbrosiaVulgaris", "FoodAmbrosiaDeus", "FoodGalaxythistle", "RiceBushel", "FoodSoybeans",
        "FoodSpacemansTrumpet", "FoodWatermelon", "FoodHolymelon", "FoodBerries", "FoodBungo",
        "FoodPeaPod", "FoodWorldPeas", "FoodPumpkin", "FoodBluePumpkin", "FoodCherry",
    ];

    public IEnumerable<string> PollenIds
    {
        get
        {
            foreach (var id in Produce)
                if (_prototypes.HasIndex<EntityPrototype>(id))
                    yield return id;
        }
    }

    public bool TryGetProduce(string pollenId, out EntProtoId produce)
    {
        produce = pollenId;
        return Produce.Contains(pollenId) && _prototypes.HasIndex(produce);
    }

    public string GetName(string pollenId) => pollenId == "advancedpollen"
        ? Loc.GetString("pollen-name-advancedpollen")
        : _prototypes.Index<EntityPrototype>(pollenId).Name;
}
