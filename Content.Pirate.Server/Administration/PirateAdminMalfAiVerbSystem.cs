// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.Administration.Systems;

public sealed class PirateAdminMalfAiVerbSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;
        if (!_admin.HasAdminFlag(player, AdminFlags.Fun))
            return;

        var target = args.Target;
        if (TryComp<MovementRelayTargetComponent>(target, out var relay))
            target = relay.Source;

        if (!HasComp<MindContainerComponent>(target) ||
            !HasComp<StationAiHeldComponent>(target) ||
            !TryComp<ActorComponent>(target, out var targetActor))
        {
            return;
        }

        var targetPlayer = targetActor.PlayerSession;

        if (_mind.TryGetMind(targetPlayer, out var mindId, out _) &&
            _roles.MindHasRole<MalfAiRoleComponent>(mindId))
        {
            return;
        }

        Verb malfAi = new()
        {
            Text = Loc.GetString("admin-verb-text-make-malfai"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(
                new ResPath("/Textures/Interface/Actions/actions_malf_ai.rsi"),
                "malfai_bg"),
            Act = () => _antag.ForceMakeAntag<MalfAiRuleComponent>(targetPlayer, "MalfAi"),
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-malfai", ("targetName", Name(args.Target))),
        };
        args.Verbs.Add(malfAi);
    }
}
