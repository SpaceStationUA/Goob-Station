// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Threading.Tasks;
using Content.Client.Parallax.Data;
using Robust.Shared.Maths;

namespace Content.Client.Parallax.Managers;

public interface IParallaxManager
{
    /// <summary>
    /// All WorldHomePosition values are offset by this.
    /// </summary>
    Vector2 ParallaxAnchor { get; set; }

    bool IsLoaded(string name);

    /// <summary>
    /// Starts a crossfade to <paramref name="target"/>, which must already be loaded.
    /// <paramref name="delay"/> keeps the outgoing parallax at full colour for a while first.
    /// </summary>
    bool StartTransition(string current, string target, float duration = 8f, float delay = 0f, string? hyper = null);

    /// <summary>
    /// Like <see cref="StartTransition"/>, but waits for the target to load instead of refusing.
    /// </summary>
    bool RequestTransition(string current, string target, float duration, float delay, string? hyper = null);

    /// <summary>
    /// The parallax the fade passes through, or null for a plain crossfade.
    /// </summary>
    string? GetTransitionHyper();

    /// <summary>
    /// Fade progress, 0..1, or -1 when no fade is drawing.
    /// </summary>
    float GetTransitionProgress();

    string? GetTransitionFrom();

    string? GetTransitionTo();

    /// <summary>
    /// The parallax on screen after a fade has settled, or null if the map's component decides.
    /// </summary>
    string? GetCurrent();

    /// <summary>
    ///     The sky the ship is heading to: the running fade's target, or a queued one still
    ///     loading. Null when neither is live. This is the name the arrival card must use even
    ///     before the fade starts — <see cref="GetCurrent"/> is the sky left behind.
    /// </summary>
    string? GetRequested();

    /// <summary>
    /// Advances travel state, any fade and the warp by one frame. The parallax system calls this
    /// once per frame, independent of drawing: with parallax disabled, or on a map whose overlay
    /// does not draw, a warp must still finish and a fade must settle.
    /// </summary>
    void AdvanceFrame();

    /// <summary>
    /// Reads travel state for drawing, without advancing it: safe to call more than once per
    /// frame, and a crossfade's two parallaxes must share one clock or the outgoing one slides
    /// against the incoming one.
    /// </summary>
    (float scrollClock, float zoomAmount) SampleTravel();

    /// <summary>
    /// Drops everything that belongs to the current round: the settled sky override, any running
    /// or queued fade, the warp in flight and the accumulated travel offsets. Called at round
    /// restart so the next round opens on its own map's parallax instead of the previous round's
    /// last sector.
    /// </summary>
    void ResetRoundState();

    /// <summary>
    /// Advances a screen-relative layer by this frame's travel and returns its world offset.
    /// </summary>
    Vector2 AdvanceScreenScroll(ParallaxLayerConfig config, Vector2 scrolling, Angle rotation);

    /// <summary>
    /// Advances a layer's drift by real time and returns its world offset.
    /// </summary>
    Vector2 AdvanceDrift(ParallaxLayerConfig config, Vector2 drift, Angle rotation);

    /// <summary>
    /// Runs the warp curve: spool, burn, coast, brake. Returns to neutral by itself.
    /// </summary>
    void StartWarp(float duration = 8.5f, float peakSpeed = 3f, float peakZoom = 1.25f);

    bool IsWarping { get; }

    /// <summary>
    /// Travel speed as a fraction of the warp peak, 0..1. Zero at rest.
    /// </summary>
    float WarpSpeedFraction { get; }

    /// <summary>
    /// How far through the warp, 0..1. Zero when not warping.
    /// </summary>
    float WarpProgress { get; }

    /// <summary>
    /// The layers of the selected parallax.
    /// </summary>
    ParallaxLayerPrepared[] GetParallaxLayers(string name);

    /// <summary>
    /// Loads in the default parallax to use.
    /// Do not call until prototype manager is available.
    /// </summary>
    void LoadDefaultParallax();

    Task LoadParallaxByName(string name);

    void UnloadParallax(string name);
}
