// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server._Pirate.Chimera.GameTicking.Rules;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Chimera;

public sealed class PirateAdminChimeraVerbSystem : EntitySystem
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
        if (!TryComp<ActorComponent>(args.User, out var actor)
            || !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun)
            || !HasComp<MindContainerComponent>(args.Target)
            || HasComp<SiliconComponent>(args.Target)
            || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("admin-verb-text-make-chimera"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("Structures/Specific/Anomalies/Cores/flesh_core.rsi"), "core"),
            Act = () => _antag.ForceMakeAntag<ChimeraRuleComponent>(targetActor.PlayerSession, "Letoferol"),
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-chimera"),
        });
    }
}
