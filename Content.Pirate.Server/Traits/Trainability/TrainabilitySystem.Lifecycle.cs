using Content.Pirate.Shared.Traits.Trainability;
using Content.Shared.Alert;
using Content.Shared.Cloning.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;

namespace Content.Pirate.Server.Traits.Trainability
{
    public sealed partial class TrainabilitySystem
    {
        private void InitializeLifecycle()
        {
            SubscribeLocalEvent<TrainabilityComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<TrainabilityComponent, CloningEvent>(OnClone);
            SubscribeLocalEvent<TrainabilityComponent, ExaminedEvent>(OnExamine);
        }

        private void OnComponentInit(
            EntityUid uid,
            TrainabilityComponent comp,
            ComponentInit args)
        {
            UpdateAlert(uid, comp);
        }

        private void UpdateAlert(
            EntityUid uid,
            TrainabilityComponent comp)
        {
            if (comp.MuscleMass >= 0.025f)
            {
                short stateIndex =
                    (short) (comp.MuscleMass / comp.MaxMuscleMass * 9);

                _alertsSystem.ShowAlert(
                    uid,
                    "Trainability",
                    stateIndex
                );
            }
            else
            {
                _alertsSystem.ClearAlert(
                    uid,
                    "Trainability"
                );
            }
        }

        private void OnClone(
            Entity<TrainabilityComponent> ent,
            ref CloningEvent args)
        {
            if (!args.Settings.EventComponents.Contains(
                    Factory.GetRegistration(ent.Comp.GetType()).Name))
            {
                return;
            }

            var clone =
                EnsureComp<TrainabilityComponent>(args.CloneUid);

            clone.TechnicalTrainingEfficiency =
                ent.Comp.TechnicalTrainingEfficiency;

            clone.TechnicalStrains =
                new List<TechnicalStrain>(
                    ent.Comp.TechnicalStrains.Count
                );

            foreach (var strain in ent.Comp.TechnicalStrains)
            {
                clone.TechnicalStrains.Add(
                    new TechnicalStrain
                    {
                        Damage = new DamageSpecifier(strain.Damage),
                        Defense = strain.Defense,
                        Stamina = strain.Stamina
                    }
                );
            }

            clone.DamageBonus =
                new DamageSpecifier(ent.Comp.DamageBonus);

            clone.MaxDamageBonus =
                ent.Comp.MaxDamageBonus;

            clone.DamageRisingSpeed =
                ent.Comp.DamageRisingSpeed;

            clone.DefenseRisingSpeed =
                ent.Comp.DefenseRisingSpeed;

            clone.DefenseBonus =
                ent.Comp.DefenseBonus;

            clone.MaxDefenseBonus =
                ent.Comp.MaxDefenseBonus;

            clone.StaminaRisingSpeed =
                ent.Comp.StaminaRisingSpeed;

            clone.MaxStaminaBonus =
                ent.Comp.MaxStaminaBonus;

            clone.StaminaBonus =
                ent.Comp.StaminaBonus;

            clone.SprintTimer =
                ent.Comp.SprintTimer;

            clone.SprintInterval =
                ent.Comp.SprintInterval;

            clone.PhysicalTrainingEfficiency =
                ent.Comp.PhysicalTrainingEfficiency;

            clone.PushUpsEfficiency =
                ent.Comp.PushUpsEfficiency;

            clone.PushUpWindow =
                ent.Comp.PushUpWindow;

            clone.MuscleMass =
                ent.Comp.MuscleMass;

            clone.MaxMuscleMass =
                ent.Comp.MaxMuscleMass;

            clone.TimeForRest =
                ent.Comp.TimeForRest;

            clone.EndRestTime =
                ent.Comp.EndRestTime;

            clone.IsResting =
                ent.Comp.IsResting;

            clone.NextStrainTime =
                ent.Comp.NextStrainTime;

            clone.MaxStrainsNumber =
                ent.Comp.MaxStrainsNumber;

            clone.StrainsApplyingDelay =
                ent.Comp.StrainsApplyingDelay;

            clone.ProteinsCost =
                ent.Comp.ProteinsCost;

            if (TryComp<StaminaComponent>(
                    args.CloneUid,
                    out var stamina))
            {
                clone.CurrentStaminaBonus =
                    clone.StaminaBonus * clone.MuscleMass;

                stamina.CritThreshold +=
                    clone.CurrentStaminaBonus;

                Dirty(args.CloneUid, stamina);
            }

            Dirty(args.CloneUid, clone);
        }

        private void OnExamine(
            EntityUid uid,
            TrainabilityComponent comp,
            ExaminedEvent args)
        {
            if (comp.MuscleMass < 0.3f)
                return;

            string key = comp.MuscleMass switch
            {
                >= 0.8f => "system-trainability-examine-level3",
                >= 0.5f => "system-trainability-examine-level2",
                _ => "system-trainability-examine-level1",
            };

            args.PushMarkup(
                Loc.GetString(
                    key,
                    ("gender", (object) GetGender(uid))
                )
            );
        }

        private Gender GetGender(EntityUid uid)
        {
            var entityGender = Gender.Neuter;

            if (TryComp<HumanoidAppearanceComponent>(
                    uid,
                    out var humanoid))
            {
                entityGender = humanoid.Gender;
            }
            else if (TryComp<GrammarComponent>(
                         uid,
                         out var grammar))
            {
                entityGender =
                    grammar.Gender ?? Gender.Neuter;
            }

            return entityGender;
        }
    }
}
