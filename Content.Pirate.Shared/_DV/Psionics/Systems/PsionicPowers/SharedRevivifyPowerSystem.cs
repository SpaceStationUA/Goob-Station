using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;

namespace Content.Shared._DV.Psionics.Systems.PsionicPowers;

/// <summary>
/// This is solely for prediction.
/// </summary>
public abstract class SharedRevivifyPowerSystem : BasePsionicPowerSystem<RevivifyPowerComponent, RevivifyPowerActionEvent>;
