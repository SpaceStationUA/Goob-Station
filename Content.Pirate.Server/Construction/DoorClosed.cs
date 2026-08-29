using Content.Shared.Construction;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using JetBrains.Annotations;

namespace Content.Pirate.Server.Construction;

/// <summary>
/// Requires a door to be shut before the edge can be taken. Secret doors use this so they can only
/// be bolted into a solid wall while closed, mirroring the /tg/station falsewall screwdriver check.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class DoorClosed : IGraphCondition
{
    [DataField]
    public bool Closed { get; private set; } = true;

    private static bool IsClosed(EntityUid uid, IEntityManager entityManager)
    {
        return entityManager.TryGetComponent<DoorComponent>(uid, out var door)
               && door.State == DoorState.Closed;
    }

    public bool Condition(EntityUid uid, IEntityManager entityManager)
    {
        return IsClosed(uid, entityManager) == Closed;
    }

    public bool DoExamine(ExaminedEvent args)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();

        if (IsClosed(args.Examined, entityManager) == Closed)
            return false;

        args.PushMarkup(Loc.GetString(Closed
            ? "construction-examine-condition-door-closed"
            : "construction-examine-condition-door-open"));
        return true;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry
        {
            Localization = Closed
                ? "construction-step-condition-door-closed"
                : "construction-step-condition-door-open",
        };
    }
}
