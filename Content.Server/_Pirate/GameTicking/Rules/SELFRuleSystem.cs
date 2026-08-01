// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.GameTicking.Rules.Components;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Shared._Pirate.Antags.SELF;
using Content.Shared._Pirate.Roles.Components;

namespace Content.Server._Pirate.GameTicking.Rules;

public sealed class SELFRuleSystem : GameRuleSystem<SELFRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SELFRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<SELFAgentRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void OnAntagSelected(Entity<SELFRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        _antag.SendBriefing(args.EntityUid, MakeBriefing(), null, null);
        EnsureComp<SELFAgentComponent>(args.EntityUid);
    }

    private void OnGetBriefing(Entity<SELFAgentRoleComponent> ent, ref GetBriefingEvent args)
    {
        if (args.Mind.Comp.OwnedEntity == null)
            return;

        args.Append(MakeBriefing());
    }

    private string MakeBriefing()
    {
        return Loc.GetString("self-role-greeting-human")
            + "\n \n"
            + Loc.GetString("self-role-greeting-equipment")
            + "\n";
    }
}
