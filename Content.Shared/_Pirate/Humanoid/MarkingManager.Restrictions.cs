using System.Linq;

namespace Content.Shared.Humanoid.Markings;

public sealed partial class MarkingManager
{
    public static bool CanPlayerUseMarking(MarkingPrototype marking, string? ckey) =>
        marking.PlayerRestrictions == null || marking.PlayerRestrictions.Count == 0 ||
        !string.IsNullOrWhiteSpace(ckey) && marking.PlayerRestrictions.Contains(ckey, StringComparer.OrdinalIgnoreCase);

    public bool FilterPlayerRestrictedMarkings(MarkingSet markings, string? ckey)
    {
        var changed = false;
        foreach (var (category, list) in markings.Markings.ToArray())
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!Markings.TryGetValue(list[i].MarkingId, out var proto) || CanPlayerUseMarking(proto, ckey))
                    continue;

                markings.Remove(category, i);
                changed = true;
            }

            if (list.Count == 0)
                markings.Markings.Remove(category);
        }

        return changed;
    }

    public List<Marking> FilterPlayerRestrictedMarkings(List<Marking> markings, string? ckey)
    {
        return markings.Where(marking => !Markings.TryGetValue(marking.MarkingId, out var proto) || CanPlayerUseMarking(proto, ckey)).ToList();
    }

    public List<Marking> FilterValidMarkings(List<Marking> markings, string species, Sex sex, string? ckey)
    {
        var valid = new List<Marking>();
        foreach (var marking in markings)
        {
            if (CanBeApplied(species, sex, marking, _prototypeManager, ckey))
                valid.Add(marking);
        }

        return valid;
    }
}
