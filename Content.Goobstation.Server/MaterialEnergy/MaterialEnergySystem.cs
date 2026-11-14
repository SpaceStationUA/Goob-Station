// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.MaterialEnergy;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;

namespace Content.Goobstation.Server.MaterialEnergy
{
    public sealed partial class MaterialEnergySystem : EntitySystem
    {
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private BatterySystem _batterySystem = default!;
        [Dependency] private StackSystem _stack = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MaterialEnergyComponent, InteractUsingEvent>(OnInteract);
        }

        private void OnInteract(EntityUid uid, MaterialEnergyComponent component, InteractUsingEvent args)
        {
            if (args.Handled || component.MaterialWhiteList == null) // Pirate: consume fuel only once.
                return;

            _entityManager.TryGetComponent<PhysicalCompositionComponent>(args.Used, out var _composition);
            if (_composition == null)
                return;

            _entityManager.TryGetComponent<StackComponent>(args.Used, out var materialStack);
            if (materialStack == null)
                return;

            foreach (var fueltype in component.MaterialWhiteList)
            {
                if (_composition.MaterialComposition.ContainsKey(fueltype) &&
                    AddBatteryCharge(
                        uid,
                        args.Used,
                        _composition.MaterialComposition[fueltype],
                        materialStack.Count))
                {
                    // Pirate: consuming the final fuel piece deletes it, so finish the interaction here.
                    args.Handled = true;
                    return;
                }
            }
        }

        private bool AddBatteryCharge(
            EntityUid cutter,
            EntityUid _material,
            int materialPerSheet,
            int sheetsInStack)
        {
            var chargeDiff = _batterySystem.GetChargeDifference(cutter);
            if (chargeDiff == 0)
                return false;

            var totalMaterial = materialPerSheet * sheetsInStack;

            var chargeToAddFloat = MathF.Min(chargeDiff, totalMaterial);
            var chargeToAdd = (int) MathF.Floor(chargeToAddFloat);

            if (chargeToAdd <= 0)
                return false;

            if (!_batterySystem.TryGetBatteryComponent(cutter, out var batteryComponent, out _))
                return false;

            // Pirate: topping up by less than one piece of ore must still consume that piece.
            var piecesToUse = (int) MathF.Ceiling((float) chargeToAdd / materialPerSheet);
            if (!_stack.TryUse(_material, piecesToUse))
                return false;

            _batterySystem.SetCharge(cutter, batteryComponent.LastCharge + chargeToAdd);
            return true;
        }
    }
}
