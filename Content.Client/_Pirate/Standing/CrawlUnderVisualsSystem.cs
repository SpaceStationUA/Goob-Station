using Content.Shared.Standing;
using Robust.Client.GameObjects;

namespace Content.Client.Standing;

public sealed class CrawlUnderVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StandingStateComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<StandingStateComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite ||
            !args.TryGetData<bool>(CrawlUnderVisuals.Hidden, out var hidden))
            return;

        if (hidden)
            _sprites.SetDrawDepth((ent.Owner, sprite), ent.Comp.CrawlingUnderDrawDepth);
        else if (sprite.DrawDepth == ent.Comp.CrawlingUnderDrawDepth)
            _sprites.SetDrawDepth((ent.Owner, sprite), ent.Comp.NormalDrawDepth);
    }
}
