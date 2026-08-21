using Content.Server._DV.Psionics.Systems;
using Robust.Shared.Audio;

namespace Content.Server._DV.Psionics.Components;

/// <summary>
/// Marks an altar as capable of sacrificing a buckled psionic to reduce glimmer.
/// The altar must have a <c>StrapComponent</c> so a psionic can be buckled to it.
/// A player with psionics or clerical training can right-click and choose
/// "Sacrifice Psionic" from the context menu.
/// </summary>
[RegisterComponent, Access(typeof(SacrificeAltarSystem))]
public sealed partial class SacrificeAltarComponent : Component
{
    /// <summary>
    /// Minimum glimmer reduced when a psionic is sacrificed.
    /// </summary>
    [DataField]
    public int GlimmerReductionMin = 100;

    /// <summary>
    /// Maximum glimmer reduced when a psionic is sacrificed.
    /// </summary>
    [DataField]
    public int GlimmerReductionMax = 200;

    /// <summary>
    /// Minimum bluespace crystals spawned.
    /// </summary>
    [DataField]
    public int BsCrystalMin = 2;

    /// <summary>
    /// Maximum bluespace crystals spawned.
    /// </summary>
    [DataField]
    public int BsCrystalMax = 6;

    /// <summary>
    /// How long the sacrifice ritual takes (DoAfter duration).
    /// </summary>
    [DataField]
    public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Sound played when the sacrifice completes.
    /// </summary>
    [DataField]
    public SoundSpecifier SacrificeSound = new SoundPathSpecifier("/Audio/Effects/hallelujah.ogg");
}
