using Content.Pirate.Shared.Traits.Trainability;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Pirate.Server.Traits.Trainability
{
    public sealed partial class TrainabilitySystem
    {
        public void AddTechnicalStrain(
            TrainabilityComponent comp,
            TechnicalStrain strain)
        {
            float efficiency = comp.TechnicalTrainingEfficiency;

            int fullExecutions = (int) MathF.Floor(efficiency);

            for (int i = 0; i < fullExecutions; i++)
            {
                if (comp.TechnicalStrains.Count >= comp.MaxStrainsNumber)
                    break;

                comp.TechnicalStrains.Add(strain);
            }

            if (comp.TechnicalStrains.Count < comp.MaxStrainsNumber)
            {
                float remainder =
                    comp.TechnicalTrainingEfficiency -
                    (float) MathF.Floor(efficiency);

                if (remainder > 0 && _random.NextFloat() < remainder)
                {
                    comp.TechnicalStrains.Add(strain);
                }
            }

            ResetRestingTimer(comp);
        }

        public void AddPhysicalStrain(
            TrainabilityComponent comp,
            float strain)
        {
            if (comp.PhysicalStrains.Count < comp.MaxStrainsNumber)
            {
                comp.PhysicalStrains.Add(
                    strain * comp.PhysicalTrainingEfficiency
                );
            }

            ResetRestingTimer(comp);
        }

        public void ResetRestingTimer(TrainabilityComponent comp)
        {
            comp.EndRestTime =
                _timing.CurTime +
                TimeSpan.FromSeconds(comp.TimeForRest);

            comp.IsResting = true;
        }

        private void HandleRecovery(
            EntityUid uid,
            TrainabilityComponent comp)
        {
            if (!TryComp<MobStateComponent>(uid, out var mob) ||
                mob.CurrentState != MobState.Alive)
            {
                return;
            }

            if (comp.IsResting &&
                comp.EndRestTime < _timing.CurTime)
            {
                comp.IsResting = false;
            }

            if (!comp.IsResting &&
                comp.TechnicalStrains.Count > 0)
            {
                if (comp.NextStrainTime < _timing.CurTime)
                {
                    ApplyTechnicalStrain(uid, comp);

                    comp.NextStrainTime =
                        _timing.CurTime +
                        TimeSpan.FromSeconds(
                            comp.StrainsApplyingDelay
                        );
                }
            }
        }

        private void ApplyTechnicalStrain(
            EntityUid uid,
            TrainabilityComponent comp)
        {
            if (comp.TechnicalStrains.Count == 0)
                return;

            var strain =
                comp.TechnicalStrains[
                    comp.TechnicalStrains.Count - 1
                ];

            if (comp.DamageBonus.GetTotal() < comp.MaxDamageBonus)
            {
                comp.DamageBonus += strain.Damage;
            }

            if (comp.DefenseBonus < comp.MaxDefenseBonus)
            {
                comp.DefenseBonus += strain.Defense;
            }

            if (TryComp<StaminaComponent>(uid, out var stamina) &&
                comp.StaminaBonus < comp.MaxStaminaBonus)
            {
                comp.StaminaBonus += comp.StaminaRisingSpeed;

                stamina.CritThreshold -= comp.CurrentStaminaBonus;

                comp.CurrentStaminaBonus =
                    comp.StaminaBonus * comp.MuscleMass;

                stamina.CritThreshold += comp.CurrentStaminaBonus;

                Dirty(uid, stamina);
            }

            comp.TechnicalStrains.RemoveAt(
                comp.TechnicalStrains.Count - 1
            );

            Dirty(uid, comp);
        }
    }
}
