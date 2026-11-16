using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;

namespace Content.Shared._DV.Psionics.Systems.PsionicPowers;

/// <summary>
///     Pirate: This is solely for prediction - the summon itself is server-side only.
/// </summary>
public abstract class SharedSummonFamiliarPowerSystem : BasePsionicPowerSystem<SummonFamiliarPowerComponent, SummonFamiliarPowerActionEvent>;
