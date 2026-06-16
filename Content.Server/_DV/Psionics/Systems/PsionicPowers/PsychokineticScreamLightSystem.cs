using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared._DV.Psionics.Events;

namespace Content.Server._DV.Psionics.Systems.PsionicPowers;

public sealed class PsychokineticScreamLightSystem : EntitySystem
{
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoweredLightComponent, PsychokineticScreamShatterLightEvent>(OnShatterLight);
    }

    private void OnShatterLight(Entity<PoweredLightComponent> light, ref PsychokineticScreamShatterLightEvent args)
    {
        _poweredLight.TryDestroyBulb(light, light.Comp);
    }
}
