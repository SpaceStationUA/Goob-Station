using System.Linq;
using Content.Shared._Pirate.StarlightSpecies;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.Muting;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.StarlightSpecies;

public sealed class OrganTongueSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrganComponent, OrganAddedToBodyEvent>(OnAdded);
        SubscribeLocalEvent<OrganTongueComponent, OrganRemovedFromBodyEvent>(OnRemoved);
    }

    private void OnAdded(Entity<OrganComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (TryComp<OrganTongueComponent>(ent, out var tongue))
        {
            RestoreSpeech(args.Body);
            Apply(args.Body, tongue);
        }
        else if (ent.Comp.SlotId == "tongue" && HasComp<TongueMutedComponent>(args.Body))
        {
            // Pirate: native tongues have no Starlight voice data, but can replace an extracted tongue.
            RestoreSpeech(args.Body);
            var prototype = MetaData(args.Body).EntityPrototype;
            if (prototype != null && prototype.TryComp<VocalComponent>(out var originalVocal, _factory) &&
                TryComp<VocalComponent>(args.Body, out var vocal))
            {
                vocal.Sounds = originalVocal.Sounds == null ? null : new(originalVocal.Sounds);
                vocal.EmoteSounds = null;
                var ev = new SoundsChangedEvent();
                RaiseLocalEvent(args.Body, ref ev);
                Dirty(args.Body, vocal);
            }
        }
    }

    private void OnRemoved(Entity<OrganTongueComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (TryComp<SpeechComponent>(args.OldBody, out var speech))
        {
            speech.AllowedEmotes.RemoveAll(e => ent.Comp.AllowedEmotes.Contains(e));
            Dirty(args.OldBody, speech);
        }
        var tongue = _body.GetBodyOrgans(args.OldBody).Where(o => o.Id != ent.Owner)
            .Select(o => CompOrNull<OrganTongueComponent>(o.Id)).FirstOrDefault(o => o != null);
        if (tongue != null)
        {
            Apply(args.OldBody, tongue);
            return;
        }
        if (TryComp<VocalComponent>(args.OldBody, out var vocal))
        {
            vocal.Sounds = null;
            vocal.EmoteSounds = null;
            Dirty(args.OldBody, vocal);
        }
        Mute(args.OldBody);
        EntityManager.System<Content.Shared._Pirate.StarlightSpecies.Markings.MarkingEmoteSystem>().Refresh(args.OldBody);
    }

    private void Apply(EntityUid body, OrganTongueComponent tongue)
    {
        if (tongue.IsMuted)
            Mute(body);
        if (tongue.Sounds != null && TryComp<VocalComponent>(body, out var vocal))
        {
            vocal.Sounds = new(tongue.Sounds);
            var ev = new SoundsChangedEvent();
            RaiseLocalEvent(body, ref ev);
            Dirty(body, vocal);
        }
        if (TryComp<SpeechComponent>(body, out var speech))
        {
            speech.AllowedEmotes = speech.AllowedEmotes.Union(tongue.AllowedEmotes).ToList();
            if (tongue.AllowAllVocalEmotes)
                speech.AllowedEmotes = speech.AllowedEmotes.Union(_prototypes.EnumeratePrototypes<EmotePrototype>()
                    .Where(e => e.Category.HasFlag(EmoteCategory.Vocal)).Select(e => new ProtoId<EmotePrototype>(e.ID))).ToList();
            Dirty(body, speech);
            EntityManager.System<Content.Shared._Pirate.StarlightSpecies.Markings.MarkingEmoteSystem>().Refresh(body);
        }
    }

    private void Mute(EntityUid body)
    {
        // Do not claim or remove muteness supplied by a trait or another system.
        if (HasComp<MutedComponent>(body))
            return;
        EnsureComp<MutedComponent>(body);
        EnsureComp<TongueMutedComponent>(body);
    }

    private void RestoreSpeech(EntityUid body)
    {
        if (!HasComp<TongueMutedComponent>(body))
            return;
        RemComp<MutedComponent>(body);
        RemComp<TongueMutedComponent>(body);
    }
}

[RegisterComponent]
public sealed partial class TongueMutedComponent : Component;
