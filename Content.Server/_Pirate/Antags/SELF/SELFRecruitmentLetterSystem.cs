// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Paper;
using Content.Server._Pirate.GameTicking.Rules.Components;
using Content.Server.Antag;
using Content.Shared._Pirate.Roles.Components;
using Content.Shared.Fax.Components;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Antags.SELF;

public sealed class SELFRecruitmentLetterSystem : EntitySystem
{
    private const string DefaultSELFRule = "SiliconLiberation";

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SELFRecruitmentLetterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SELFRecruitmentLetterComponent, SignSuccessfulEvent>(OnSigned);
    }

    private void OnMapInit(Entity<SELFRecruitmentLetterComponent> ent, ref MapInitEvent args)
    {
        // The recruitment effect must not be duplicable through a fax machine.
        RemComp<FaxableObjectComponent>(ent.Owner);
    }

    private void OnSigned(Entity<SELFRecruitmentLetterComponent> ent, ref SignSuccessfulEvent args)
    {
        if (ent.Comp.Used
            || !TryComp<ActorComponent>(args.User, out var actor)
            || !_mind.TryGetMind(args.User, out var mindId, out _)
            || IsBlacklisted(mindId))
        {
            return;
        }

        ent.Comp.Used = true;
        _antag.ForceMakeAntag<SELFRuleComponent>(actor.PlayerSession, DefaultSELFRule);
    }

    private bool IsBlacklisted(EntityUid mindId)
    {
        return _roles.MindHasRole<NukeopsRoleComponent>(mindId)
            || _roles.MindHasRole<TraitorRoleComponent>(mindId)
            || _roles.MindHasRole<RevolutionaryRoleComponent>(mindId)
            || _roles.MindHasRole<SELFAgentRoleComponent>(mindId);
    }
}
