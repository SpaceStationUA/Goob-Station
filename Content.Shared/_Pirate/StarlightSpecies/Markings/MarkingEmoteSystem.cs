using System.Linq;
using Content.Shared._Shitmed.Humanoid.Events;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.StarlightSpecies.Markings;

[RegisterComponent]
public sealed partial class MarkingEmotesComponent : Component
{
    public HashSet<ProtoId<EmotePrototype>> Granted = new();
}

/// <summary>Keep shell emotes in sync with profile changes and shell removal/regrowth.</summary>
public sealed class MarkingEmoteSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpeechComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ProfileLoadFinishedEvent>(OnProfile);
        SubscribeLocalEvent<HumanoidAppearanceComponent, HumanoidMarkingsChangedEvent>(OnMarkings);
    }

    private void OnInit(Entity<SpeechComponent> ent, ref MapInitEvent args) => Refresh(ent);
    private void OnProfile(EntityUid uid, HumanoidAppearanceComponent comp, ProfileLoadFinishedEvent args) => Refresh(uid);
    private void OnMarkings(Entity<HumanoidAppearanceComponent> ent, ref HumanoidMarkingsChangedEvent args) => Refresh(ent);

    public void Refresh(EntityUid uid)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var appearance) || !TryComp<SpeechComponent>(uid, out var speech))
            return;
        var ids = appearance.MarkingSet.Markings.Values.SelectMany(m => m).Select(m => m.MarkingId).ToHashSet();
        var desired = new HashSet<ProtoId<EmotePrototype>>();
        foreach (var id in ids)
        {
            if (!_prototypes.TryIndex<MarkingPrototype>(id, out var marking) || marking.Emotes == null)
                continue;
            foreach (var emote in marking.Emotes)
            {
                if (emote.RequiredMarkings != null && !emote.RequiredMarkings.All(m => ids.Contains(m.Id)))
                    continue;
                if (emote.RequiredMarkingsAny != null && !emote.RequiredMarkingsAny.Any(m => ids.Contains(m.Id)))
                    continue;
                desired.Add(emote.EmotePrototype);
            }
        }
        if (desired.Count == 0 && !HasComp<MarkingEmotesComponent>(uid))
            return;
        var state = EnsureComp<MarkingEmotesComponent>(uid);
        speech.AllowedEmotes.RemoveAll(e => state.Granted.Contains(e));
        state.Granted.Clear();
        foreach (var emote in desired)
        {
            if (speech.AllowedEmotes.Contains(emote))
                continue;
            speech.AllowedEmotes.Add(emote);
            state.Granted.Add(emote);
        }
        Dirty(uid, speech);
    }
}
