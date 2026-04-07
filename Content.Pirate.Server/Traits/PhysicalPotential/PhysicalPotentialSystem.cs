using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Sprinting;
using Content.Shared._Pirate.Stunnable;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Cloning.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Pirate.Shared.Traits.PhysicalPotential;
using Robust.Shared.Random;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Content.Shared.Alert;

namespace Content.Pirate.Server.Traits.PhysicalPotential
{
    public sealed class PhysicalPotentialSystem : EntitySystem
    {
        private static readonly string[] PhysicalDamageTypes = { "Blunt", "Slash", "Piercing" };

        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly HungerSystem _hungerSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alertsSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<PhysicalPotentialComponent, DamageModifyEvent>(OnDamageModify);
            SubscribeLocalEvent<PhysicalPotentialComponent, ForcedStandSucceededEvent>(OnForcedStandSucceeded);

            SubscribeLocalEvent<PhysicalPotentialComponent, ComponentInit>(OnComponentInit);

            SubscribeLocalEvent<PhysicalPotentialComponent, CloningEvent>(OnClone);
        }

        private void OnComponentInit(EntityUid uid, PhysicalPotentialComponent comp, ComponentInit args)
        {
            UpdateAlert(uid, comp);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var query = EntityQueryEnumerator<PhysicalPotentialComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                UpdateSprintProgress(frameTime, uid, comp);
                HandleRecovery(uid, comp);
            }
        }

        #region Calculate strains
        // -- HITS --
        private void OnMeleeHit(MeleeHitEvent args)
        {
            if (!TryComp<PhysicalPotentialComponent>(args.User, out var comp))
                return;

            args.BonusDamage += comp.DamageBonus;

            var resolvedDamage = new DamageSpecifier(args.BaseDamage);
            resolvedDamage += args.BonusDamage;
            resolvedDamage = DamageSpecifier.ApplyModifierSets(resolvedDamage, args.ModifiersList);

            var damageStrain = GetDamageStain(comp, resolvedDamage);
            if (damageStrain.Empty)
                return;

            foreach (var hitEntity in args.HitEntities)
            {
                if (!TryComp<MobStateComponent>(hitEntity, out var mob)) continue;
                if (mob.CurrentState != MobState.Alive) continue;

                // Create and queue a new training strain
                var newStrain = new TrainingStrain { Damage = damageStrain };
                AddStrain(comp, newStrain);
            }
        }

        public DamageSpecifier GetDamageStain(PhysicalPotentialComponent comp, DamageSpecifier damage)
        {
            var damageStrain = new DamageSpecifier();
            var totalDamage = FixedPoint2.Zero;

            foreach (var type in PhysicalDamageTypes)
            {
                if (damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                    totalDamage += amount;
            }

            if (totalDamage <= FixedPoint2.Zero)
                return damageStrain;

            foreach (var type in PhysicalDamageTypes)
            {
                if (damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                    damageStrain.DamageDict[type] = amount / totalDamage;
            }

            damageStrain *= comp.DamageRisingSpeed;
            return damageStrain;
        }

        // -- DAMAGE --
        private void OnDamageModify(EntityUid uid, PhysicalPotentialComponent comp, DamageModifyEvent args)
        {
            var trainsDefense = ApplyDefenseReduction(args.Damage, comp.DefenseBonus);
            var isAlive = TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Alive;

            if (args.Origin != null && trainsDefense && isAlive && args.Damage.GetTotal() > 0)
            {
                var newStrain = new TrainingStrain { Defense = comp.DefenseRisingSpeed };
                AddStrain(comp, newStrain);
            }
        }

        private static bool ApplyDefenseReduction(DamageSpecifier damage, FixedPoint2 defenseBonus)
        {
            var totalPhysicalDamage = FixedPoint2.Zero;
            string? lastPositiveType = null;

            foreach (var type in PhysicalDamageTypes)
            {
                if (!damage.DamageDict.TryGetValue(type, out var amount) || amount <= FixedPoint2.Zero)
                    continue;

                totalPhysicalDamage += amount;
                lastPositiveType = type;
            }

            if (totalPhysicalDamage <= FixedPoint2.Zero)
                return false;

            var remainingReduction = FixedPoint2.Min(defenseBonus, totalPhysicalDamage);
            if (remainingReduction <= FixedPoint2.Zero || lastPositiveType == null)
                return true;

            foreach (var type in PhysicalDamageTypes)
            {
                if (!damage.DamageDict.TryGetValue(type, out var amount) || amount <= FixedPoint2.Zero)
                    continue;

                FixedPoint2 reduction;
                if (type == lastPositiveType)
                {
                    reduction = FixedPoint2.Min(amount, remainingReduction);
                }
                else
                {
                    reduction = FixedPoint2.Min(amount, totalPhysicalDamage == FixedPoint2.Zero
                        ? FixedPoint2.Zero
                        : remainingReduction * amount / totalPhysicalDamage);
                }

                damage.DamageDict[type] = amount - reduction;
                remainingReduction -= reduction;
                totalPhysicalDamage -= amount;
            }

            return true;
        }

        // -- PUSH-UP --
        private void OnForcedStandSucceeded(EntityUid uid, PhysicalPotentialComponent comp, ForcedStandSucceededEvent args)
        {
            if (!TryComp<MeleeWeaponComponent>(uid, out var melee)) return;

            // Create and queue a new training strain 
            var newStrain = new TrainingStrain
            {
                Damage = GetDamageStain(comp, melee.Damage),
                Defense = comp.DefenseRisingSpeed,
                Stamina = comp.StaminaRisingSpeed
            };

            float effectivenes = comp.trainingEffectiveness * comp.PushUpsEfficiency;
            AddStrain(comp, newStrain, effectivenes);
        }

        // -- STAMINA AND SPRINT --
        private void UpdateSprintProgress(float frameTime, EntityUid uid, PhysicalPotentialComponent comp)
        {
            if (!TryComp<SprinterComponent>(uid, out var sprinter)
                || !sprinter.IsSprinting
                || !TryComp<InputMoverComponent>(uid, out var mover)
                || !mover.HasDirectionalMovement
                || !TryComp<PhysicsComponent>(uid, out var physics)
                || physics.LinearVelocity.LengthSquared() <= 0.01f)
            {
                comp.SprintTimer = 0;
                return;
            }

            comp.SprintTimer += frameTime;

            // Check if the sprint duration has exceeded the defined interval for a "tick"
            if (comp.SprintTimer > comp.SprintInterval)
            {
                comp.SprintTimer = 0;

                var newStrain = new TrainingStrain { Stamina = comp.StaminaRisingSpeed };
                AddStrain(comp, newStrain);
            }
        }

        #endregion

        #region Strain Handling 
        public void AddStrain(PhysicalPotentialComponent comp, TrainingStrain strain, float? effectivenes = null)
        {
            // Use the provided effectiveness value or fall back to the component's default
            float currentEffectiveness = effectivenes ?? comp.trainingEffectiveness;

            // Calculate the number of guaranteed additions (integer part)
            int fullExecutions = (int) MathF.Floor(currentEffectiveness);

            // Add strains as many times as the integer part allows
            for (int i = 0; i < fullExecutions; i++)
            {
                // Stop adding if the maximum strain limit is reached
                if (comp.Strains.Count >= comp.MaxStrainsNumber) break;
                comp.Strains.Add(strain);
            }

            // If there is still room in the list, process the fractional part
            if (comp.Strains.Count < comp.MaxStrainsNumber)
            {
                // Calculate the remainder (e.g., 0.3 from 1.3)
                float remainder = currentEffectiveness - (float) MathF.Floor(currentEffectiveness);

                // Add a "bonus" strain based on the probability of the remainder
                if (remainder > 0 && _random.Prob(remainder))
                {
                    comp.Strains.Add(strain);
                }
            }

            // Reset the rest timer and set the cooldown period
            comp.EndRestTime = _timing.CurTime + TimeSpan.FromSeconds(comp.TimeForRest);
            comp.IsResting = true;
        }




        private void HandleRecovery(EntityUid uid, PhysicalPotentialComponent comp)
        {
            if (!TryComp<MobStateComponent>(uid, out var mob) || mob.CurrentState != MobState.Alive) return;

            // Check if the rest period after the last activity has ended 
            if (comp.IsResting && comp.EndRestTime < _timing.CurTime)
            {
                comp.IsResting = false;
            }

            // Gradually process the strain queue if the player is resting 
            if (!comp.IsResting && comp.Strains.Count > 0)
            {
                // Introduce a delay between iterations for smooth bonus progression 
                if (comp.NextStrainTime < _timing.CurTime)
                {
                    ApplyStrain(uid, comp);
                    comp.NextStrainTime = _timing.CurTime + TimeSpan.FromSeconds(comp.StrainsApplyingDelay);
                }
            }
        }

        // Apply a specific strain point to the character's stats 
        private void ApplyStrain(EntityUid uid, PhysicalPotentialComponent comp)
        {
            if (comp.Strains.Count == 0) return;

            var strain = comp.Strains[comp.Strains.Count - 1];

            // Update damage bonus
            if (comp.DamageBonus.GetTotal() < comp.MaxDamageBonus)
            {
                comp.DamageBonus += strain.Damage;
            }

            // Update defense bonus
            if (comp.DefenseBonus < comp.MaxDefenseBonus)
            {
                comp.DefenseBonus += strain.Defense;
            }

            // Update stamina bonus
            if (TryComp<StaminaComponent>(uid, out var stamina))
            {
                var staminaIncrease = MathF.Min(strain.Stamina, comp.MaxStamina - stamina.CritThreshold);
                if (staminaIncrease > 0f)
                {
                    stamina.CritThreshold += staminaIncrease;
                    comp.StaminaBonus += staminaIncrease;
                    Dirty(uid, stamina);
                }
            }

            comp.Strains.RemoveAt(comp.Strains.Count - 1);

            UpdateAlert(uid, comp);

            // Mark component as dirty to sync data with the client 
            Dirty(uid, comp);

            // Deduct hunger/calories for training 
            if (TryComp<HungerComponent>(uid, out var hunger))
            {
                _hungerSystem.ModifyHunger(uid, -comp.HungerCost, hunger);
            }
        }
        #endregion

        #region Alert
        private void UpdateAlert(EntityUid uid, PhysicalPotentialComponent comp)
        {
            short stateIndex = (short) Math.Clamp(Math.Round(comp.PowerLevel / 10f * 9f), 0, 9);

            _alertsSystem.ShowAlert(uid, "PhysicalPotential", stateIndex);
        }





        #endregion

        private void OnClone(Entity<PhysicalPotentialComponent> ent, ref CloningEvent args)
        {
            if (!args.Settings.EventComponents.Contains(Factory.GetRegistration(ent.Comp.GetType()).Name))
                return;

            var clone = EnsureComp<PhysicalPotentialComponent>(args.CloneUid);
            clone.trainingEffectiveness = ent.Comp.trainingEffectiveness;
            clone.Strains = new List<TrainingStrain>(ent.Comp.Strains.Count);
            foreach (var strain in ent.Comp.Strains)
            {
                clone.Strains.Add(new TrainingStrain
                {
                    Damage = new DamageSpecifier(strain.Damage),
                    Defense = strain.Defense,
                    Stamina = strain.Stamina
                });
            }

            clone.DamageBonus = new DamageSpecifier(ent.Comp.DamageBonus);
            clone.MaxDamageBonus = ent.Comp.MaxDamageBonus;
            clone.DamageRisingSpeed = ent.Comp.DamageRisingSpeed;
            clone.DefenseRisingSpeed = ent.Comp.DefenseRisingSpeed;
            clone.DefenseBonus = ent.Comp.DefenseBonus;
            clone.MaxDefenseBonus = ent.Comp.MaxDefenseBonus;
            clone.StaminaRisingSpeed = ent.Comp.StaminaRisingSpeed;
            clone.MaxStamina = ent.Comp.MaxStamina;
            clone.StaminaBonus = ent.Comp.StaminaBonus;
            clone.SprintTimer = ent.Comp.SprintTimer;
            clone.SprintInterval = ent.Comp.SprintInterval;
            clone.PushUpsEfficiency = ent.Comp.PushUpsEfficiency;
            clone.TimeForRest = ent.Comp.TimeForRest;
            clone.EndRestTime = ent.Comp.EndRestTime;
            clone.IsResting = ent.Comp.IsResting;
            clone.NextStrainTime = ent.Comp.NextStrainTime;
            clone.MaxStrainsNumber = ent.Comp.MaxStrainsNumber;
            clone.StrainsApplyingDelay = ent.Comp.StrainsApplyingDelay;
            clone.HungerCost = ent.Comp.HungerCost;

            if (TryComp<StaminaComponent>(args.CloneUid, out var stamina))
            {
                stamina.CritThreshold += clone.StaminaBonus;
                Dirty(args.CloneUid, stamina);
            }

            Dirty(args.CloneUid, clone);
        }
    }
}
