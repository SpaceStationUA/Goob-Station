// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Skia;
using Content.Shared.Alert;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Skia;

public sealed class SkiaResurrectWhenAbleSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly ProtoId<AlertPrototype> _resurrectingAlert = "SkiaResurrecting";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalSession?.AttachedEntity is not { } entity
            || !TryComp<SkiaResurrectWhenAbleComponent>(entity, out var component))
            return;

        if (component.ResurrectAt is not { } resurrectAt)
        {
            _alerts.ClearAlert(entity, _resurrectingAlert);
            return;
        }

        _alerts.ShowAlert(
            entity,
            _prototype.Index(_resurrectingAlert),
            cooldown: (resurrectAt - TimeSpan.FromSeconds(component.TimeToResurrect), resurrectAt));
    }
}
