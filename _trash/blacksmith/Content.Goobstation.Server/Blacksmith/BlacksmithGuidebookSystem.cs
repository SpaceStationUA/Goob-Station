using Content.Goobstation.Shared.Blacksmith;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Goobstation.Server.Blacksmith;

/// <summary>
/// Studying the guidebook: first use 5s → level 1, second use 5min → level 2.
/// </summary>
public sealed class BlacksmithGuidebookSystem : EntitySystem
{
    private static readonly TimeSpan FirstStudyDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SecondStudyDelay = TimeSpan.FromMinutes(5);

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlacksmithGuidebookComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<BlacksmithGuidebookComponent, BlacksmithStudyDoAfterEvent>(OnStudyFinished);
    }

    private void OnUseInHand(Entity<BlacksmithGuidebookComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var knowledge = EnsureComp<BlacksmithKnowledgeComponent>(args.User);
        if (knowledge.Level >= 2)
        {
            _popup.PopupEntity(Loc.GetString("blacksmith-guidebook-already-mastered"), args.User, args.User);
            // Still allow opening the guide via GuideHelp; don't block completely.
            return;
        }

        var delay = knowledge.Level == 0 ? FirstStudyDelay : SecondStudyDelay;
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new BlacksmithStudyDoAfterEvent(), ent, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString(knowledge.Level == 0
                ? "blacksmith-guidebook-study-start"
                : "blacksmith-guidebook-study-start-long"),
            args.User,
            args.User);
    }

    private void OnStudyFinished(Entity<BlacksmithGuidebookComponent> ent, ref BlacksmithStudyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var user = args.Args.User;
        var knowledge = EnsureComp<BlacksmithKnowledgeComponent>(user);
        if (knowledge.Level >= 2)
            return;

        knowledge.Level++;
        Dirty(user, knowledge);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg"), user);

        _popup.PopupEntity(
            Loc.GetString(knowledge.Level == 1
                ? "blacksmith-guidebook-study-level1"
                : "blacksmith-guidebook-study-level2"),
            user,
            user);
    }
}
