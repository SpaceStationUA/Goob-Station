namespace Content.Shared._DV.Psionics.Events;

/// <summary>
/// Raised on a psionic entity after <c>InitializePowerComponents</c> finishes the
/// base power setup (action button, PsionicComponent registration, pool merges,
/// init feedback). <see cref="PowerType"/> identifies which specific power was
/// just initialized so handlers can filter for only the power they care about.
/// </summary>
/// <param name="powerType">The component type of the power that was just initialized.</param>
public sealed class PsionicPowerPostInitializedEvent(Type powerType)
{
    public Type PowerType { get; } = powerType;
}
