using System.Linq;
using Content.Server.Atmos.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class BarotraumaSystem
{
    private static bool IsProtectionSlot(BarotraumaComponent component, string slot) =>
        component.ProtectionSlots.Contains(slot) || component.AlternativeProtectionSlots.Any(group => group.Contains(slot));

    private void ApplyAlternativeProtection(EntityUid uid, BarotraumaComponent component,
        ref float highModifier, ref float highMultiplier, ref float lowModifier, ref float lowMultiplier)
    {
        // Starlight treats a neocyte frame and outer clothing as alternative covered torso slots.
        foreach (var group in component.AlternativeProtectionSlots)
        {
            var found = false;
            var groupHighModifier = float.MaxValue;
            var groupHighMultiplier = float.MaxValue;
            var groupLowModifier = float.MinValue;
            var groupLowMultiplier = float.MinValue;
            foreach (var slot in group)
            {
                if (!_inventorySystem.TryGetSlotEntity(uid, slot, out var item) ||
                    !TryGetPressureProtectionValues(item.Value, out var hm, out var ha, out var lm, out var la))
                    continue;
                found = true;
                // Either layer can seal this body part; weaker equipment must not reduce protection.
                groupHighModifier = Math.Min(groupHighModifier, ha.Value);
                groupHighMultiplier = Math.Min(groupHighMultiplier, hm.Value);
                groupLowModifier = Math.Max(groupLowModifier, la.Value);
                groupLowMultiplier = Math.Max(groupLowMultiplier, lm.Value);
            }
            if (!found)
            {
                highModifier = lowModifier = 0f;
                highMultiplier = lowMultiplier = 1f;
                return;
            }
            highModifier = Math.Max(highModifier, groupHighModifier);
            highMultiplier = Math.Max(highMultiplier, groupHighMultiplier);
            lowModifier = Math.Min(lowModifier, groupLowModifier);
            lowMultiplier = Math.Min(lowMultiplier, groupLowMultiplier);
        }
    }
}
