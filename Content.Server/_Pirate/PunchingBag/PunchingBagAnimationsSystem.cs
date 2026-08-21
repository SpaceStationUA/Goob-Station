using Content.Shared._Pirate.PunchingBag;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;

namespace Content.Server._Pirate.PunchingBag;

public sealed class PunchingBagAnimationsSystem : SharedPunchingBagAnimationsSystem
{
    private readonly Dictionary<EntityUid, float> _playerPullStrength = new();

    protected override void PlayAnimation(EntityUid uid, EntityUid attacker, string animationState)
    {
        var filter = Filter.Pvs(uid, entityManager: EntityManager);

        if (TryComp(attacker, out ActorComponent? actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(new PunchingBagAnimationEvent(GetNetEntity(uid), animationState), filter);

        PullerComponent? pullerComp = null;
        if (Resolve(attacker, ref pullerComp))
        {
            if (!_playerPullStrength.TryGetValue(attacker, out var currentStrength))
            {
                currentStrength = 0f;
            }

            currentStrength += 0.02f;
            _playerPullStrength[attacker] = currentStrength;

            float targetReduction;
            if (currentStrength >= 1.0f)
            {
                targetReduction = 1.0f;
            }
            else if (currentStrength >= 0.70f)
            {
                targetReduction = 0.70f;
            }
            else if (currentStrength >= 0.40f)
            {
                targetReduction = 0.40f;
            }
            else
            {
                targetReduction = pullerComp.PulledDensityReduction;
            }

            pullerComp.PulledDensityReduction = targetReduction;
            Dirty(attacker, pullerComp);
        }
    }
}

