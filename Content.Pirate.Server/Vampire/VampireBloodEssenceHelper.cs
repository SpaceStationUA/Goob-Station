using Content.Pirate.Shared.Vampire;
using Content.Pirate.Shared.Vampire.Components;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Pirate.Server.Vampire
{
    /// <summary>
    /// Helper class for managing vampire blood essence operations
    /// </summary>
    public static class VampireBloodEssenceHelper
    {
        /// <summary>
        /// Adds blood essence to a vampire using the VampireAddBloodEssenceEvent
        /// </summary>
        /// <param name="entityManager">Entity manager instance</param>
        /// <param name="vampire">The vampire entity to add blood essence to</param>
        /// <param name="amount">Amount of blood essence to add</param>
        /// <param name="source">Optional source entity that provided the blood essence</param>
        /// <returns>True if the event was raised successfully</returns>
        public static bool AddBloodEssence(IEntityManager entityManager, EntityUid vampire, FixedPoint2 amount, EntityUid? source = null)
        {
            // Check if the entity is actually a vampire
            if (!entityManager.HasComponent<VampireComponent>(vampire))
                return false;

            // Create and raise the blood essence event
            var addBloodEssenceEvent = new VampireAddBloodEssenceEvent(amount, source);
            entityManager.EventBus.RaiseLocalEvent(vampire, addBloodEssenceEvent);

            return true;
        }

        /// <summary>
        /// Convenience method to add blood essence from blood sucking
        /// </summary>
        /// <param name="entityManager">Entity manager instance</param>
        /// <param name="vampire">The vampire entity</param>
        /// <param name="victim">The victim entity</param>
        /// <param name="bloodVolume">Volume of blood sucked as FixedPoint2</param>
        /// <param name="conversionRate">Rate to convert blood volume to essence (default 1)</param>
        /// <returns>True if blood essence was added successfully</returns>
        public static bool AddBloodEssenceFromSucking(IEntityManager entityManager, EntityUid vampire, EntityUid victim, FixedPoint2 bloodVolume, float conversionRate = 1f)
        {
            var essenceAmount = bloodVolume * conversionRate;
            return AddBloodEssence(entityManager, vampire, essenceAmount, victim);
        }
    }
}
