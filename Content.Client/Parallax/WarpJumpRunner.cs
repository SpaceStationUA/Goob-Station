// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Parallax.Data;
using Content.Client.Parallax.Managers;
using Robust.Shared.Prototypes;

namespace Content.Client.Parallax;

/// <summary>
///     Runs a whole warp: the curve, and the crossfade through hyperspace into the destination.
/// </summary>
public static class WarpJumpRunner
{
    // The fade runs through the flight. It starts once the burn has the ship up to speed and ends
    // before braking begins, so the new parallax comes through while the ship still moves.
    public const float EntryFraction = 0.15f;
    public const float BrakeFraction = 0.85f;

    public const string Hyperspace = "JourneyHyper";

    private const float PeakSpeed = 120f;

    /// <summary>
    ///     Starts the warp and fades from <paramref name="from"/> to <paramref name="to"/> through hyperspace.
    /// </summary>
    /// <returns>Null on success, otherwise why the warp did not start or the fade was refused.</returns>
    public static string? Run(
        IParallaxManager parallax,
        IPrototypeManager prototypes,
        string? from,
        string to,
        float duration)
    {
        if (parallax.IsWarping)
            return "a warp is already running";

        if (!prototypes.HasIndex<ParallaxPrototype>(to))
            return $"no parallax prototype called '{to}'";

        parallax.StartWarp(duration, PeakSpeed, 1f);

        if (!parallax.IsLoaded(to) || !parallax.IsLoaded(Hyperspace))
        {
            parallax.LoadParallaxByName(to);
            parallax.LoadParallaxByName(Hyperspace);
        }

        if (from == null)
            return "warp is running, but there is no parallax to fade from";

        var delay = duration * EntryFraction;
        var fade = duration * (BrakeFraction - EntryFraction);

        return parallax.RequestTransition(from, to, fade, delay, Hyperspace)
            ? null
            : "warp is running, but the fade was refused";
    }
}
