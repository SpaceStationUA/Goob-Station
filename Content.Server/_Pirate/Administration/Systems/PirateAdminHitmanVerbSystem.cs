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

public sealed class PirateAdminHitmanVerbSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IAdminManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (!_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) ||
            !TryComp<ActorComponent>(args.Target, out var targetActor))
        {
            return;
        }

        Verb hitman = new()
        {
            Text = Loc.GetString("admin-verb-make-hitman"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(
                new ResPath("/Textures/_Pirate/Interface/Misc/job_icons.rsi"),
                "Hitman"),
            Act = () =>
            {
                _antag.ForceMakeAntag<HitmanRuleComponent>(targetActor.PlayerSession, "HitmanRule");
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-text-make-hitman"),
        };
        args.Verbs.Add(hitman);
    }
}
