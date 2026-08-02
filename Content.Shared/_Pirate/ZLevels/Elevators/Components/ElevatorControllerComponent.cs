using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ZLevels.Elevators.Components;

/// <summary>
/// The brain of an elevator: anchored at the lower-left of the cab footprint on the cab's start deck.
/// It stays put — the cab floor tiles and riders move. Other parts (panel, doors, call buttons,
/// indicators) link to it by matching <see cref="ElevatorId"/>.
///
/// Mapping contract: the start deck holds the cab floor tiles; every other served deck has an empty or
/// shaft-floor footprint. Served decks are discovered by walking the z-network while the footprint stays open.
/// </summary>
[RegisterComponent]
public sealed partial class ElevatorControllerComponent : Component
{
    /// <summary>Shared id linking this controller to its panels, doors, call buttons and indicators.</summary>
    [DataField(required: true)]
    public string ElevatorId = string.Empty;

    /// <summary>Footprint width in tiles (extends +X from the controller tile).</summary>
    [DataField]
    public int Width = 1;

    /// <summary>Footprint height in tiles (extends +Y from the controller tile).</summary>
    [DataField]
    public int Height = 1;

    /// <summary>Optional override tile for every cab footprint tile. If null, home-deck tiles are captured.</summary>
    [DataField]
    public string? CabFloorTile;

    /// <summary>Tile left on the basement when the cab is gone. A real floor (gravity + atmosphere are tile-based).</summary>
    [DataField]
    public string ShaftFloorTile = "FloorElevatorShaft";

    /// <summary>Seconds spent travelling per deck (one z-step).</summary>
    [DataField]
    public float PerDeckTravelSeconds = 1.0f;

    /// <summary>If true, mobs crushed by the arriving cab are gibbed; otherwise they take heavy damage.</summary>
    [DataField]
    public bool ViolentLanding;

    /// <summary>If true, descending shows a travel-warning telegraph on the destination footprint.</summary>
    [DataField]
    public bool WarnsOnDownMovement = true;

    /// <summary>The looping elevator-music speaker that rides with the cab.</summary>
    [DataField]
    public EntProtoId MusicSpeakerProto = "ElevatorMusicSpeaker";

    /// <summary>Telegraph spawned on each destination tile while the cab descends onto it.</summary>
    [DataField]
    public EntProtoId TravelWarningProto = "ElevatorTravelWarning";

    /// <summary>Runtime: the single looping speaker, carried with the cab.</summary>
    [ViewVariables]
    public EntityUid MusicSpeaker = EntityUid.Invalid;

    /// <summary>Damage applied to anything crushed in the destination shaft (when not gibbing).</summary>
    [DataField]
    public DamageSpecifier CrushDamage = new();

    /// <summary>Sound played per deck-step while travelling. ~6 dB quieter than default (≈ half volume).</summary>
    [DataField]
    public SoundSpecifier? TravelSound =
        new SoundPathSpecifier("/Audio/Mecha/sound_mecha_hydraulic.ogg")
        {
            Params = AudioParams.Default.WithVolume(-6f),
        };

    /// <summary>Sound played when the cab arrives and doors open.</summary>
    [DataField]
    public SoundSpecifier? ArriveSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    // Runtime state.

    /// <summary>Grid the controller is anchored to (the cab's start deck). Cached at init.</summary>
    [ViewVariables]
    public EntityUid AnchorGrid = EntityUid.Invalid;

    /// <summary>Lower-left footprint tile (local to <see cref="AnchorGrid"/>). Same local indices on every deck.</summary>
    [ViewVariables]
    public Vector2i OriginTile;

    /// <summary>Cab floor tiles captured at init, keyed by footprint offset from <see cref="OriginTile"/>.</summary>
    [ViewVariables]
    public Dictionary<Vector2i, Tile> ResolvedCabFloorTiles = new();

    /// <summary>Shaft floor tile resolved at init (from <see cref="ShaftFloorTile"/>).</summary>
    [ViewVariables]
    public Tile ResolvedShaftFloorTile;

    /// <summary>Depth (z-network) the cab is currently on.</summary>
    [ViewVariables]
    public int CurrentDepth;

    /// <summary>Depth the cab is travelling toward (equals <see cref="CurrentDepth"/> when idle).</summary>
    [ViewVariables]
    public int TargetDepth;

    /// <summary>True while the cab is in transit; controls are locked.</summary>
    [ViewVariables]
    public bool Moving;

    /// <summary>When the next single-deck step should occur (while <see cref="Moving"/>).</summary>
    [ViewVariables]
    public TimeSpan NextStepTime;

    /// <summary>Served deck depths, ascending. Discovered at init.</summary>
    [ViewVariables]
    public List<int> ServedDepths = new();

    /// <summary>True once the controller has resolved its footprint/served decks.</summary>
    [ViewVariables]
    public bool Initialized;

    /// <summary>Optional per-depth display names ("2" -&gt; "Cargo"). Falls back to "Floor N".</summary>
    [DataField]
    public Dictionary<int, string> FloorNames = new();
}
