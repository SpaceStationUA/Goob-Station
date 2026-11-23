// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Klovnstation14.

using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Decals;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Lawnmower;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedKsLawnmowerBladesSystem))]
public sealed partial class KsLawnmowerBladesComponent : Component
{
    /// <summary>
    ///     Grass floor types that the blades replace with mowed turf.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<ContentTileDefinition>> MowableTiles = new();

    [DataField(required: true)]
    public SoundSpecifier DriveSound = default!;

    [DataField(required: true)]
    public SoundSpecifier BumpSound = default!;

    [DataField(required: true)]
    public SoundSpecifier BladeSound = default!;

    [DataField(required: true)]
    public SoundSpecifier CrushSound = default!;

    [DataField(required: true)]
    public DamageSpecifier BumpDamage = new();

    /// <summary>
    ///     The most damage a crush deals. It is cut short of gibbing, and of going far past death.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 CrushDamage;

    [DataField]
    public ProtoId<DamageTypePrototype> CrushDamageType = "Blunt";

    /// <summary>
    ///     How far past its death threshold a crush may take its victim.
    /// </summary>
    [DataField]
    public FixedPoint2 CrushLethalOvershoot = 10;

    [DataField]
    public TimeSpan CrushKnockdownTime = TimeSpan.FromSeconds(10);

    [DataField]
    public ProtoId<EmotePrototype> CrushEmote = "Scream";

    [DataField]
    public LocId CrushPopup = "ks-lawn-mower-crush";

    [DataField]
    public LocId GibPopup = "ks-lawn-mower-gib";

    /// <summary>
    ///     The fixture that touches what the blades hit.
    /// </summary>
    [DataField]
    public string BladeFixture = "blades";

    /// <summary>
    ///     The tile that mowing a <see cref="MowableTiles"/> tile leaves.
    /// </summary>
    [DataField]
    public ProtoId<ContentTileDefinition> MowedTile = "FloorMowedAstroGrass";

    /// <summary>
    ///     Decals drawn from this sprite are cut away with the grass.
    /// </summary>
    [DataField]
    public ResPath MowableDecalSprite = new("/Textures/Decals/Flora/flora_grass.rsi");

    [DataField]
    public ProtoId<DecalPrototype> TracksDecal = "KsLawnmowerBloodTracks";

    [DataField]
    public Color TracksColor = Color.FromHex("#8f1818");

    /// <summary>
    ///     How many tiles the tracks last after the blades draw blood, fading over them.
    /// </summary>
    [DataField]
    public int TracksTiles = 4;

    /// <summary>
    ///     Blood the blades spill from each victim.
    /// </summary>
    [DataField]
    public FixedPoint2 BleedAmount = 4;

    [DataField]
    public TimeSpan DriveSoundInterval = TimeSpan.FromSeconds(0.8);

    /// <summary>
    ///     The speed, in metres per second, from which the mower counts as driving: it mows and sounds.
    /// </summary>
    [DataField]
    public float MinDrivingSpeed = 0.2f;

    /// <summary>
    ///     How far a standing target is shoved, in metres.
    /// </summary>
    [DataField]
    public float BumpThrowDistance = 4f;

    [DataField]
    public float BumpThrowSpeed = 6f;

    /// <summary>
    ///     Camera kick for the rider, against the way they drive, when the blades catch something.
    /// </summary>
    [DataField]
    public float RiderCameraKick = 0.15f;

    /// <summary>
    ///     Camera kick for a gibbed victim, along the way the mower drives.
    /// </summary>
    [DataField]
    public float VictimCameraKick = 0.3f;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextDriveSound;

    /// <summary>
    ///     Targets the blades already hit and still touch. Leaving the blades re-arms a hit.
    /// </summary>
    /// <remarks>
    ///     Net entities, as a gibbed target is deleted while still in here and can't be networked as a uid.
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public HashSet<NetEntity> HitTargets = new();

    // Only the server mows and leaves tracks.
    public (EntityUid Grid, Vector2i Indices)? LastTile;
    public int BloodyTiles;
}
