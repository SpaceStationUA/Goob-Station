/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._FarHorizons.Planets;

/// <summary>
/// A planet body seen in space: it renders as a large, slowly spinning sprite in the parallax
/// background (behind grids and entities), and holds the z-map network that IS the planet — the
/// orbit → atmosphere → surface stack a ship descends into when it flies here. One component per
/// planet, tying the thing you see in the sky to the place you land on.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEPlanetComponent : Component
{
    /// <summary>
    /// The z-network entity that forms this planet's descendable z-stack.
    /// Assigned at runtime (when the planet's surface is generated/linked), so it's networked.
    /// Null until the planet has a stack — it still renders in the sky, just isn't landable yet.
    /// The descent sequence reads this to know where an approaching ship ends up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Network;

    /// <summary>The sprite drawn in the parallax background for this planet.</summary>
    [DataField]
    public SpriteSpecifier? Sprite;

    /// <summary>
    /// True when this planet is drawn with the procedural star system shaders (a planet spawned
    /// by the star system) instead of <see cref="Sprite"/>. The client overlay renders the body
    /// through the planet's own shader, and the world-space star system overlay skips it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ShaderMode;

    /// <summary>World-space radius of the body, stamped by the star system at spawn.</summary>
    [DataField, AutoNetworkedField]
    public float WorldRadius;

    /// <summary>
    /// Visual spin rate in radians per second. The background sprite rotates continuously at this
    /// rate; 0 disables the spin.
    /// </summary>
    [DataField]
    public float SpinRate = 0.02f;

    /// <summary>
    /// World-space radius around the planet's coordinate within which it can be approached and is
    /// rendered. Shown on the shuttle console; flying to the centre lets you descend. The render
    /// compresses this whole radius across the view — at the edge the planet is a few pixels near
    /// the screen edge, at the centre it's large and screen-centred.
    /// </summary>
    [DataField]
    public float ApproachRadius = 64f;

    /// <summary>Sprite scale (native px → world units) at the radius edge: small but still visible.</summary>
    [DataField]
    public float MinScale = 0.1f;

    /// <summary>Sprite scale at the radius centre: the planet at full apparent size.</summary>
    [DataField]
    public float MaxScale = 2f;

    /// <summary>
    /// World-space radius at which the sprite reaches <see cref="MinScale"/>. The scale eases
    /// (smoothstep) from <see cref="MaxScale"/> at the zone edge down to <see cref="MinScale"/>
    /// here, on its OWN radius independent of <see cref="ApproachRadius"/> — set it much larger
    /// than the approach radius for a shrink that keeps going long after the planet has slid to
    /// the screen edge, so the size change reads as very gradual. Held at min beyond.
    /// </summary>
    [DataField]
    public float MinScaleRadius = 512f;

    /// <summary>
    /// World-space radius around the planet's coordinate that counts as being AT the planet.
    /// Inside it the planet renders screen-centred at <see cref="MaxScale"/>, and the boundary is
    /// drawn as a ring in the world so you can see the zone while flying. Past it, the planet
    /// gradually eases from the view centre toward the screen edge/corner along its bearing,
    /// arriving at the edge at <see cref="ApproachRadius"/>.
    /// </summary>
    [DataField]
    public float ZoneRadius = 16f;

    /// <summary>Colour of the zone-boundary ring drawn around the planet's coordinate.</summary>
    [DataField]
    public Color ZoneColor = Color.Cyan.WithAlpha(0.35f);

    /// <summary>
    /// Colour of the planet clouds when descending.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color VeilColor = new(0.85f, 0.87f, 0.92f);

    /// <summary>
    /// Radius (in tiles, around 0,0 of the surface levels) within which descending ships
    /// are dropped. The descent picks a random point inside this disc when it warps the
    /// set into the planet's z-stack; 0 means everyone arrives exactly at the origin.
    /// </summary>
    [DataField]
    public float LandingRadius;
}
