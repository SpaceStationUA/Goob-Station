// SPDX-License-Identifier: MIT

using Content.Client.Parallax.Data;
using Content.Client.Parallax.Managers;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.IoC;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Parallax;

/// <summary>
///     Runs the warp's screen effects and decides when they fire: the shockwaves at entry and exit,
///     the vignette that follows the speed, the transit hum and the arrival card.
/// </summary>
public sealed class WarpEffectsSystem : EntitySystem
{
    [Dependency] private readonly IParallaxManager _parallax = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const float ShockDuration = 0.7f;

    // The exit shock is a touch stronger, because by then the ship is arriving rather than leaving.
    private const float EntryStrength = 5f;
    private const float ExitStrength = 6f;

    // Fraction of the warp at which the exit shock fires, as the new parallax starts to come through.
    private const float ExitAt = 0.62f;

    // Fraction of the warp at which the arrival card slides in, once the new parallax dominates.
    private const float ArrivalAt = 0.86f;
    private const float ArrivalDuration = 7f;
    private const string ArrivalFallbackTitle = "warp-arrival-unknown-sector";

    private const float MaxVignette = 0.8f;

    private static readonly SoundPathSpecifier EntrySound = new("/Audio/_Pirate/Effects/Shuttle/FTL_Jump.ogg");
    private static readonly SoundPathSpecifier ExitSound = new("/Audio/_Pirate/Effects/Shuttle/FTL_stop.ogg");
    private static readonly AudioParams WarpAudioParams = AudioParams.Default.WithVolume(-5f);

    // The transit hum is heard across the station for the length of the warp.
    private static readonly SoundPathSpecifier TransitHumSound = new("/Audio/_Pirate/Effects/Shuttle/FTL_drive_hum.ogg");
    private static readonly AudioParams TransitHumParams = AudioParams.Default.WithVolume(-10f).WithLoop(true);

    private WarpArrivalOverlay _arrival = default!;

    private double _shockStart = double.MinValue;
    private float _shockDuration = ShockDuration;
    private float _shockStrength;

    private bool _wasWarping;
    private bool _exitFired;
    private bool _arrivalFired;

    // Captured when the warp starts. The fade clears its live target before the card fires.
    private string? _arrivalTarget;
    private EntityUid? _humLoop;

    public float VignetteStrength { get; private set; }

    public bool ShockActive =>
        (float) (_timing.CurTime.TotalSeconds - _shockStart) < _shockDuration;

    public float ShockProgress =>
        Math.Clamp((float) (_timing.CurTime.TotalSeconds - _shockStart) / _shockDuration, 0f, 1f);

    public float ShockStrength => _shockStrength;

    public override void Initialize()
    {
        base.Initialize();

        _arrival = new WarpArrivalOverlay();
        _overlay.AddOverlay(new WarpShockwaveOverlay(this));
        _overlay.AddOverlay(new WarpVignetteOverlay(this));
        _overlay.AddOverlay(_arrival);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        StopTransitHum();
        _overlay.RemoveOverlay<WarpShockwaveOverlay>();
        _overlay.RemoveOverlay<WarpVignetteOverlay>();
        _overlay.RemoveOverlay(_arrival);
    }

    /// <summary>
    ///     Plays the entry flash. The caller that starts the warp plays it, not <see cref="Update"/>,
    ///     so that a staged start does not sound twice.
    /// </summary>
    public void PlayEntrySound()
    {
        _audio.PlayGlobal(EntrySound, Filter.Local(), false, WarpAudioParams);
    }

    private void TriggerShockwave(float strength)
    {
        _shockStart = _timing.CurTime.TotalSeconds;
        _shockDuration = ShockDuration;
        _shockStrength = strength;
    }

    private void ShowArrivalCard()
    {
        var target = _arrivalTarget ?? _parallax.GetTransitionTo() ?? _parallax.GetCurrent();
        if (target == null
            || !_prototypeManager.TryIndex<ParallaxPrototype>(target, out var proto)
            || proto.SectorName == string.Empty)
        {
            _arrival.Show(Loc.GetString(ArrivalFallbackTitle), string.Empty, ArrivalDuration);
            return;
        }

        _arrival.Show(Loc.GetString(proto.SectorName), Localize(proto.SectorDescription), ArrivalDuration);
    }

    private string Localize(string key)
    {
        return key == string.Empty ? string.Empty : Loc.GetString(key);
    }

    private void StopTransitHum()
    {
        if (_humLoop == null)
            return;

        _audio.Stop(_humLoop);
        _humLoop = null;
    }

    public override void Update(float frameTime)
    {
        var warping = _parallax.IsWarping;
        var speed = _parallax.WarpSpeedFraction;
        VignetteStrength = MaxVignette * speed * speed;

        if (warping && !_wasWarping)
        {
            // The transition is already requested here, so its target can be captured before the
            // fade clears it.
            _arrivalTarget = _parallax.GetTransitionTo();
            TriggerShockwave(EntryStrength);
            StopTransitHum();
            _humLoop = _audio.PlayGlobal(TransitHumSound, Filter.Local(), false, TransitHumParams)?.Entity;
            _exitFired = false;
            _arrivalFired = false;
        }
        else if (warping && !_exitFired && _parallax.WarpProgress >= ExitAt)
        {
            TriggerShockwave(ExitStrength);
            _audio.PlayGlobal(ExitSound, Filter.Local(), false, WarpAudioParams);
            _exitFired = true;
        }

        if (!warping && _wasWarping)
            StopTransitHum();

        if (warping && !_arrivalFired && _parallax.WarpProgress >= ArrivalAt)
        {
            _arrivalFired = true;
            ShowArrivalCard();
        }

        _wasWarping = warping;
    }
}
