using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Pirate.Shared.TerrorSpider.EntitySystems;

public sealed class TerrorEggSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly EntProtoId[] _terrorSpiders =
    {
        "MobTerrorGray",
        "MobTerrorGreen",
        "MobTerrorRed"
    };
    private DamageSpecifier? _damage;
    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator -= 1f;
        _damage ??= new DamageSpecifier(_prototypes.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(1));

        var query = EntityQueryEnumerator<EggHolderComponent>();
        while (query.MoveNext(out var uid, out var eggHolder))
        {
            eggHolder.Counter++;
            _damageable.TryChangeDamage(uid, _damage, false);

            if (eggHolder.Counter < 300)
                continue;

            PredictedSpawnAtPosition(_random.Pick(_terrorSpiders), Transform(uid).Coordinates);
            RemComp<EggHolderComponent>(uid);
        }
    }
}
