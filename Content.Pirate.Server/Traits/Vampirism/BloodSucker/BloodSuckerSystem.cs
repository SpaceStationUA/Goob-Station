using Content.Goobstation.Common.Religion;
using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Server.Traits.Vampirism.Components;
using Content.Pirate.Server.Vampire.Systems;
using Content.Pirate.Shared.Vampire.Components;
using Content.Pirate.Shared.Vampirism;
using Content.Pirate.Shared.Vampirism.Events;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.Rotting;
using Content.Server.Body.Systems;
using Content.Server.DoAfter;
using Content.Server.Drunk;
using Content.Server.Popups;
using Content.Shared.Administration.Logs;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Drunk;
using Content.Shared.HealthExaminable;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Verbs;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.Traits.Vampirism.Systems
{
    public sealed partial class BloodSuckerSystem : EntitySystem
    {
        private const string PiercingDamage = "Piercing";

        private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
        private static readonly ProtoId<DamageGroupPrototype> AirlossGroup = "Airloss";
        private static readonly string[] MouthSlots = { "mask", "head" };

        [Dependency] private SharedSolutionContainerSystem _solutionSystem = default!;
        [Dependency] private PopupSystem _popups = default!;
        [Dependency] private DoAfterSystem _doAfter = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private DamageableSystem _damageableSystem = default!;
        [Dependency] private InventorySystem _inventorySystem = default!;
        [Dependency] private ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private SharedInteractionSystem _interactionSystem = default!;
        [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private SharedAudioSystem _audio = default!;
        [Dependency] private VampireSystem _vampireSystem = default!;
        [Dependency] private HungerSystem _hunger = default!;
        [Dependency] private ThirstSystem _thirst = default!;
        [Dependency] private RottingSystem _rotting = default!;
        [Dependency] private StatusEffectsSystem _statusEffects = default!;
        [Dependency] private DrunkSystem _drunk = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BloodSuckerComponent, GetVerbsEvent<InnateVerb>>(AddSuccVerb);
            SubscribeLocalEvent<BloodSuckedComponent, HealthBeingExaminedEvent>(OnHealthExamined);
            SubscribeLocalEvent<BloodSuckedComponent, DamageChangedEvent>(OnDamageChanged);
            SubscribeLocalEvent<BloodSuckerComponent, BloodSuckDoAfterEvent>(OnDoAfter);
        }

        private void AddSuccVerb(EntityUid uid, BloodSuckerComponent component, GetVerbsEvent<InnateVerb> args)
        {
            var victim = args.Target;
            if (uid == victim
                || !args.CanAccess
                || !HasComp<BloodstreamComponent>(victim)
                || HasComp<SiliconComponent>(victim))
                return;

            args.Verbs.Add(new InnateVerb
            {
                Act = () => StartSuccDoAfter(uid, victim, component),
                Text = Loc.GetString("action-name-suck-blood"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Nyanotrasen/Icons/verbiconfangs.png")),
                Priority = 2,
            });
        }

        private void OnHealthExamined(EntityUid uid, BloodSuckedComponent component, HealthBeingExaminedEvent args)
        {
            args.Message.PushNewline();
            args.Message.AddMarkup(Loc.GetString("bloodsucked-health-examine", ("target", uid)));
        }

        private void OnDamageChanged(EntityUid uid, BloodSuckedComponent component, DamageChangedEvent args)
        {
            if (args.DamageIncreased)
                return;

            if (!_prototypeManager.TryIndex(BruteGroup, out var brute)
                || !_prototypeManager.TryIndex(AirlossGroup, out var airloss))
                return;

            var damage = args.Damageable.Damage;
            var hasBrute = damage.TryGetDamageInGroup(brute, out var bruteTotal) && bruteTotal > FixedPoint2.Zero;
            var hasAirloss = damage.TryGetDamageInGroup(airloss, out var airlossTotal) && airlossTotal > FixedPoint2.Zero;

            if (!hasBrute && !hasAirloss)
                RemComp<BloodSuckedComponent>(uid);
        }

        private void OnDoAfter(EntityUid uid, BloodSuckerComponent component, BloodSuckDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled || args.Args.Target is not { } victim)
                return;

            if (!TryComp<BloodstreamComponent>(victim, out var stream))
                return;

            // Hard checks repeat with every sip; the taste is only composed on the first one.
            var firstSip = !args.Tasted;
            if (!CanBite(uid, victim, stream, component, out var taste, includeTaste: firstSip))
                return;

            if (!TryDrain(uid, victim, component, stream, firstSip ? taste : args.Result))
                return;

            // The blood is only described once the first sip actually happened.
            if (firstSip)
            {
                args.Tasted = true;
                args.Result = taste;
                _popups.PopupEntity(Loc.GetString("bloodsucker-taste-popup", ("taste", taste.Taste)), uid, uid, PopupType.Medium);
            }

            if (!args.Logged)
            {
                args.Logged = true;
                _adminLogger.Add(LogType.MeleeHit,
                    LogImpact.Medium,
                    $"{ToPrettyString(uid):player} sucked blood from {ToPrettyString(victim):target}");
            }

            args.Handled = true;
            args.Repeat = true;
        }

        public void StartSuccDoAfter(EntityUid bloodsucker,
            EntityUid victim,
            BloodSuckerComponent? bloodSuckerComponent = null,
            BloodstreamComponent? stream = null)
        {
            if (!Resolve(bloodsucker, ref bloodSuckerComponent) || !Resolve(victim, ref stream))
                return;

            // Hard checks before the do-after starts; the taste is composed after the first sip.
            if (!CanBite(bloodsucker, victim, stream, bloodSuckerComponent, out _, includeTaste: false))
                return;

            _popups.PopupEntity(Loc.GetString("bloodsucker-doafter-start", ("target", victim)), victim, bloodsucker, PopupType.Medium);
            _popups.PopupEntity(Loc.GetString("bloodsucker-doafter-start-victim", ("sucker", bloodsucker)), victim, victim, PopupType.LargeCaution);

            var doAfter = new DoAfterArgs(EntityManager,
                bloodsucker,
                bloodSuckerComponent.Delay,
                new BloodSuckDoAfterEvent(),
                bloodsucker,
                target: victim)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                MovementThreshold = bloodSuckerComponent.MovementThreshold,
                DistanceThreshold = bloodSuckerComponent.BiteRange,
                NeedHand = false,
            };

            _doAfter.TryStartDoAfter(doAfter);
        }

        /// <summary>
        /// Runs every check that gates a sip and tells the bloodsucker which one failed.
        /// When <paramref name="includeTaste"/> is set, the taste is composed at the same time,
        /// so a passed check already carries everything the sip needs.
        /// </summary>
        private bool CanBite(EntityUid bloodsucker,
            EntityUid victim,
            BloodstreamComponent stream,
            BloodSuckerComponent comp,
            out BloodTasteResult taste,
            bool includeTaste = true)
        {
            taste = new BloodTasteResult();

            if (HasComp<SiliconComponent>(victim))
            {
                _popups.PopupEntity(Loc.GetString("vampire-drink-target-not-viable"), bloodsucker, bloodsucker, PopupType.MediumCaution);
                return false;
            }

            // Blocked ingestion stays silent: the game already shows why the mouth is unusable.
            var ingestAttempt = new IngestionAttemptEvent(IngestionSystem.DefaultFlags);
            RaiseLocalEvent(bloodsucker, ref ingestAttempt);
            if (ingestAttempt.Cancelled)
                return false;

            if (HasMouthCovered(bloodsucker))
            {
                _popups.PopupEntity(Loc.GetString("vampire-mouth-covered"), bloodsucker, bloodsucker);
                return false;
            }

            if (_inventorySystem.TryGetSlotEntity(victim, "head", out var head)
                && HasComp<PressureProtectionComponent>(head))
            {
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-mouth-blocked", ("target", victim)), victim, bloodsucker, PopupType.Medium);
                return false;
            }

            if (!_interactionSystem.InRangeUnobstructed(bloodsucker, victim, comp.BiteRange))
            {
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-too-far"), bloodsucker, bloodsucker, PopupType.Medium);
                return false;
            }

            if (!_solutionSystem.ResolveSolution(victim, stream.BloodSolutionName, ref stream.BloodSolution))
            {
                _popups.PopupEntity(Loc.GetString("vampire-drink-target-not-viable"), bloodsucker, bloodsucker, PopupType.MediumCaution);
                return false;
            }

            if (IsBloodless(victim, stream))
            {
                var key = HasComp<BloodSuckedComponent>(victim)
                    ? "bloodsucker-fail-no-blood-bloodsucked"
                    : "bloodsucker-fail-no-blood";

                _popups.PopupEntity(Loc.GetString(key, ("target", victim)), bloodsucker, bloodsucker, PopupType.Medium);
                return false;
            }

            if (includeTaste)
                taste = EvaluateSuck(bloodsucker, victim);

            return true;
        }

        /// <summary>
        /// Evaluates the soft conditions and composes the taste of the blood.
        /// </summary>
        private BloodTasteResult EvaluateSuck(EntityUid bloodsucker, EntityUid victim)
        {
            var result = new BloodTasteResult();
            var modifiers = new List<string>();

            if (_rotting.IsRotten(victim))
            {
                result.GrantsBuffs = false;
                modifiers.Add(Loc.GetString("bloodsucker-taste-rotten"));
            }
            else if (TryComp<MobStateComponent>(victim, out var mobState) && mobState.CurrentState == MobState.Dead)
            {
                modifiers.Add(Loc.GetString("bloodsucker-taste-stale"));
            }

            if (HasComp<VampireComponent>(victim) || HasComp<VampirismComponent>(victim))
            {
                result.GrantsPower = false;
                modifiers.Add(Loc.GetString("bloodsucker-taste-vampire"));
            }

            if (HasComp<BibleUserComponent>(victim))
                modifiers.Add(Loc.GetString("bloodsucker-taste-holy"));

            // Drunkenness is a status effect rather than raw ethanol in the blood: ethanol
            // metabolises away long before the victim actually sobers up.
            if (_statusEffects.HasStatusEffect(victim, SharedDrunkSystem.Drunk))
            {
                result.GetsDrunk = true;
                modifiers.Add(Loc.GetString("bloodsucker-taste-drunk"));
            }

            var raceTaste = Loc.GetString("bloodsucker-taste-race-generic");
            var customTaste = string.Empty;
            if (TryComp<TastyBloodComponent>(victim, out var tastyBlood))
            {
                raceTaste = Loc.GetString(tastyBlood.RaceTaste);
                customTaste = tastyBlood.CustomTaste;

                if (!tastyBlood.GivesBenefits)
                {
                    result.GrantsBuffs = false;
                    result.GrantsPower = false;
                    modifiers.Add(Loc.GetString("bloodsucker-taste-not-nourishing"));
                }
            }
            else
            {
                result.GrantsBuffs = false;
                modifiers.Add(Loc.GetString("bloodsucker-taste-not-nourishing"));
            }

            // Faith protection only matters to actual vampires; full-power vampires ignore it.
            if (TryComp<VampireComponent>(bloodsucker, out var vampire)
                && !vampire.FullPower
                && _vampireSystem.IsProtectedByFaith(victim))
                result.GrantsPower = false;

            result.Taste = modifiers.Count == 0
                ? Loc.GetString("bloodsucker-taste-base", ("race", raceTaste))
                : Loc.GetString("bloodsucker-taste-modified", ("race", raceTaste), ("modifiers", string.Join(", ", modifiers)));

            if (!string.IsNullOrEmpty(customTaste))
                result.Taste = Loc.GetString("bloodsucker-taste-custom", ("taste", result.Taste), ("custom", customTaste));

            return result;
        }

        /// <summary>
        /// Drains one portion of blood and applies everything that follows from the cached taste.
        /// </summary>
        private bool TryDrain(EntityUid bloodsucker,
            EntityUid victim,
            BloodSuckerComponent comp,
            BloodstreamComponent victimBlood,
            BloodTasteResult taste)
        {
            if (IsBloodless(victim, victimBlood))
                return false;

            // Resolve the recipient before splitting the victim's solution, otherwise a failure
            // here would silently delete the drawn blood.
            if (!TryComp<BloodstreamComponent>(bloodsucker, out var suckerBlood)
                || !_solutionSystem.ResolveSolution(bloodsucker, suckerBlood.BloodSolutionName, ref suckerBlood.BloodSolution))
                return false;

            if (victimBlood.BloodSolution is not { } victimSolution)
                return false;

            var units = comp.UnitsToSucc;
            var drawn = _solutionSystem.SplitSolution(victimSolution, units);
            _bloodstreamSystem.TryAddToBloodstream((bloodsucker, suckerBlood), drawn);

            // Copy the victim's drunkenness instead of pouring ethanol into the already drawn
            // blood, so the bloodsucker gets tipsy without extra reagents to metabolise.
            if (taste.GetsDrunk)
                _drunk.TryApplyDrunkenness(bloodsucker, TimeSpan.FromSeconds(comp.DrunkPerUnit * units));

            if (taste.GrantsBuffs)
                ApplyBuffs(bloodsucker, suckerBlood, comp, units);

            if (TryComp<VampireComponent>(bloodsucker, out var vampire))
                TryGainVampirePower(bloodsucker, vampire, victim, units, taste.GrantsPower);

            _audio.PlayPvs("/Audio/Items/drink.ogg", bloodsucker);
            _popups.PopupEntity(Loc.GetString("bloodsucker-blood-sucked-victim", ("sucker", bloodsucker)), victim, victim, PopupType.LargeCaution);
            _popups.PopupEntity(Loc.GetString("bloodsucker-blood-sucked", ("target", victim)), bloodsucker, bloodsucker, PopupType.Medium);
            EnsureComp<BloodSuckedComponent>(victim);

            if (comp.PierceDamage > FixedPoint2.Zero)
            {
                var pierce = new DamageSpecifier();
                pierce.DamageDict[PiercingDamage] = comp.PierceDamage;
                _damageableSystem.TryChangeDamage(victim, pierce, true, true);
            }

            return true;
        }

        private void ApplyBuffs(EntityUid bloodsucker, BloodstreamComponent suckerBlood, BloodSuckerComponent comp, float units)
        {
            _bloodstreamSystem.TryModifyBloodLevel((bloodsucker, suckerBlood), comp.BloodRestorePerUnit * units);
            _bloodstreamSystem.TryModifyBleedAmount((bloodsucker, suckerBlood), -comp.BleedReductionPerUnit * units);
            _hunger.ModifyHunger(bloodsucker, comp.HungerPerUnit * units);

            if (TryComp<ThirstComponent>(bloodsucker, out var thirst))
                _thirst.ModifyThirst(bloodsucker, thirst, comp.ThirstPerUnit * units);

            var healing = BuildHealing(comp, units);
            if (!healing.Empty)
                _damageableSystem.TryChangeDamage(bloodsucker, healing, true);
        }

        private DamageSpecifier BuildHealing(BloodSuckerComponent comp, float units)
        {
            var healing = new DamageSpecifier();

            foreach (var (groupId, amount) in comp.HealGroups)
            {
                if (amount <= FixedPoint2.Zero || !_prototypeManager.TryIndex(groupId, out var group))
                    continue;

                healing += new DamageSpecifier(group, -(amount * units));
            }

            return healing;
        }

        /// <summary>
        /// Antag-only follow-up to a successful sip. The taste result already decided whether
        /// this blood can feed the vampire at all, so only the per-target math remains here.
        /// </summary>
        private void TryGainVampirePower(EntityUid uid, VampireComponent comp, EntityUid target, float units, bool grantsPower)
        {
            if (units <= 0f || !grantsPower)
                return;

            // Only so much power can be wrung out of a single victim.
            var drunkFromTarget = comp.BloodDrunkFromTargets.GetValueOrDefault(target, 0);
            if (drunkFromTarget >= comp.MaxBloodPerTarget)
            {
                _popups.PopupEntity(Loc.GetString("vampire-drink-target-hard-max", ("amount", comp.MaxBloodPerTarget)), uid, uid, PopupType.MediumCaution);
                return;
            }

            if (HasComp<SiliconComponent>(target)
                || !TryComp<MobStateComponent>(target, out var mobState)
                || (mobState.CurrentState == MobState.Dead && comp.DeadEfficiency == 0f))
                return;

            var targetIsHumanoid = HasComp<HumanoidAppearanceComponent>(target);
            var efficiency = targetIsHumanoid ? comp.HumanoidEfficiency : comp.NonHumanoidEfficiency;
            if (mobState.CurrentState == MobState.Dead)
                efficiency *= comp.DeadEfficiency;
            if (TryComp<PerishableComponent>(target, out var rot))
                efficiency *= GetRotEfficiency(comp, rot.Stage);

            if (efficiency <= 0f)
                return;

            var bloodGained = MathF.Min(units * efficiency * 2, comp.MaxBloodPerTarget - drunkFromTarget);
            if (bloodGained <= 0f)
                return;

            _vampireSystem.AddBlood(uid, comp, bloodGained, target, countTotalBlood: targetIsHumanoid);
        }

        private bool IsBloodless(EntityUid victim, BloodstreamComponent stream)
        {
            return _bloodstreamSystem.GetBloodLevel((victim, stream)) <= 0f;
        }

        private bool HasMouthCovered(EntityUid uid)
        {
            if (!TryComp<InventoryComponent>(uid, out var inventory))
                return false;

            foreach (var slot in MouthSlots)
            {
                if (_inventorySystem.TryGetSlotEntity(uid, slot, out var ent, inventory)
                    && TryComp<IngestionBlockerComponent>(ent, out var blocker)
                    && blocker.Enabled)
                    return true;
            }

            return false;
        }

        private static float GetRotEfficiency(VampireComponent comp, int stage) => stage switch
        {
            0 => comp.Rot0Efficiency,
            1 => comp.Rot1Efficiency,
            2 => comp.Rot2Efficiency,
            3 => comp.Rot3Efficiency,
            _ => comp.Rot4Efficiency,
        };

    }
}
