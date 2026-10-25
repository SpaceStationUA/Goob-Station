using Content.Goobstation.Shared.MartialArts;

namespace Content.Pirate.Shared._Pirate.MartialArts;

public sealed class UnathiTailSystem : EntitySystem
{
    [Dependency] private readonly SharedMartialArtsSystem _martialArts = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GrantUnathiTailComponent, ComponentInit>(OnGrantInit);
    }

    private void OnGrantInit(Entity<GrantUnathiTailComponent> ent, ref ComponentInit args)
    {
        _martialArts.TryGrantMartialArt(ent.Owner, ent.Comp);
    }
}
