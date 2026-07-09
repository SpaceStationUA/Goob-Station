using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server._starcup.Weather;

/// <summary>
/// Defines a preset for Markov chain-based weather scheduling. Describes how an environment tends towards weather
/// conditions.
/// </summary>
[Prototype]
public sealed partial class WeatherSchedulerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Maps weather prototype IDs to their possible next states. The reserved key "WeatherClear" means no weather.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, WeatherState> States = default!;
}

[Serializable, DataDefinition]
public partial struct WeatherState
{
    /// <summary>
    /// Describes possible weather states that may follow this one, and how likely they are to occur compared to others.
    /// Keys are weather prototype IDs, or "WeatherClear" for no weather.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, float> Transitions = default!;
}
