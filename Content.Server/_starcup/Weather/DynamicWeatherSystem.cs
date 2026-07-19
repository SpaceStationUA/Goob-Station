using System.Linq;
using Content.Shared.Random.Helpers;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._starcup.Weather;

public sealed partial class DynamicWeatherSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>
    /// The reserved weather state that functions as a stand-in for no active weather event.
    /// </summary>
    public const string WeatherClear = "WeatherClear";

    /// <summary>
    /// How long DynamicWeatherSystem simulates weather transitions to pick a random initial state.
    /// </summary>
    public static readonly TimeSpan MaximumExpectedRoundLength = TimeSpan.FromHours(3);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DynamicWeatherComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DynamicWeatherComponent> ent, ref MapInitEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.Scheduler, out var scheduler))
            return;

        ent.Comp.NextUpdate = _gameTiming.CurTime + ent.Comp.StepFrequency;

        var initialState = scheduler.States.First().Key;
        if (ent.Comp.RandomInitialState)
        {
            for (var i = 0; i < MaximumExpectedRoundLength / ent.Comp.StepFrequency; i++)
            {
                initialState = NextState(ent.Comp, scheduler);
                ent.Comp.CurrentState = initialState;
            }
        }

        if (initialState == WeatherClear)
            initialState = null;

        SetWeather(ent.Owner, ent.Comp, initialState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<DynamicWeatherComponent, MapComponent>();
        while (query.MoveNext(out var entity, out var dynamicWeather, out _))
        {
            if (now < dynamicWeather.NextUpdate)
                continue;

            dynamicWeather.NextUpdate = now + dynamicWeather.StepFrequency;

            if (!_proto.TryIndex(dynamicWeather.Scheduler, out var scheduler))
                continue;

            var next = NextState(dynamicWeather, scheduler);
            SetWeather(entity, dynamicWeather, next);
        }
    }

    private string NextState(DynamicWeatherComponent dynamicWeather, WeatherSchedulerPrototype scheduler)
    {
        var currentState = dynamicWeather.CurrentState ?? WeatherClear;

        if (!scheduler.States.TryGetValue(currentState, out var state))
        {
            Log.Error($"Weather scheduler {scheduler.ID} is missing state {currentState}");
            return WeatherClear;
        }

        return _robustRandom.Pick(state.Transitions);
    }

    private void SetWeather(EntityUid map, DynamicWeatherComponent dynamicWeather, string? weatherProtoId)
    {
        if (weatherProtoId == WeatherClear)
            weatherProtoId = null;

        var previousState = dynamicWeather.CurrentState;
        dynamicWeather.CurrentState = weatherProtoId;

        WeatherPrototype? proto = null;
        if (weatherProtoId != null && !_proto.TryIndex(weatherProtoId, out proto))
        {
            Log.Error($"Dynamic weather could not find prototype {weatherProtoId}");
            weatherProtoId = null;
            dynamicWeather.CurrentState = null;
        }

        var mapId = Transform(map).MapID;
        var endTime = dynamicWeather.NextUpdate + WeatherComponent.ShutdownTime;
        _weather.SetWeather(mapId, proto, endTime);

        if (previousState == weatherProtoId)
            return;

        var ev = new DynamicWeatherUpdateEvent(map, previousState, weatherProtoId);
        RaiseLocalEvent(map, ref ev, true);
    }
}

/// <summary>
/// Raised when a map with dynamic weather switches from one weather state to another.
/// </summary>
[ByRefEvent]
public readonly record struct DynamicWeatherUpdateEvent(EntityUid DynamicWeather, string? PreviousState, string? NextState);
