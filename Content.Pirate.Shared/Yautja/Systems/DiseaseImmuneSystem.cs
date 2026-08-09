using Content.Goobstation.Shared.Disease;
using Content.Pirate.Shared.Yautja.Components;

namespace Content.Pirate.Shared.Yautja.Systems;

public sealed class DiseaseImmuneSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DiseaseImmuneComponent, DiseaseInfectAttemptEvent>(OnInfectAttempt);
    }

    private void OnInfectAttempt(Entity<DiseaseImmuneComponent> ent, ref DiseaseInfectAttemptEvent args)
    {
        args.CanInfect = false;
    }
}
