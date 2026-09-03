// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.EntityEffects.Effects;
using Content.Shared.Damage.Components;
using Content.Shared.Light;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server._Pirate.EntityEffects.Effects;

/// <summary>
/// Server side of the temporary godmode effect: manages the green glow light and
/// removes it again when the effect ends (unless the entity had its own light).
/// </summary>
public sealed class TemporaryGodmodeSystem : SharedTemporaryGodmodeSystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemporaryGodmodeComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TemporaryGodmodeComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(Entity<TemporaryGodmodeComponent> ent, ref ComponentInit args)
    {
        if (HasComp<PointLightComponent>(ent))
        {
            ent.Comp.HadPointLight = true;
            return;
        }

        var light = _light.EnsureLight(ent);
        _light.SetRadius(ent, ent.Comp.LightRadius, light);
        _light.SetEnergy(ent, ent.Comp.LightEnergy, light);
        _light.SetColor(ent, ent.Comp.TintColor, light);
        _light.SetCastShadows(ent, false, light);
    }

    private void OnRemove(Entity<TemporaryGodmodeComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.HadPointLight || TerminatingOrDeleted(ent))
            return;

        RemComp<PointLightComponent>(ent);
    }
}
