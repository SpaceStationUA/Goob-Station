// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Shared.Actions.Events;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Magic.Components;
using Content.Shared.Speech.Components;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Goobstation.Wizard.Chuuni;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;

namespace Content.Server._Pirate.Speech;

public sealed class SpeakOnActionPirateSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpeakOnActionComponent, ActionAttemptEvent>(OnActionAttempt);
    }

    private void OnActionAttempt(Entity<SpeakOnActionComponent> ent, ref ActionAttemptEvent args)
    {
        // Only intercept emote actions; let normal speak pass through to base system.
        if (!ent.Comp.IsEmote)
            return;


        var speech = ent.Comp.Sentence;

        if (TryComp(ent, out MagicComponent? magic))
        {
            var invocationEv = new GetSpellInvocationEvent(magic.School, args.User);
            RaiseLocalEvent(args.User, invocationEv);
            if (invocationEv.Invocation.HasValue)
                speech = invocationEv.Invocation;
            if (invocationEv.ToHeal.GetTotal() > FixedPoint2.Zero)
            {
                _damageable.TryChangeDamage(args.User,
                    -invocationEv.ToHeal,
                    true,
                    false,
                    targetPart: TargetBodyPart.All,
                    splitDamage: SplitDamageBehavior.SplitEnsureAll);
            }
        }

        if (string.IsNullOrWhiteSpace(speech))
        {
            args.Cancelled = true;
            return;
        }

        _chat.TrySendInGameICMessage(args.User, Loc.GetString(speech), InGameICChatType.Emote, false);
    }
}
