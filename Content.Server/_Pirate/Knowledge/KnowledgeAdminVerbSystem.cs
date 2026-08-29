// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Knowledge;

public sealed class KnowledgeAdminVerbSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !_admins.HasAdminFlag(actor.PlayerSession, AdminFlags.Debug) ||
            !HasComp<MobStateComponent>(args.Target) && _knowledge.GetContainer(args.Target) is null)
        {
            return;
        }

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("knowledge-admin-verb"),
            Message = Loc.GetString("knowledge-admin-verb-description"),
            Category = VerbCategory.Debug,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/vv.svg.192dpi.png")),
            Act = () => _eui.OpenEui(new KnowledgeAdminEui(args.Target), actor.PlayerSession),
            Impact = LogImpact.Medium,
        });
    }
}
