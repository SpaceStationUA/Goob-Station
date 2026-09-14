using Content.Goobstation.Shared.Sprinting;
using Content.Pirate.Shared.Traits.Trainability;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Physics.Components;

namespace Content.Pirate.Server.Traits.Trainability
{
    public sealed partial class TrainabilitySystem
    {
        private void InitializePhysicalTraining()
        {
            SubscribeLocalEvent<TrainabilityComponent, StoodEvent>(OnStood);
            SubscribeLocalEvent<TrainabilityComponent, DownedEvent>(OnDowned);
        }

        private void UpdateSprintProgress(
            float frameTime,
            EntityUid uid,
            TrainabilityComponent comp)
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

            if (comp.SprintTimer > comp.SprintInterval)
            {
                comp.SprintTimer = 0;

                var newStrain = new TechnicalStrain
                {
                    Stamina = comp.StaminaRisingSpeed
                };

                AddTechnicalStrain(comp, newStrain);
            }
        }

        private void OnStood(
            EntityUid uid,
            TrainabilityComponent comp,
            StoodEvent args)
        {
            comp.LastStandTime = _timing.CurTime;
        }

        private void OnDowned(
            EntityUid uid,
            TrainabilityComponent comp,
            DownedEvent args)
        {
            if ((_timing.CurTime - comp.LastStandTime).TotalSeconds
                < comp.PushUpWindow)
            {
                AddPhysicalStrain(comp, comp.PushUpsEfficiency);

                _popup.PopupEntity(
                    Loc.GetString(
                        "system-trainability-push-up",
                        ("gender", (object) GetGender(uid))
                    ),
                    uid,
                    PopupType.Medium
                );
            }
        }
    }
}
