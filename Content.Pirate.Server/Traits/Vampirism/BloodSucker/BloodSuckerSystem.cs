using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Pirate.Shared.Vampirism.Events;
using Content.Pirate.Server.Traits.Vampirism.Components;
using Content.Pirate.Server.Vampire.Systems;
using Content.Goobstation.Common.Religion;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Systems;
using Content.Server.Popups;
using Content.Server.DoAfter;
using Content.Server.Nutrition.Components;
using Content.Server.Mind;
using Content.Shared.HealthExaminable;
using Content.Shared.Body.Organ;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Atmos.Rotting;
using Content.Server.Nutrition.EntitySystems;
using Content.Pirate.Shared.Vampire.Components;
using Content.Goobstation.Shared.Religion;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Robust.Shared.Audio;

namespace Content.Pirate.Server.Traits.Vampirism.Systems
{
    public sealed class BloodSuckerSystem : EntitySystem
    {
        [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
        [Dependency] private readonly PopupSystem _popups = default!;
        [Dependency] private readonly DoAfterSystem _doAfter = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
        [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly VampireSystem _vampireSystem = default!;
        [Dependency] private readonly HungerSystem _hunger = default!;
        [Dependency] private readonly ThirstSystem _thirst = default!;
        [Dependency] private readonly RottingSystem _rotting = default!;

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

            if (!TryComp<BloodstreamComponent>(victim, out var bloodstream) || args.User == victim || !args.CanAccess)
                return;

            InnateVerb verb = new()
            {
                Act = () =>
                {
                    StartSuccDoAfter(uid, victim, component, bloodstream);
                },
                Text = Loc.GetString("action-name-suck-blood"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Nyanotrasen/Icons/verbiconfangs.png")),
                Priority = 2
            };
            args.Verbs.Add(verb);
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

            if (_prototypeManager.TryIndex<DamageGroupPrototype>("Brute", out var brute) && args.Damageable.Damage.TryGetDamageInGroup(brute, out var bruteTotal)
                && _prototypeManager.TryIndex<DamageGroupPrototype>("Airloss", out var airloss) && args.Damageable.Damage.TryGetDamageInGroup(airloss, out var airlossTotal))
                if (bruteTotal == 0 && airlossTotal == 0)
                    RemComp<BloodSuckedComponent>(uid);
        }

        private void OnDoAfter(EntityUid uid, BloodSuckerComponent component, BloodSuckDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled || args.Args.Target == null)
                return;

            var success = TrySucc(uid, args.Args.Target.Value);
            args.Handled = success;
            if (success)
                args.Repeat = true;
        }

        public void StartSuccDoAfter(EntityUid bloodsucker, EntityUid victim, BloodSuckerComponent? bloodSuckerComponent = null, BloodstreamComponent? stream = null)
        {
            if (!Resolve(bloodsucker, ref bloodSuckerComponent) || !Resolve(victim, ref stream))
                return;

            // Hard checks: block the action before doafter starts

            if (!CanBite(bloodsucker, victim, bloodSuckerComponent, stream))
                return;

            var result = EvaluateSuck(bloodsucker, victim, bloodSuckerComponent, stream);

            // Map SuckResult flags to warning popups — no duplicate condition checks.
            if (result.NoBlood)
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-no-blood", ("target", victim)), victim, bloodsucker, PopupType.Medium);
            else if (result.IsRotten)
                _popups.PopupEntity(Loc.GetString("vampire-blooddrink-rotted"), victim, bloodsucker, PopupType.Medium);
            else if (result.NoBuffs)
                _popups.PopupEntity(Loc.GetString("bloodsucker-not-blood", ("target", victim)), victim, bloodsucker, PopupType.Medium);

            if (result.NoPower)
                _popups.PopupEntity(Loc.GetString("bloodsucker-victim-is-vampire"), victim, bloodsucker, PopupType.MediumCaution);

            if (result.NoPower && TryComp<VampireComponent>(bloodsucker, out var vamp)
                && !vamp.FullPower
                && _vampireSystem.IsProtectedByFaith(victim))
                _popups.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), bloodsucker, bloodsucker, PopupType.MediumCaution);

            // All good — show doafter messages and start.
            _popups.PopupEntity(Loc.GetString("bloodsucker-doafter-start", ("target", victim)), victim, bloodsucker, PopupType.Medium);
            _popups.PopupEntity(Loc.GetString("bloodsucker-doafter-start-victim", ("sucker", bloodsucker)), victim, victim, PopupType.LargeCaution);

            var args = new DoAfterArgs(EntityManager, bloodsucker, bloodSuckerComponent.Delay, new BloodSuckDoAfterEvent(), bloodsucker, target: victim)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                MovementThreshold = 0.01f,
                DistanceThreshold = 2f,
                NeedHand = false
            };

            _doAfter.TryStartDoAfter(args);
        }
        /// <summary>
        /// Returns true if the bite should be blocked completely.
        /// These are fundamental feasibility checks — if any fail, nothing happens.
        /// </summary>
        private bool CanBite(EntityUid bloodsucker, EntityUid victim, BloodSuckerComponent comp, BloodstreamComponent stream)
        {
            // Check for IPCs/silicons — they have no blood.
            if (HasComp<SiliconComponent>(victim))
            {
                _popups.PopupEntity(Loc.GetString("vampire-drink-target-not-viable"), bloodsucker, bloodsucker, PopupType.MediumCaution);
                return false;
            }

            // Bloodsucker must be able to bite — check ingestion ability.
            var ingestAttempt = new IngestionAttemptEvent(IngestionSystem.DefaultFlags);
            RaiseLocalEvent(bloodsucker, ref ingestAttempt);
            if (ingestAttempt.Cancelled)
                return false;

            // Bloodsucker's mouth must be free (no mask/head blocking).
            if (IsMouthBlocked(bloodsucker))
            {
                _popups.PopupEntity(Loc.GetString("vampire-mouth-covered"), bloodsucker, bloodsucker);
                return false;
            }

            // Victim's head must not have pressure-protecting gear (hard to bite through).
            if (_inventorySystem.TryGetSlotEntity(victim, "head", out var head) && HasComp<PressureProtectionComponent>(head))
            {
                _popups.PopupEntity(Loc.GetString("bloodsucker-fail-mouth-blocked", ("target", victim)), victim, bloodsucker, PopupType.Medium);
                return false;
            }

            // Must be in range and unobstructed.
            if (!_interactionSystem.InRangeUnobstructed(bloodsucker, victim))
                return false;

            // Bloodstream solution must exist.
            if (!_solutionSystem.ResolveSolution(victim, stream.BloodSolutionName, ref stream.BloodSolution))
            {
                _popups.PopupEntity(Loc.GetString("vampire-drink-target-not-viable"), bloodsucker, bloodsucker, PopupType.MediumCaution);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Evaluates soft conditions that determine whether the bloodsucker receives buffs.
        /// Returns a record describing what's allowed.
        /// </summary>
        private SuckResult EvaluateSuck(EntityUid bloodsucker, EntityUid victim, BloodSuckerComponent comp, BloodstreamComponent stream)
        {
            var result = new SuckResult();

            // Check if victim is rotten — no buffs from rotten blood.
            if (_rotting.IsRotten(victim))
            {
                result.NoBuffs = true;
                result.IsRotten = true;
            }

            // Check if victim is another vampire or has vampirism trait — no power.
            if (HasComp<VampireComponent>(victim) || HasComp<VampirismComponent>(victim))
                result.NoPower = true;

            // Check if victim has no blood left.
            if (_bloodstreamSystem.GetBloodLevel((victim, stream)) == 0.0f)
                result.NoBlood = true;

            // TastyBloodComponent marks humanoid/animal blood — verify victim is a valid blood source.
            // Without it, the blood is from an alien/creature and grants no buffs.
            if (!HasComp<TastyBloodComponent>(victim))
                result.NoBuffs = true;

            // Faith-protected victims give no power (unless vampire is full power).
            if (TryComp<VampireComponent>(bloodsucker, out var vamp)
                && !vamp.FullPower
                && _vampireSystem.IsProtectedByFaith(victim))
                result.NoPower = true;

            return result;
        }

        public bool TrySucc(EntityUid bloodsucker, EntityUid victim, BloodSuckerComponent? bloodsuckerComp = null)
        {
            // Resolve bloodsucker component
            if (!Resolve(bloodsucker, ref bloodsuckerComp))
                return false;

            // Resolve victim bloodstream
            if (!TryComp<BloodstreamComponent>(victim, out var bloodstream))
                return false;

            // Hard checks — block entirely if infeasible
            if (!CanBite(bloodsucker, victim, bloodsuckerComp, bloodstream))
                return false;

            // Soft checks — evaluate buff eligibility
            var result = EvaluateSuck(bloodsucker, victim, bloodsuckerComp, bloodstream);

            // Success — blood is drawn, effects applied
            _adminLogger.Add(LogType.MeleeHit, LogImpact.Medium, $"{ToPrettyString(bloodsucker):player} sucked blood from {ToPrettyString(victim):target}");

            // Play sound and show popups.
            _audio.PlayPvs("/Audio/Items/drink.ogg", bloodsucker);
            _popups.PopupEntity(Loc.GetString("bloodsucker-blood-sucked-victim", ("sucker", bloodsucker)), victim, victim, PopupType.LargeCaution);
            _popups.PopupEntity(Loc.GetString("bloodsucker-blood-sucked", ("target", victim)), bloodsucker, bloodsucker, PopupType.Medium);
            EnsureComp<BloodSuckedComponent>(victim);

            var unitsSucked = bloodsuckerComp.UnitsToSucc;

            // Drain blood from victim (always happens if blood exists).
            if (!result.NoBlood && bloodstream.BloodSolution != null)
            {
                // The split already contains the victim's chemicals/poisons in proportion to the drawn volume.
                var drawn = _solutionSystem.SplitSolution(bloodstream.BloodSolution.Value, unitsSucked);

                // Transfer them into the bloodsucker, so poisoned blood can still harm them.
                if (TryComp<BloodstreamComponent>(bloodsucker, out var suckerStream)
                    && _solutionSystem.ResolveSolution(bloodsucker, suckerStream.BloodSolutionName, ref suckerStream.BloodSolution))
                {
                    _bloodstreamSystem.TryAddToBloodstream((bloodsucker, suckerStream), drawn);
                }

                // Apply blood buffs (only if soft checks allow).
                if (!result.NoBuffs)
                {
                    // Restore blood level (counters BloodDeficiency drain).
                    _bloodstreamSystem.TryModifyBloodLevel((bloodsucker, null), FixedPoint2.New(unitsSucked * 0.05f));

                    // Reduce bleeding.
                    _bloodstreamSystem.TryModifyBleedAmount((bloodsucker, null), unitsSucked * -0.05f);

                    // Satisfy hunger.
                    _hunger.ModifyHunger(bloodsucker, unitsSucked * 6f);

                    // Satisfy thirst directly.
                    if (TryComp<ThirstComponent>(bloodsucker, out var thirst))
                        _thirst.ModifyThirst(bloodsucker, thirst, unitsSucked * 2f);

                    // Heal brute and burn damage — use concrete type IDs, split evenly per group.
                    var healing = new DamageSpecifier();
                    // Brute group: Blunt, Slash, Piercing (3 types)
                    var brutePerType = unitsSucked * -0.6f / 3f;
                    healing.DamageDict["Blunt"] = brutePerType;
                    healing.DamageDict["Slash"] = brutePerType;
                    healing.DamageDict["Piercing"] = brutePerType;
                    // Burn group: Heat, Shock, Cold, Caustic (4 types)
                    var burnPerType = unitsSucked * -0.25f / 4f;
                    healing.DamageDict["Heat"] = burnPerType;
                    healing.DamageDict["Shock"] = burnPerType;
                    healing.DamageDict["Cold"] = burnPerType;
                    healing.DamageDict["Caustic"] = burnPerType;
                    _damageableSystem.TryChangeDamage(bloodsucker, healing, true);
                }

                // Antag vampires feed on the blood to power their abilities.
                // TryGainVampirePower has its own checks for vampire victims, faith, etc.
                if (TryComp<VampireComponent>(bloodsucker, out var vamp))
                    TryGainVampirePower(bloodsucker, vamp, victim, unitsSucked);
            }
            else if (result.NoBlood)
            {
                // No blood to drain — do not proceed to pierce damage or return success.
                return false;
            }

            // Add a little pierce damage to victim (always happens when blood exists).
            DamageSpecifier pierce = new();
            pierce.DamageDict.Add("Piercing", 1);
            _damageableSystem.TryChangeDamage(victim, pierce, true, true);

            return true;
        }

        /// <summary>
        /// Antag-only follow-up to a successful bite. Feeds the vampire's power system.
        /// </summary>
        private void TryGainVampirePower(EntityUid uid, VampireComponent comp, EntityUid target, float drunkAmount)
        {
            if (drunkAmount <= 0f)
                return;

            // Drinking another vampire's blood grants no power.
            if (HasComp<VampireComponent>(target) || HasComp<VampirismComponent>(target))
            {
                _popups.PopupEntity(Loc.GetString("bloodsucker-victim-is-vampire"), uid, uid, PopupType.MediumCaution);
                return;
            }

            // Holy people give no power unless we are at full power.
            if (_vampireSystem.IsProtectedByFaith(target) && !comp.FullPower)
            {
                _popups.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), uid, uid, PopupType.MediumCaution);
                return;
            }

            // Only so much power can be wrung out of a single victim.
            var drunkFromTarget = comp.BloodDrunkFromTargets.GetValueOrDefault(target, 0);
            if (drunkFromTarget >= comp.MaxBloodPerTarget)
            {
                _popups.PopupEntity(Loc.GetString("vampire-drink-target-hard-max", ("amount", comp.MaxBloodPerTarget)), uid, uid, PopupType.MediumCaution);
                return;
            }

            // Silicons and (optionally) the dead are not a usable source of power.
            if (HasComp<SiliconComponent>(target)
                || !TryComp<MobStateComponent>(target, out var mobState)
                || (mobState.CurrentState == MobState.Dead && comp.DeadEfficiency == 0f))
            {
                _popups.PopupEntity(Loc.GetString("vampire-drink-target-not-viable"), uid, uid, PopupType.MediumCaution);
                return;
            }

            // How much of the drawn blood is actually usable as power.
            var targetIsHumanoid = HasComp<HumanoidAppearanceComponent>(target);
            var efficiency = targetIsHumanoid ? comp.HumanoidEfficiency : comp.NonHumanoidEfficiency;
            if (mobState.CurrentState == MobState.Dead)
                efficiency *= comp.DeadEfficiency;
            if (TryComp<PerishableComponent>(target, out var rot))
                efficiency *= GetRotEfficiency(comp, rot.Stage);

            if (efficiency <= 0f)
            {
                _popups.PopupEntity(Loc.GetString("vampire-drink-target-rot"), uid, uid, PopupType.MediumCaution);
                return;
            }

            var bloodGained = MathF.Min(drunkAmount * efficiency * 2, comp.MaxBloodPerTarget - drunkFromTarget);
            if (bloodGained <= 0f)
                return;

            _vampireSystem.AddBlood(uid, comp, bloodGained, target, countTotalBlood: targetIsHumanoid);
        }

        private static float GetRotEfficiency(VampireComponent comp, int stage) => stage switch
        {
            0 => comp.Rot0Efficiency,
            1 => comp.Rot1Efficiency,
            2 => comp.Rot2Efficiency,
            3 => comp.Rot3Efficiency,
            _ => comp.Rot4Efficiency,
        };



        private bool IsMouthBlocked(EntityUid uid)
        {
            if (!HasComp<InventoryComponent>(uid))
                return false;

            var slots = new[] { "mask", "head" };
            foreach (var slot in slots)
                if (_inventorySystem.TryGetSlotEntity(uid, slot, out var ent) &&
                    TryComp<IngestionBlockerComponent>(ent.Value, out var blocker) && blocker.Enabled)
                    return true;

            return false;
        }

        /// <summary>
        /// Result of soft-check evaluation. Determines what buffs the bloodsucker receives.
        /// </summary>
        private sealed class SuckResult
        {
            /// <summary>If true, blood is still drained but no healing/hunger/thirst/blood restore.</summary>
            public bool NoBuffs;

            /// <summary>If true, vampire power system is not fed.</summary>
            public bool NoPower;

            /// <summary>If true, victim has no blood to drain.</summary>
            public bool NoBlood;

            /// <summary>If true, victim is rotten — specific popup feedback.</summary>
            public bool IsRotten;
        }
    }
}
