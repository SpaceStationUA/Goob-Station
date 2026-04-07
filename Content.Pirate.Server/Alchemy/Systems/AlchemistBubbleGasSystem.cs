using Content.Pirate.Shared.Alchemy.Components;
using Content.Server.Chat.Systems;
using Content.Shared.Throwing;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Alchemy.Systems;

public sealed class AlchemistBubbleGasSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlchemistBubbleGasComponent, ComponentInit>(OnInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AlchemistBubbleGasComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextAction)
                continue;

            comp.NextAction = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(comp.MinInterval, comp.MaxInterval));

            _chat.TryEmoteWithChat(uid, "Laugh", forceEmote: true);

            var angle = _random.NextAngle();
            var direction = angle.ToVec() * 1.5f;
            _throwing.TryThrow(uid, direction, comp.JumpSpeed, uid, recoil: false, compensateFriction: true);

            if (!string.IsNullOrEmpty(comp.JumpEffect))
                Spawn(comp.JumpEffect, Transform(uid).Coordinates);
        }
    }

    private void OnInit(Entity<AlchemistBubbleGasComponent> ent, ref ComponentInit args)
    {
        ent.Comp.NextAction = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(ent.Comp.MinInterval, ent.Comp.MaxInterval));
    }
}
