using Content.Goobstation.Shared.Particles;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Fluids;

// Each effect has one texture because emitters cannot use multiple textures.
[RegisterComponent]
public sealed partial class HotSpringSteamComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<ParticleEffectPrototype>> Effects = new();
}
