// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Revolutionary;
using Content.Shared._EinsteinEngines.Revolutionary;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Revolutionary.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.GameTicking.Rules;

public sealed partial class RevolutionaryRuleSystem
{
    [Dependency] private readonly SharedHandsSystem _recruitmentHands = default!;
    [Dependency] private readonly SharedInteractionSystem _recruitmentInteraction = default!;

    private readonly HashSet<EntityUid> _pendingRecruits = new();
    private static readonly TimeSpan RecruitmentTimeout = TimeSpan.FromSeconds(30);

    private void OfferRecruitment(EntityUid leader, AfterRevolutionaryConvertedEvent offer, EntityUid mind)
    {
        var target = offer.Target;
        if (offer.Used is not { } converter || !TryComp<ActorComponent>(target, out var actor)
            || !_pendingRecruits.Add(target))
            return;

        var expires = _timing.CurTime + RecruitmentTimeout;
        var prompt = new RevolutionaryRecruitmentEui(session =>
        {
            if (session.AttachedEntity != target || _timing.CurTime >= expires
                || !_mind.TryGetMind(target, out var currentMind, out _) || currentMind != mind
                || !TryComp<HeadRevolutionaryComponent>(leader, out var head) || !_mobState.IsAlive(leader)
                || !_recruitmentHands.IsHolding(leader, converter)
                || !_recruitmentInteraction.InRangeUnobstructed(leader, target))
                return;

            TryRecruit(leader, head, offer, accepted: true);
        }, () => _pendingRecruits.Remove(target));

        _euiMan.OpenEui(prompt, actor.PlayerSession);
        Timer.Spawn(RecruitmentTimeout, () =>
        {
            if (!prompt.IsShutDown)
                prompt.Close();
        });
    }
}
