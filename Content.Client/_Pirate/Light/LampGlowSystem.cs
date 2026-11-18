// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._Pirate.Light;

public sealed class LampGlowSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private LampGlowOverlay? _overlay;
    private bool _enabled;
    private bool _cones;
    private float _intensity;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.PirateLampGlowIntensity, value => { _intensity = value; Refresh(); }, true);
        Subs.CVar(_cfg, CCVars.PirateLampGlowCone, value => { _cones = value; Refresh(); }, true);
        Subs.CVar(_cfg, CCVars.PirateLampGlow, value => { _enabled = value; Refresh(); }, true);
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
            _overlay = new LampGlowOverlay();
            _overlayMan.AddOverlay(_overlay);
        }

        _overlay.Intensity = _intensity;
        _overlay.DrawCones = _cones;
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
