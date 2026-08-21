using Content.Server.Chat.Managers;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared._DV.Psionics.Events.PowerDoAfterEvents;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server._DV.Psionics.Systems.PsionicPowers;

public sealed class AssayPowerSystem : SharedAssayPowerSystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AssayPowerComponent, AssayDoAfterEvent>(OnDoAfter);
    }

    /// <summary>
    ///     Scans a target over a short DoAfter, then reports the target's psionic statistics to the caster's chat window.
    /// </summary>
    protected override void OnPowerUsed(Entity<AssayPowerComponent> psionic, ref AssayPowerActionEvent args)
    {
        if (psionic.Comp.GetDoAfterId() is not null)
            return;

        // Blocked by psionic shielding (e.g. a DarkSwap user in the shadow realm) - don't consume the cooldown.
        if (!Psionic.CanBeTargeted(args.Target, ignorePsionicRequirement: true, hasAggressor: args.Performer))
        {
            args.Handled = false;
            return;
        }

        var ev = new AssayDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(EntityManager, args.Performer, psionic.Comp.UseDelay, ev, psionic, target: args.Target)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!DoAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return;

        psionic.Comp.SaveDoAfterId(doAfterId.Value);
        Dirty(psionic);

        Popup.PopupEntity(Loc.GetString("assay-begin", ("entity", args.Target)), args.Performer, PopupType.Medium);
        _audioSystem.PlayPvs("/Audio/_EinsteinEngines/Psionics/heartbeat_fast.ogg",
            args.Performer,
            AudioParams.Default.WithVolume(8f).WithMaxDistance(1.5f).WithRolloffFactor(3.5f));

        AfterPowerUsed(psionic, args.Performer);
    }

    /// <summary>
    ///     Assuming the DoAfter wasn't cancelled, the user wasn't mindbroken, and the target still exists, prepare the scan results.
    /// </summary>
    private void OnDoAfter(Entity<AssayPowerComponent> psionic, ref AssayDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        psionic.Comp.RemoveSavedDoAfterId();
        Dirty(psionic);

        var user = psionic.Owner;
        if (args.Cancelled
            || args.Target is not { } target
            || !_playerManager.TryGetSessionByEntity(user, out var session))
            return;

        if (InspectSelf(user, target, psionic, session))
            return;

        // If the target only has potential (not yet a full psionic), show the potential text.
        if (HasComp<PotentialPsionicComponent>(target) && !HasComp<PsionicComponent>(target))
        {
            var potential = Loc.GetString("assay-potential", ("entity", target));
            ApplyAssayResponse(target, ref potential);
            Popup.PopupEntity(potential, user, user, PopupType.Large);
            SendDescToChat($"[font size={psionic.Comp.FontSize}][color={psionic.Comp.FontColor}]{potential}[/color][/font]", session);
            return;
        }

        // List the target's psionic powers
        var powers = new List<string>();
        foreach (var comp in EntityManager.GetComponents(target))
        {
            if (comp is not BasePsionicPowerComponent power)
                continue;

            powers.Add(power.PowerMetapsionicFeedback is { } feedback
                ? Loc.GetString(feedback, ("entity", target))
                : Loc.GetString(power.PowerName));
        }

        if (powers.Count == 0)
        {
            var noPowers = Loc.GetString("no-powers", ("entity", target));
            ApplyAssayResponse(target, ref noPowers);
            Popup.PopupEntity(noPowers, user, user, PopupType.LargeCaution);
            SendDescToChat($"[font size={psionic.Comp.FontSize}][color={psionic.Comp.FontColor}]{noPowers}[/color][/font]", session);
            return;
        }

        // Each power is described on its own line in the chat report.
        var message = Loc.GetString("assay-psionic", ("entity", target)) + "\n" + string.Join("\n", powers);
        ApplyAssayResponse(target, ref message);
        SendDescToChat($"[font size={psionic.Comp.FontSize}][color={psionic.Comp.FontColor}]{message}[/color][/font]", session);
    }

    /// <summary>
    ///     Lets the scanned target rewrite or append to its own assay result via <see cref="AssayResponseComponent"/>.
    /// </summary>
    private void ApplyAssayResponse(EntityUid target, ref string message)
    {
        if (!TryComp<AssayResponseComponent>(target, out var response))
            return;

        if (response.ReplaceMessage is { } replace)
        {
            message = Loc.GetString(replace, ("entity", target));
            return;
        }

        if (response.AppendMessage is { } append)
            message += $" {Loc.GetString(append, ("entity", target))}";
    }

    /// <summary>
    ///     A special easter egg for scanning yourself.
    /// </summary>
    private bool InspectSelf(EntityUid user, EntityUid target, Entity<AssayPowerComponent> psionic, ICommonSession session)
    {
        if (target != user)
            return false;

        var assaySelf = Loc.GetString("assay-self", ("entity", target));
        Popup.PopupEntity(assaySelf, user, user, PopupType.LargeCaution);
        SendDescToChat($"[font size=20][color=#ff0000]{assaySelf}[/color][/font]", session);
        return true;
    }

    private void SendDescToChat(string feedbackMessage, ICommonSession session)
    {
        _chatManager.ChatMessageToOne(
            ChatChannel.Emotes,
            feedbackMessage,
            feedbackMessage,
            EntityUid.Invalid,
            false,
            session.Channel);
    }
}
