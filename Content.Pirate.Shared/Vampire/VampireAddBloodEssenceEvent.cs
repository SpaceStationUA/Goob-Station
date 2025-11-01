using Content.Goobstation.Maths.FixedPoint;

namespace Content.Pirate.Shared.Vampire
{
    /// <summary>
    /// Event for adding blood essence to a vampire
    /// </summary>
    public sealed partial class VampireAddBloodEssenceEvent : EntityEventArgs
    {
        /// <summary>
        /// Amount of blood essence to add
        /// </summary>
        public FixedPoint2 Amount { get; set; }

        /// <summary>
        /// Source entity that provided the blood essence (optional)
        /// </summary>
        public EntityUid? Source { get; set; }

        public VampireAddBloodEssenceEvent(FixedPoint2 amount, EntityUid? source = null)
        {
            Amount = amount;
            Source = source;
        }
    }
}
