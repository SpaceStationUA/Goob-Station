// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Skia;
using Content.Shared.Alert;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Skia;

public sealed class SkiaLightLevelHealthSystem : SharedSkiaLightLevelHealthSystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SkiaLightReactiveSystem _lightReactive = default!;

    private readonly ProtoId<AlertPrototype> _darkAlert = "SkiaLightLevelDark";
    private readonly ProtoId<AlertPrototype> _neutralAlert = "SkiaLightLevelNeutral";
    private readonly ProtoId<AlertPrototype> _brightAlert = "SkiaLightLevelBright";
    private readonly ProtoId<AlertCategoryPrototype> _alertCategory = "SkiaLight";

    private int _lastThreshold;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalSession?.AttachedEntity is not { } entity
            || !TryComp<SkiaLightLevelHealthComponent>(entity, out var lightHealth))
            return;

        var threshold = CurrentThreshold(_lightReactive.GetLightLevelForPoint(entity), lightHealth);
        var alertId = threshold switch
        {
            -1 => _darkAlert,
            1 => _brightAlert,
            _ => _neutralAlert,
        };

        if (threshold != _lastThreshold)
        {
            var category = _prototype.Index(_alertCategory);
            _alerts.ClearAlertCategory(entity, category);
        }

        _lastThreshold = threshold;
        _alerts.ShowAlert(entity, _prototype.Index(alertId));
    }
}
