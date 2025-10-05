using System.Linq;
using Content.Server.Body.Components;
using Content.Pirate.Server.Traits.Vampirism.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;

using Robust.Shared.Analyzers;

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
        // Check if entity has a stomach, unless requirement is ignored. Timely, instead of trait requirements
        if (!ent.Comp.IgnoreStomachRequirement)
        {
            if (!TryComp<BodyComponent>(ent, out var bodyCheck)
                || !_body.TryGetBodyOrganEntityComps<StomachComponent>((ent, bodyCheck), out var stomachComps)
                || stomachComps.Count == 0)
            {
                // No stomach found and requirement not ignored - don't initialize vampirism
                return;
            }
        }

        EnsureBloodSucker(ent);

        if (!TryComp<BodyComponent>(ent, out var body)
            || !_body.TryGetBodyOrganEntityComps<MetabolizerComponent>((ent, body), out var comps))
            return;

        foreach (var comp in comps)
        {
            if (!TryComp<StomachComponent>(comp.Comp2.Owner, out var stomach))
                continue;

            _metabolizer.SetMetabolizerTypes((comp.Comp2.Owner, comp.Comp1), ent.Comp.MetabolizerPrototypes);

            if (ent.Comp.SpecialDigestible is {} whitelist)
                stomach.SpecialDigestible = whitelist;
        }
    }

    private void EnsureBloodSucker(Entity<VampirismComponent> uid)
    {
        if (HasComp<BloodSuckerComponent>(uid))
            return;

        AddComp(uid, new BloodSuckerComponent
        {
            Delay = uid.Comp.SuccDelay,
            InjectWhenSucc = false, // The code for it is deprecated, might wanna make it inject something when (if?) it gets reworked
            UnitsToSucc = uid.Comp.UnitsToSucc,
            WebRequired = false
        });
    }
}
