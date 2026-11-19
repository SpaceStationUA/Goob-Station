// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Pirate.Light;

public sealed class BloomSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private BloomOverlay? _overlay;
    private bool _enabled;
    private float _intensity;
    private float _threshold;
    private float _spread;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.PirateBloomIntensity, value => { _intensity = value; Refresh(); }, true);
        Subs.CVar(_cfg, CCVars.PirateBloomThreshold, value => { _threshold = value; Refresh(); }, true);
        Subs.CVar(_cfg, CCVars.PirateBloomSpread, value => { _spread = value; Refresh(); }, true);
        Subs.CVar(_cfg, CCVars.PirateBloom, value => { _enabled = value; Refresh(); }, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void Refresh()
    {
        if (!_enabled || _intensity <= 0f)
        {
            RemoveOverlay();
            return;
        }

        if (_overlay == null)
        {
            _overlay = new BloomOverlay();
            _overlayMan.AddOverlay(_overlay);
        }

        _overlay.Intensity = _intensity;
        _overlay.Threshold = _threshold;
        _overlay.Spread = _spread;
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayMan.RemoveOverlay(_overlay);
        _overlay.Dispose();
        _overlay = null;
    }
}
