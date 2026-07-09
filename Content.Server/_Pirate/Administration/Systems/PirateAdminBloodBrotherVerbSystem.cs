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

public sealed class PirateAdminBloodBrotherVerbSystem : EntitySystem
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

        var player = actor.PlayerSession;
        if (!_admin.HasAdminFlag(player, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        Verb bloodBrother = new()
        {
            Text = Loc.GetString("admin-verb-text-make-blood-brother"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Pirate/Interface/Misc/job_icons.rsi"), "BloodBrother"),
            Act = () =>
            {
                _antag.ForceMakeAntag<BloodBrotherRuleComponent>(targetActor.PlayerSession, "BloodBrothers");
            },
            Impact = LogImpact.High,
            Message = Loc.GetString("admin-verb-make-blood-brother"),
        };
        args.Verbs.Add(bloodBrother);
    }
}
