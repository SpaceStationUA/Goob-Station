using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System.Collections.Generic;

namespace Content.Pirate.Shared.Components
{
    [DataDefinition]
    public sealed partial class GhostStateSnapshot
    {
        public int? VisibilityMask;
        public bool? DrawFov;
        public bool? CanCollide;
        public Robust.Shared.Physics.BodyType? BodyType;
        public int? VisibilityLayers;
        public List<int>? FixtureLayers;
        public bool HadPhysics;
        public bool HadContentEye;
        public bool HadMovementIgnoreGravity;
        public bool HadCanMoveInAir;
        public bool HadZombieImmune;
        public bool HadBreathingImmunity;
        public bool HadPressureImmunity;
        public bool HadActiveListener;
        public bool HadWeakToHoly;
        public bool HadCrematoriumImmune;
        public bool HadFTLSmashImmune;
        public bool HadUniversalLanguageSpeaker;
        public bool HadExaminer;
        public bool HadSpeechDead;
        public string? OldSpeechVerb;
    }
    [RegisterComponent]
    public sealed partial class GhostTargetingComponent : Component
    {
        [ViewVariables]
        public GhostStateSnapshot? SavedState { get; set; }

        // --- Основна інформація ---
        [ViewVariables]
        public bool IsGhost { get; set; } = false;
        [ViewVariables]
        public NetEntity Target { get; set; } = NetEntity.Invalid;

        [ViewVariables]
        public List<string> GhostLayers { get; set; } = new();

        [DataField]
        public List<string> BaseGhostActions { get; set; } = new()
        {
            "ActionJustDecorTargetGhost",
            "ActionJustDecorClearTargetGhost",
            "ActionJustDecorToggleGhostForm",
            "ActionJustDecorGhostBlink"
        };
        [DataField]
        public List<EntityUid>? ActionEntities;

        [ViewVariables]
        public EntityUid? ToggleGhostFormActionUid { get; set; }

    }
}
