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
        foreach (var list in markings.Markings.Values)
            changed |= list.RemoveAll(marking => Markings.TryGetValue(marking.MarkingId, out var proto) && !CanPlayerUseMarking(proto, ckey)) > 0;

        return changed;
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
