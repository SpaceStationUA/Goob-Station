using Content.Shared.Standing;
using Robust.Client.GameObjects;

namespace Content.Client.Standing;

public sealed class CrawlUnderVisualsSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StandingStateComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var standing, out var sprite))
        {
            var targetDepth = standing.IsCrawlingUnder 
                ? standing.CrawlingUnderDrawDepth 
                : standing.NormalDrawDepth;

            if (sprite.DrawDepth != targetDepth)
            {
                sprite.DrawDepth = targetDepth;
            }
        }
    }
}
