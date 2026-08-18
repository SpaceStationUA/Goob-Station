using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid.Markings;

public static class MarkingColoration
{
    public static void Clamp(Marking marking, IPrototypeManager prototypes)
    {
        if (!prototypes.TryIndex(marking.MarkingId, out MarkingPrototype? prototype)
            || prototype.Coloration is not { } colorationId
            || !prototypes.TryIndex(colorationId, out MarkingColorationPrototype? coloration))
        {
            return;
        }

        for (var i = 0; i < marking.MarkingColors.Count; i++)
            marking.SetColor(i, coloration.Strategy.Clamp(marking.MarkingColors[i]));
    }

    public static void Clamp(MarkingSet set, IPrototypeManager prototypes)
    {
        foreach (var markings in set.Markings.Values)
        {
            foreach (var marking in markings)
                Clamp(marking, prototypes);
        }
    }
}
