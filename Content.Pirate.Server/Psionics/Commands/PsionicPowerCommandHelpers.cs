using System.Linq;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Psionics.Commands;

/// <summary>
/// Shared helpers for the psionic power admin commands.
/// </summary>
public static class PsionicPowerCommandHelpers
{
    /// <summary>
    /// Returns all non-abstract entity prototypes that grant a psionic power component.
    /// </summary>
    public static string[] GetPsionicPowerPrototypeIds(IPrototypeManager prototype)
    {
        return prototype.EnumeratePrototypes<EntityPrototype>()
            .Where(IsPsionicPowerPrototype)
            .Select(prototype => prototype.ID)
            .OrderBy(id => id)
            .ToArray();
    }

    /// <summary>
    /// Whether the prototype is a pure psionic power entity.
    /// Only prototypes consisting entirely of psionic power components qualify,
    /// so clothing, mobs and other entities that merely grant a power are excluded.
    /// </summary>
    public static bool IsPsionicPowerPrototype(EntityPrototype prototype)
    {
        if (prototype.Abstract)
            return false;

        var components = prototype.Components.Values;
        return components.Count > 0
            && components.All(component => component.Component is BasePsionicPowerComponent);
    }
}
