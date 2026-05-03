using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking;
using Content.Server.Body.Systems;
using Content.Shared.Mobs;

namespace Content.Server.GameTicking.Rules;

public sealed class ParadoxSyncSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var query = EntityQueryEnumerator<ParadoxSyncComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Target == args.Target)
            {
                _body.GibBody(uid, splatModifier: 5f);
            }
        }
    }

    private void OnRoundEnd(RoundEndMessageEvent args)
    {
        var query = EntityQueryEnumerator<ParadoxSyncComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (Terminating(uid)) continue;

            Spawn(comp.EffectProto, _transform.GetMapCoordinates(uid));

            QueueDel(uid);
        }
    }
}