/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._FarHorizons.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;

namespace Content.Client._FarHorizons.Planets;

/// <summary>
/// Client-side: registers the <see cref="CEPlanetOverlay"/> that renders planet bodies into the
/// parallax background, and removes it with the rest of the star system when the
/// "Render Star System" graphics option is turned off.
/// </summary>
public sealed partial class CEPlanetSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private CEPlanetOverlay _planetOverlay = default!;
    private CEZCloudsOverlay _cloudsOverlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _planetOverlay = new CEPlanetOverlay();
        _cloudsOverlay = new CEZCloudsOverlay();
        _cfg.OnValueChanged(FHCCVars.RenderStarSystem, EnsureOverlay, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(FHCCVars.RenderStarSystem, EnsureOverlay);
        if (_overlay.HasOverlay<CEPlanetOverlay>())
            _overlay.RemoveOverlay(_planetOverlay);
        if (_overlay.HasOverlay<CEZCloudsOverlay>())
            _overlay.RemoveOverlay(_cloudsOverlay);
    }

    private void EnsureOverlay(bool enabled)
    {
        if (enabled)
        {
            if (!_overlay.HasOverlay<CEPlanetOverlay>())
                _overlay.AddOverlay(_planetOverlay);
            if (!_overlay.HasOverlay<CEZCloudsOverlay>())
                _overlay.AddOverlay(_cloudsOverlay);
        }
        else
        {
            if (_overlay.HasOverlay<CEPlanetOverlay>())
                _overlay.RemoveOverlay(_planetOverlay);
            if (_overlay.HasOverlay<CEZCloudsOverlay>())
                _overlay.RemoveOverlay(_cloudsOverlay);
        }
    }
}
