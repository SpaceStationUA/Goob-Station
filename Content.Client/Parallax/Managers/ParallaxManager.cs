// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Client.Parallax.Data;
using Content.Shared.CCVar;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.Parallax.Managers;

public sealed class ParallaxManager : IParallaxManager
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IDependencyCollection _deps = null!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private ISawmill _sawmill = Logger.GetSawmill("parallax");

    public Vector2 ParallaxAnchor { get; set; }

    private readonly Dictionary<string, ParallaxLayerPrepared[]> _parallaxesLQ = new();
    private readonly Dictionary<string, ParallaxLayerPrepared[]> _parallaxesHQ = new();

    private readonly Dictionary<string, CancellationTokenSource> _loadingParallaxes = new();

    // The fade in progress. Both ends are resident before it starts: loading is async, and a
    // target that arrives mid-fade would show as a black frame.
    private string? _transitionFrom;
    private string? _transitionTo;
    private string? _transitionHyper;
    private double _transitionStart;
    private float _transitionDuration = 8f;
    private float _transitionDelay;

    private string? _pendingFrom;
    private string? _pendingTo;
    private string? _pendingHyper;
    private float _pendingDuration;
    private float _pendingDelay;

    // The parallax on screen once a fade has settled. The map's ParallaxComponent is networked and
    // server-owned, so the client cannot rewrite it; without this the map's parallax would come back.
    private string? _current;

    public bool IsLoaded(string name) => _parallaxesLQ.ContainsKey(name);

    public string? GetCurrent() => _current;

    // The scroll clock integrates speed over time, so changing speed mid-warp does not move layers
    // that have already travelled. It starts at zero, so a parallax at rest keeps still.
    private double _scrollClock;
    private double _lastTick;
    private bool _travelSeeded;
    private float _speed = 1f;
    private float _zoom = 1f;
    private float _lastStep;
    private float _lastDt;

    // A breakpoint or an alt-tab hands over seconds of dt in one frame. Integrating that would
    // jump the background across half the screen.
    private const float MaxStep = 0.1f;

    private readonly Dictionary<ParallaxLayerConfig, Vector2> _drift = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ParallaxLayerConfig, Vector2> _screenScroll = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Advances travel state, any fade and the warp by one frame. The system calls this once per
    /// frame, independent of drawing: with parallax disabled, or on a map whose overlay does not
    /// draw, a warp must still finish and a fade must settle.
    /// </summary>
    public void AdvanceFrame()
    {
        var now = _timing.RealTime.TotalSeconds;

        if (!_travelSeeded)
        {
            _travelSeeded = true;
            _lastTick = now;
        }

        var dt = (float) Math.Clamp(now - _lastTick, 0.0, MaxStep);
        _lastTick = now;
        _lastDt = dt;

        SettleTransition();
        PollPending();

        if (_warping)
            DriveWarp(now);

        _lastStep = _speed * dt;
        _scrollClock += _lastStep;
    }

    /// <summary>
    ///     Advances a layer's drift by real time and returns its world offset.
    /// </summary>
    public Vector2 AdvanceDrift(ParallaxLayerConfig config, Vector2 drift, Angle rotation)
    {
        var offset = _drift.GetValueOrDefault(config);
        offset += Vector2.Transform(drift, Matrix3x2.CreateRotation((float) (double) rotation)) * _lastDt;
        _drift[config] = offset;
        return offset;
    }

    /// <summary>
    ///     Advances a screen-relative layer by this frame's travel and returns its world offset.
    /// </summary>
    /// <remarks>
    ///     The step is rotated by the camera at the moment it is taken. Turning the view then changes
    ///     the direction of future motion only, rather than re-rotating everything already travelled.
    /// </remarks>
    public Vector2 AdvanceScreenScroll(ParallaxLayerConfig config, Vector2 scrolling, Angle rotation)
    {
        var offset = _screenScroll.GetValueOrDefault(config);
        offset += Vector2.Transform(scrolling, Matrix3x2.CreateRotation((float) (double) rotation)) * _lastStep;
        _screenScroll[config] = offset;
        return offset;
    }

    /// <summary>
    /// Reads travel state for drawing, without advancing it: safe to call more than once per
    /// frame, so two viewports cannot step the same clock twice.
    /// </summary>
    public (float scrollClock, float zoomAmount) SampleTravel()
    {
        return ((float) _scrollClock, _zoom);
    }

    private bool _warping;
    private float _warpProgress;
    private double _warpStart;
    private float _warpDuration = 8.5f;
    private float _warpPeakSpeed = 3f;
    private float _warpPeakZoom = 1.25f;

    // Fractions of the warp: spool up, burn, coast, brake.
    private const float SpoolEnd = 0.15f;
    private const float BurnEnd = 0.32f;
    private const float CoastEnd = 0.76f;

    private static float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public void StartWarp(float duration = 8.5f, float peakSpeed = 3f, float peakZoom = 1.25f)
    {
        _warpStart = _timing.RealTime.TotalSeconds;
        _warpDuration = MathF.Max(duration, 0.5f);
        _warpPeakSpeed = MathF.Max(peakSpeed, 0f);
        _warpPeakZoom = MathF.Max(peakZoom, 1f);
        _warping = true;
    }

    public bool IsWarping => _warping;

    public float WarpSpeedFraction =>
        _warping && _warpPeakSpeed > 0f ? Math.Clamp(_speed / _warpPeakSpeed, 0f, 1f) : 0f;

    public float WarpProgress => _warping ? _warpProgress : 0f;

    private void DriveWarp(double now)
    {
        var t = (float) ((now - _warpStart) / _warpDuration);

        if (t >= 1f)
        {
            _warping = false;
            _warpProgress = 0f;
            _speed = 1f;
            _zoom = 1f;
            return;
        }

        _warpProgress = t;

        // Spooling keeps a trickle of motion, so the jump does not start from a dead stop.
        float speed;
        if (t < SpoolEnd)
            speed = 0.3f * SmoothStep(t / SpoolEnd);
        else if (t < BurnEnd)
            speed = 0.3f + 0.7f * SmoothStep((t - SpoolEnd) / (BurnEnd - SpoolEnd));
        else if (t < CoastEnd)
            speed = 1f;
        else
            speed = 1f - SmoothStep((t - CoastEnd) / (1f - CoastEnd));

        // Zoom comes in after the burn starts and lets go before the ship stops.
        float zoom;
        if (t < BurnEnd)
            zoom = 1f;
        else if (t < CoastEnd)
            zoom = 1f + (_warpPeakZoom - 1f) * SmoothStep((t - BurnEnd) / (CoastEnd * 0.35f));
        else if (t < CoastEnd + 0.12f)
            zoom = _warpPeakZoom;
        else
            zoom = _warpPeakZoom + (1f - _warpPeakZoom) *
                SmoothStep((t - CoastEnd - 0.12f) / (1f - CoastEnd - 0.12f));

        _speed = _warpPeakSpeed * speed;
        _zoom = zoom;
    }

    /// <summary>
    ///     Starts a fade, or waits for the target to load and then starts it. Returns whether the
    ///     request was accepted, not whether it has begun. A newer request replaces a pending one.
    /// </summary>
    public bool RequestTransition(string current, string target, float duration, float delay, string? hyper = null)
    {
        if (_transitionTo != null || current == target)
            return false;

        if (IsLoaded(target) && (hyper == null || IsLoaded(hyper)))
            return StartTransition(current, target, duration, delay, hyper);

        LoadParallaxByName(target);
        if (hyper != null)
            LoadParallaxByName(hyper);

        _pendingFrom = current;
        _pendingTo = target;
        _pendingHyper = hyper;
        _pendingDuration = duration;
        _pendingDelay = delay;
        _sawmill.Info($"Transition to {target} queued, waiting for it to load");
        return true;
    }

    private void PollPending()
    {
        if (_pendingTo == null || _transitionTo != null)
            return;

        if (!IsLoaded(_pendingTo) || (_pendingHyper != null && !IsLoaded(_pendingHyper)))
            return;

        var from = _pendingFrom!;
        var to = _pendingTo;
        var hyper = _pendingHyper;
        _pendingFrom = null;
        _pendingTo = null;
        _pendingHyper = null;

        StartTransition(from, to, _pendingDuration, _pendingDelay, hyper);
    }

    /// <summary>
    ///     Begins a crossfade from the current parallax to <paramref name="target"/>. Does nothing
    ///     if a fade is already running.
    /// </summary>
    /// <remarks>
    ///     The target must be loaded first. Fading into a parallax that has not arrived shows as a
    ///     black frame partway through the movement.
    /// </remarks>
    public bool StartTransition(string current, string target, float duration = 8f, float delay = 0f, string? hyper = null)
    {
        if (_transitionTo != null)
            return false;

        if (current == target || !IsLoaded(target))
            return false;

        if (hyper != null && !IsLoaded(hyper))
            return false;

        _transitionFrom = current;
        _transitionTo = target;
        _transitionHyper = hyper;
        _transitionDuration = MathF.Max(duration, 0.1f);
        _transitionDelay = MathF.Max(delay, 0f);
        _transitionStart = _timing.RealTime.TotalSeconds;

        _sawmill.Info($"Parallax transition {current} -> {target} over {_transitionDuration}s" +
                      (_transitionDelay > 0f ? $" starting in {_transitionDelay}s" : ""));
        return true;
    }

    /// <summary>
    ///     Fade position, 0..1, or -1 when no fade is drawing. Before the delay runs out the
    ///     outgoing parallax stays on screen at full colour, which is also -1.
    /// </summary>
    public float GetTransitionProgress()
    {
        if (_transitionTo == null)
            return -1f;

        var t = TransitionPosition();
        return t < 0f || t >= 1f ? -1f : t;
    }

    private float TransitionPosition()
    {
        var elapsed = _timing.RealTime.TotalSeconds - _transitionStart - _transitionDelay;
        return (float) (elapsed / _transitionDuration);
    }

    private void SettleTransition()
    {
        if (_transitionTo == null || TransitionPosition() < 1f)
            return;

        _current = _transitionTo;
        _transitionFrom = null;
        _transitionTo = null;
        _transitionHyper = null;
    }

    /// <summary>
    /// Drops everything that belongs to the current round: the settled sky override, any running
    /// or queued fade, the warp in flight and the accumulated travel offsets. The settled sky is
    /// client-only state the server cannot clear, so without this the next round would open on
    /// the previous round's last sector instead of its own map's parallax.
    /// </summary>
    public void ResetRoundState()
    {
        _current = null;
        _transitionFrom = null;
        _transitionTo = null;
        _transitionHyper = null;
        _pendingFrom = null;
        _pendingTo = null;
        _pendingHyper = null;
        _warping = false;
        _warpProgress = 0f;
        _speed = 1f;
        _zoom = 1f;
        _scrollClock = 0;
        _lastStep = 0f;
        _lastDt = 0f;
        _travelSeeded = false;
        _drift.Clear();
        _screenScroll.Clear();
    }

    /// <summary>
    ///     The parallax being faded out, or null if none.
    /// </summary>
    public string? GetTransitionFrom() => _transitionFrom;

    /// <summary>
    ///     The parallax being faded in, or null if none.
    /// </summary>
    public string? GetTransitionTo() => _transitionTo;

    public string? GetTransitionHyper() => _transitionHyper;

    public ParallaxLayerPrepared[] GetParallaxLayers(string name)
    {
        if (_configurationManager.GetCVar(CCVars.ParallaxLowQuality))
        {
            return !_parallaxesLQ.TryGetValue(name, out var lq) ? Array.Empty<ParallaxLayerPrepared>() : lq;
        }

        return !_parallaxesHQ.TryGetValue(name, out var hq) ? Array.Empty<ParallaxLayerPrepared>() : hq;
    }

    public void UnloadParallax(string name)
    {
        if (_loadingParallaxes.TryGetValue(name, out var loading))
        {
            _sawmill.Debug($"Cancelling loading parallax {name}");
            loading.Cancel();
            _loadingParallaxes.Remove(name, out _);
            return;
        }

        _sawmill.Debug($"Unloading parallax {name}");

        if (_parallaxesLQ.Remove(name, out var layers))
        {
            foreach (var layer in layers)
            {
                layer.Config.Texture.Unload(_deps);
            }
        }

        if (_parallaxesHQ.Remove(name, out layers))
        {
            foreach (var layer in layers)
            {
                layer.Config.Texture.Unload(_deps);
            }
        }
    }

    public async void LoadDefaultParallax()
    {
        _sawmill.Level = LogLevel.Info;
        await LoadParallaxByName("Default");
    }

    public async Task LoadParallaxByName(string name)
    {
        if (_parallaxesLQ.ContainsKey(name) || _loadingParallaxes.ContainsKey(name)) return;

        var token = new CancellationTokenSource();
        _loadingParallaxes[name] = token;
        var cancel = token.Token;

        _sawmill.Debug($"Loading parallax {name}");

        // The layers loaded so far, so they can be released if the load is cancelled.
        var loadedLayers = new List<ParallaxLayerPrepared>();

        try
        {
            var parallaxPrototype = _prototypeManager.Index<ParallaxPrototype>(name);

            ParallaxLayerPrepared[][] layers;

            if (parallaxPrototype.LayersLQUseHQ)
            {
                layers = new ParallaxLayerPrepared[2][];
                layers[0] = layers[1] = await LoadParallaxLayers(parallaxPrototype.Layers, loadedLayers, cancel);
            }
            else
            {
                // Explicitly allocate params array to avoid sandbox violation since C# 14.
                var tasks = new[]
                {
                    LoadParallaxLayers(parallaxPrototype.Layers, loadedLayers, cancel),
                    LoadParallaxLayers(parallaxPrototype.LayersLQ, loadedLayers, cancel),
                };
                layers = await Task.WhenAll(tasks);
            }

            cancel.ThrowIfCancellationRequested();

            _loadingParallaxes.Remove(name);

            _parallaxesLQ[name] = layers[1];
            _parallaxesHQ[name] = layers[0];

            _sawmill.Verbose($"Loading parallax {name} completed");
        }
        catch (OperationCanceledException)
        {
            _sawmill.Verbose($"Loading parallax {name} cancelled");

            foreach (var loadedLayer in loadedLayers)
            {
                loadedLayer.Config.Texture.Unload(_deps);
            }
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to load parallax {name}: {ex}");

            // Without this the failed name stays in the loading set and can never be retried.
            _loadingParallaxes.Remove(name);
        }
    }

    private async Task<ParallaxLayerPrepared[]> LoadParallaxLayers(
        List<ParallaxLayerConfig> layersIn,
        List<ParallaxLayerPrepared> loadedLayers,
        CancellationToken cancel = default)
    {
        // Because this is async, make sure it doesn't change (prototype reloads could muck this up)
        // Since the tasks aren't awaited until the end, this should be fine
        var tasks = new Task<ParallaxLayerPrepared>[layersIn.Count];
        for (var i = 0; i < layersIn.Count; i++)
        {
            tasks[i] = LoadParallaxLayer(layersIn[i], loadedLayers, cancel);
        }
        return await Task.WhenAll(tasks);
    }

    private async Task<ParallaxLayerPrepared> LoadParallaxLayer(
        ParallaxLayerConfig config,
        List<ParallaxLayerPrepared> loadedLayers,
        CancellationToken cancel = default)
    {
        var prepared = new ParallaxLayerPrepared()
        {
            Texture = await config.Texture.GenerateTexture(cancel),
            Config = config
        };

        loadedLayers.Add(prepared);

        return prepared;
    }
}
