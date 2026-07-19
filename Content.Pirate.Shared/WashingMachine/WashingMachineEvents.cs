namespace Content.Pirate.Shared.WashingMachine;

public sealed class WashingMachineIsBeingWashed(EntityUid washingMachine, IReadOnlySet<EntityUid> items) : EntityEventArgs
{
    public readonly EntityUid WashingMachine = washingMachine;
    public readonly IReadOnlySet<EntityUid> Items = items;
}

public sealed class WashingMachineStartedWashingEvent(IReadOnlySet<EntityUid> items) : EntityEventArgs
{
    public readonly IReadOnlySet<EntityUid> Items = items;
}

public sealed class WashingMachineWashedEvent(EntityUid washingMachine, IReadOnlySet<EntityUid> items) : EntityEventArgs
{
    public readonly EntityUid WashingMachine = washingMachine;
    public readonly IReadOnlySet<EntityUid> Items = items;
}

public sealed class WashingMachineFinishedWashingEvent(IReadOnlySet<EntityUid> items) : EntityEventArgs
{
    public readonly IReadOnlySet<EntityUid> Items = items;
}
