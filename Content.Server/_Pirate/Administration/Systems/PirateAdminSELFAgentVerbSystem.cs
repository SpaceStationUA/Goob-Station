// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.GameTicking.Rules.Components;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Administration.Systems;

public sealed class PirateAdminSELFAgentVerbSystem : EntitySystem
{
    private const string DefaultSELFRule = "SiliconLiberation";

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IAdminManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor)
            || !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun)
            || !HasComp<MindContainerComponent>(args.Target)
            || !TryComp<ActorComponent>(args.Target, out var targetActor))
        {
            return;
        }

        var name = Loc.GetString("admin-verb-text-make-selfagent");
        args.Verbs.Add(new Verb
        {
            Text = name,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(
                new ResPath("/Textures/_Pirate/Objects/Specific/SELF/freemag.rsi"),
                "icon"),
            Act = () => _antag.ForceMakeAntag<SELFRuleComponent>(targetActor.PlayerSession, DefaultSELFRule),
            Impact = LogImpact.High,
            Message = string.Join(": ", name, Loc.GetString("admin-verb-make-selfagent")),
        });
    }
}
