using Content.Server._EinsteinEngines.Language;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Content.Shared._Pirate.Traits;
using Content.Shared._Pirate.Traits.Conditions;
using Content.Shared._Pirate.Traits.Effects;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Traits;

public sealed class TraitSystem : SharedTraitSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedHandsSystem _sharedHandsSystem = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly ILogManager _log = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null ||
            !_prototypeManager.TryIndex<JobPrototype>(args.JobId, out var jobProto) ||
            !jobProto.ApplyTraits)
            return;

        var (validTraits, disabledTraits) = ValidateTraits(args.Mob, args.Profile.TraitPreferences, args.Player, args.JobId, args.Profile.Species, args.Profile);

        foreach (var traitId in validTraits)
        {
            if (!_prototypeManager.TryIndex(traitId, out var trait))
                continue;

            ApplyTrait(args.Mob, trait);
        }

        if (disabledTraits.Count > 0)
        {
            RaiseNetworkEvent(new DisabledTraitsEvent(disabledTraits), args.Player);
        }
    }

    private (HashSet<ProtoId<TraitPrototype>> Valid, Dictionary<ProtoId<TraitPrototype>, List<string>> Disabled) ValidateTraits(
        EntityUid player,
        IReadOnlySet<ProtoId<TraitPrototype>> selectedTraits,
        Robust.Shared.Player.ICommonSession? session,
        string? jobId,
        string? speciesId,
        Content.Shared.Preferences.HumanoidCharacterProfile? profile)
    {
        var validTraits = new HashSet<ProtoId<TraitPrototype>>();
        var disabledTraits = new Dictionary<ProtoId<TraitPrototype>, List<string>>();

        var context = new TraitConditionContext
        {
            Player = player,
            Session = session,
            EntMan = EntityManager,
            Proto = _prototypeManager,
            CompFactory = _factory,
            LogMan = _log,
            JobId = jobId,
            SpeciesId = speciesId,
            Profile = profile,
        };

        foreach (var traitId in selectedTraits)
        {
            if (!_prototypeManager.TryIndex(traitId, out var trait))
                continue;

            if (speciesId is { } selectedSpecies && (trait.ExcludedSpecies.Contains(selectedSpecies) ||
                (trait.IncludedSpecies.Count > 0 && !trait.IncludedSpecies.Contains(selectedSpecies))))
            {
                continue;
            }

            var conditionsMet = true;
            foreach (var condition in trait.Conditions)
            {
                if (!condition.Evaluate(context))
                {
                    conditionsMet = false;
                    break;
                }
            }

            if (!conditionsMet)
            {
                var reasons = new List<string>();
                foreach (var condition in trait.Conditions)
                {
                    if (!condition.Evaluate(context))
                    {
                        var tooltip = condition.GetTooltip(_prototypeManager, Loc);
                        if (!string.IsNullOrEmpty(tooltip))
                            reasons.Add(tooltip);
                    }
                }
                disabledTraits[traitId] = reasons;
                continue;
            }

            var conflictFound = false;
            foreach (var validTraitId in validTraits)
            {
                if (trait.Conflicts.Contains(validTraitId))
                {
                    conflictFound = true;
                    break;
                }

                if (_prototypeManager.TryIndex(validTraitId, out var validTrait) && validTrait.Conflicts.Contains(traitId))
                {
                    conflictFound = true;
                    break;
                }
            }

            if (conflictFound)
                continue;

            validTraits.Add(traitId);
        }

        return (validTraits, disabledTraits);
    }

    private void ApplyTrait(EntityUid player, TraitPrototype trait)
    {
        var traitComponent = EnsureComp<TraitComponent>(player);
        traitComponent.ActiveTraits.Add(trait.ID);

        EntityManager.AddComponents(player, trait.Components, false);

        var language = EntityManager.System<LanguageSystem>();

        if (trait.RemoveLanguagesSpoken is not null)
            foreach (var lang in trait.RemoveLanguagesSpoken)
                language.RemoveLanguage(player, lang, true, false);

        if (trait.RemoveLanguagesUnderstood is not null)
            foreach (var lang in trait.RemoveLanguagesUnderstood)
                language.RemoveLanguage(player, lang, false, true);

        if (trait.LanguagesSpoken is not null)
            foreach (var lang in trait.LanguagesSpoken)
                language.AddLanguage(player, lang, true, false);

        if (trait.LanguagesUnderstood is not null)
            foreach (var lang in trait.LanguagesUnderstood)
                language.AddLanguage(player, lang, false, true);

        if (trait.TraitGear != null)
        {
            var coords = Transform(player).Coordinates;
            var inhandEntity = Spawn(trait.TraitGear, coords);
            _sharedHandsSystem.TryPickupAnyHand(player, inhandEntity);
        }

        var context = new TraitEffectContext
        {
            Player = player,
            EntMan = EntityManager,
            Proto = _prototypeManager,
            CompFactory = _factory,
            LogMan = _log,
            Transform = Transform(player),
        };

        foreach (var effect in trait.Effects)
        {
            effect.Apply(context);
        }
    }
}
