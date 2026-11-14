using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Vampirism.Events
{
    [Serializable, NetSerializable]
    public sealed partial class BloodSuckDoAfterEvent : SimpleDoAfterEvent
    {
        /// <summary>
        /// Soft-check result collected on the first sip. Repeats reuse it instead of
        /// evaluating the blood again, so the taste is shown exactly once per bite session.
        /// </summary>
        [DataField]
        public BloodTasteResult Result = new();

        /// <summary>
        /// Whether the first sip of this session already happened. Until then no taste exists:
        /// soft checks only run once the do-after has completed.
        /// </summary>
        [DataField]
        public bool Tasted;

        /// <summary>
        /// Whether the first successful sip of this session was already written to the admin log.
        /// </summary>
        [DataField]
        public bool Logged;
    }
}
