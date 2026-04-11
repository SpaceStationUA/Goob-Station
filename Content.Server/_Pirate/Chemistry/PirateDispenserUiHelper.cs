using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._Pirate.Chemistry;

public static class PirateDispenserUiHelper
{
    public static ContainerInfo? BuildOutputContainerInfo(
        EntityUid? container,
        SharedSolutionContainerSystem solutionContainerSystem,
        Func<EntityUid, string> nameResolver)
    {
        if (container is not { Valid: true })
            return null;

        if (!solutionContainerSystem.TryGetFitsInDispenser(container.Value, out _, out var solution))
            return null;

        return new ContainerInfo(nameResolver(container.Value), solution.Volume, solution.MaxVolume)
        {
            Reagents = solution.Contents
        };
    }
}
