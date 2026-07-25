using System.Linq;
using Content.Server.Body.Components;
using Content.Pirate.Server.Traits.Vampirism.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Prototypes;
using Robust.Shared.Analyzers;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Traits.Vampirism;

[Access(typeof(MetabolizerComponent), Other = AccessPermissions.ReadWriteExecute)]
public sealed class VampirismSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly MetabolizerSystem _metabolizer = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<VampirismComponent, MapInitEvent>(OnInitVampire);
    }

    private void OnInitVampire(Entity<VampirismComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<BloodSuckerComponent>(ent);

        SetMetabolizerTypes(ent, ent.Comp.MetabolizerPrototypes);
    }

    public void SetMetabolizerTypes(EntityUid uid, HashSet<ProtoId<MetabolizerTypePrototype>> metabolizerTypes)
    {
        if (metabolizerTypes == null)
            return;

        if (!TryComp<BodyComponent>(uid, out var body)
            || !_body.TryGetBodyOrganEntityComps<MetabolizerComponent>((uid, body), out var comps))
            return;

        foreach (var comp in comps)
        {
            _metabolizer.SetMetabolizerTypes((comp.Comp2.Owner, comp.Comp1), metabolizerTypes);
        }
    }

}
