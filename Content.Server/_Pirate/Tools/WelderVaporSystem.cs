using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Content.Shared.Tools.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Pirate.Tools;

public sealed class WelderVaporSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;

    private const float UpdateInterval = 1.0f;
    private const float VaporAmountPerUpdate = 0.02f;
    private float _accumulator;

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        while (_accumulator >= UpdateInterval)
        {
            _accumulator -= UpdateInterval;

            var query = EntityQueryEnumerator<WelderComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var welder, out var xform))
            {
                if (!welder.Enabled)
                    continue;

                var parent = xform.ParentUid;
                if (!IsWelding(uid, parent))
                    continue;

                var mixture = _atmos.GetContainingMixture((uid, (TransformComponent?) xform), ignoreExposed: true, excite: true);
                if (mixture != null)
                    mixture.AdjustMoles(Gas.WaterVapor, VaporAmountPerUpdate);
            }
        }
    }

    private bool IsWelding(EntityUid welder, EntityUid parent)
    {
        if (!TryComp<DoAfterComponent>(parent, out var doAfterComp))
            return false;

        foreach (var doAfter in doAfterComp.DoAfters.Values)
        {
            if (doAfter.Args.Used == welder)
                return true;
        }

        return false;
    }
}
