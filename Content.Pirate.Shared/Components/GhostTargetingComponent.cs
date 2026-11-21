using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.Containers;
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
        public bool HadGodmode;
        public bool HadFTLSmashImmune;
        public bool HadUniversalLanguageSpeaker;
        public bool HadExaminer;
        public bool HadSpeechDead;
        public string? OldSpeechVerb;
    }
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
    public sealed partial class GhostTargetingComponent : Component
    {
        [ViewVariables]
        public GhostStateSnapshot? SavedState { get; set; }

        // --- Основна інформація ---
        [ViewVariables, AutoNetworkedField]
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
            "ActionJustDecorGhostBlink",
            "ActionJustDecorTakeControlGhost"
        };
        [DataField]
        public List<EntityUid>? ActionEntities;

        [ViewVariables]
        public EntityUid? ToggleGhostFormActionUid { get; set; }

        [ViewVariables]
        public EntityUid? ControlledTarget;

        [ViewVariables]
        public EntityUid? ControlledMind;

        [ViewVariables]
        public EntityUid? OriginalTargetMind;

        [ViewVariables]
        public EntityUid? ControlMindHolder;

        public bool WasVisitingGhost;

        [ViewVariables]
        public TimeSpan ControlTimeEnd;

        [ViewVariables]
        public TimeSpan ControlCooldownEnd;

        [DataField]
        public TimeSpan ControlDuration = TimeSpan.FromSeconds(12);

        [DataField]
        public TimeSpan ControlCooldown = TimeSpan.FromSeconds(60);

        public Container? ControlContainer;

    }
}
