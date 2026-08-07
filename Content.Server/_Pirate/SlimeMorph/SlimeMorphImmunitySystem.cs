using Content.Shared._Pirate.SlimeMorph;
using Content.Shared.Popups;

namespace Content.Server._Pirate.SlimeMorph;

public sealed class SlimeMorphImmunitySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SlimeMorphImmunityComponent, SlimeMorphStudyAttemptEvent>(OnStudyAttempt);
        SubscribeLocalEvent<SlimeMorphImmunityComponent, SlimeMorphMimicAttemptEvent>(OnMimicAttempt);
    }

    private void OnStudyAttempt(Entity<SlimeMorphImmunityComponent> ent, ref SlimeMorphStudyAttemptEvent args)
    {
        args.Cancel();
        _popup.PopupEntity(Loc.GetString("slime-morph-study-immunity"), args.User, args.User);
    }

    private void OnMimicAttempt(Entity<SlimeMorphImmunityComponent> ent, ref SlimeMorphMimicAttemptEvent args)
    {
        args.Cancel();
        _popup.PopupEntity(Loc.GetString("slime-morph-study-immunity"), args.User, args.User);
    }
}
