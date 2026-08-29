using Content.Shared._Pirate.PunchingBag;
using Robust.Shared.Player;

namespace Content.Server._Pirate.PunchingBag;

public sealed class PunchingBagAnimationsSystem : SharedPunchingBagAnimationsSystem
{
    protected override void PlayAnimation(EntityUid uid, EntityUid attacker, string animationState)
    {
        var filter = Filter.Pvs(uid, entityManager: EntityManager);

        if (TryComp(attacker, out ActorComponent? actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(new PunchingBagAnimationEvent(GetNetEntity(uid), animationState), filter);

    }
}
