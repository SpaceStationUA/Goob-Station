using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Traits.Trainability;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Pirate.Server.Traits.Trainability
{
    public sealed partial class TrainabilitySystem
    {
        private static readonly string[] PhysicalDamageTypes =
        {
            "Blunt",
            "Slash",
            "Piercing"
        };

        private void InitializeCombat()
        {
            SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<TrainabilityComponent, DamageModifyEvent>(OnDamageModify);
        }

        private void OnMeleeHit(MeleeHitEvent args)
        {
            if (!TryComp<TrainabilityComponent>(args.User, out var comp))
                return;

            args.BonusDamage += comp.DamageBonus * comp.MuscleMass;

            var resolvedDamage = new DamageSpecifier(args.BaseDamage);
            resolvedDamage += args.BonusDamage;
            resolvedDamage = DamageSpecifier.ApplyModifierSets(
                resolvedDamage,
                args.ModifiersList
            );

            var damageStrain = GetDamageStain(comp, resolvedDamage);

            if (damageStrain.Empty)
                return;

            foreach (var hitEntity in args.HitEntities)
            {
                if (!TryComp<MobStateComponent>(hitEntity, out var mob))
                    continue;

                if (mob.CurrentState != MobState.Alive)
                    continue;

                var newStrain = new TechnicalStrain
                {
                    Damage = damageStrain
                };

                AddTechnicalStrain(comp, newStrain);
            }
        }

        public DamageSpecifier GetDamageStain(
            TrainabilityComponent comp,
            DamageSpecifier damage)
        {
            var damageStrain = new DamageSpecifier();
            var totalDamage = FixedPoint2.Zero;

            foreach (var type in PhysicalDamageTypes)
            {
                if (damage.DamageDict.TryGetValue(type, out var amount)
                    && amount > FixedPoint2.Zero)
                {
                    totalDamage += amount;
                }
            }

            if (totalDamage <= FixedPoint2.Zero)
                return damageStrain;

            foreach (var type in PhysicalDamageTypes)
            {
                if (damage.DamageDict.TryGetValue(type, out var amount)
                    && amount > FixedPoint2.Zero)
                {
                    damageStrain.DamageDict[type] = amount / totalDamage;
                }
            }

            damageStrain *= comp.DamageRisingSpeed;

            return damageStrain;
        }

        private void OnDamageModify(
            EntityUid uid,
            TrainabilityComponent comp,
            DamageModifyEvent args)
        {
            var trainsDefense = ApplyDefenseReduction(
                args.Damage,
                comp.DefenseBonus * comp.MuscleMass
            );

            var isAlive =
                TryComp<MobStateComponent>(uid, out var mob)
                && mob.CurrentState == MobState.Alive;

            if (args.Origin != null
                && trainsDefense
                && isAlive
                && args.Damage.GetTotal() > 0)
            {
                var newStrain = new TechnicalStrain
                {
                    Defense = comp.DefenseRisingSpeed
                };

                AddTechnicalStrain(comp, newStrain);
            }
        }

        private static bool ApplyDefenseReduction(
            DamageSpecifier damage,
            FixedPoint2 defenseBonus)
        {
            var totalPhysicalDamage = FixedPoint2.Zero;
            string? lastPositiveType = null;

            foreach (var type in PhysicalDamageTypes)
            {
                if (!damage.DamageDict.TryGetValue(type, out var amount)
                    || amount <= FixedPoint2.Zero)
                {
                    continue;
                }

                totalPhysicalDamage += amount;
                lastPositiveType = type;
            }

            if (totalPhysicalDamage <= FixedPoint2.Zero)
                return false;

            var remainingReduction =
                FixedPoint2.Min(defenseBonus, totalPhysicalDamage);

            if (remainingReduction <= FixedPoint2.Zero
                || lastPositiveType == null)
            {
                return true;
            }

            foreach (var type in PhysicalDamageTypes)
            {
                if (!damage.DamageDict.TryGetValue(type, out var amount)
                    || amount <= FixedPoint2.Zero)
                {
                    continue;
                }

                FixedPoint2 reduction;

                if (type == lastPositiveType)
                {
                    reduction = FixedPoint2.Min(
                        amount,
                        remainingReduction
                    );
                }
                else
                {
                    reduction = FixedPoint2.Min(
                        amount,
                        totalPhysicalDamage == FixedPoint2.Zero
                            ? FixedPoint2.Zero
                            : remainingReduction * amount / totalPhysicalDamage
                    );
                }

                damage.DamageDict[type] = amount - reduction;

                remainingReduction -= reduction;
                totalPhysicalDamage -= amount;
            }

            return true;
        }
    }
}
