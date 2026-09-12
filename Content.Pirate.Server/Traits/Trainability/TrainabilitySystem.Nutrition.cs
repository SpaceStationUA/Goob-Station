using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Traits.Trainability;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Pirate.Server.Traits.Trainability
{
    public sealed partial class TrainabilitySystem
    {
        private void InitializeNutrition()
        {
            SubscribeLocalEvent<SolutionComponent, SolutionChangedEvent>(
                OnSolutionChanged
            );
        }

        private void OnSolutionChanged(
            Entity<SolutionComponent> ent,
            ref SolutionChangedEvent args)
        {
            var uid = Transform(ent).ParentUid;

            if (!TryComp<TrainabilityComponent>(uid, out var comp))
                return;

            if (comp.PhysicalStrains.Count == 0 ||
                comp.MuscleMass >= comp.MaxMuscleMass)
            {
                return;
            }

            var strain = comp.PhysicalStrains[^1];

            var solution = ent.Comp.Solution;

            var protein =
                solution.GetTotalPrototypeQuantity("Protein");

            if (protein >= comp.ProteinsCost)
            {
                solution.RemoveReagent(
                    "Protein",
                    FixedPoint2.New(comp.ProteinsCost)
                );

                comp.MuscleMass += strain;

                comp.PhysicalStrains.RemoveAt(
                    comp.PhysicalStrains.Count - 1
                );

                if (comp.MuscleMass > comp.MaxMuscleMass)
                    comp.MuscleMass = comp.MaxMuscleMass;
            }

            UpdateAlert(uid, comp);
            Dirty(uid, comp);
        }
    }
}
