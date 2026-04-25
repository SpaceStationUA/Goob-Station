using System;
using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Brewing;

[Serializable, NetSerializable]
public sealed class BrewStationStartMixMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class BrewStationBoundUserInterfaceState : BoundUserInterfaceState
{
    public string Title { get; }
    public string VoiceHint { get; }
    public NetEntity? Container { get; }
    public string? ContainerName { get; }
    public FixedPoint2? Volume { get; }
    public FixedPoint2? MaxVolume { get; }
    public IReadOnlyList<ReagentQuantity>? Reagents { get; }
    public bool Mixing { get; }

    public BrewStationBoundUserInterfaceState(
        string title,
        string voiceHint,
        NetEntity? container,
        string? containerName,
        FixedPoint2? volume,
        FixedPoint2? maxVolume,
        IEnumerable<ReagentQuantity>? reagents,
        bool mixing)
    {
        Title = title;
        VoiceHint = voiceHint;
        Container = container;
        ContainerName = containerName;
        Volume = volume;
        MaxVolume = maxVolume;
        Reagents = reagents?.ToList();
        Mixing = mixing;
    }
}
