// SPDX-License-Identifier: MIT

using Content.Pirate.Shared.Revolutionary.Components;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Implants;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Traitor.Uplink;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Audio;

namespace Content.Pirate.Server.Revolutionary;

public sealed class RevolutionaryUplinkSystem : EntitySystem
{
    private static readonly SoundSpecifier LieutenantBriefingSound =
        new SoundPathSpecifier("/Audio/_Pirate/Ambience/Antag/Revolutionary/rev_lieu_intro.ogg");

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SubdermalImplantSystem _implants = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly UplinkSystem _uplink = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionaryRuleComponent, AfterAntagEntitySelectedEvent>(OnHeadRevolutionarySelected);
        SubscribeLocalEvent<RevolutionaryLieutenantComponent, ImplantImplantedEvent>(OnLieutenantImplanted);
        SubscribeLocalEvent<RevolutionaryLieutenantComponent, ImplantRemovedEvent>(OnLieutenantRemoved);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemoved);
    }

    private void OnHeadRevolutionarySelected(
        Entity<RevolutionaryRuleComponent> rule,
        ref AfterAntagEntitySelectedEvent args)
    {
        var headRevolutionary = args.EntityUid;
        if (!HasComp<HeadRevolutionaryComponent>(headRevolutionary)
            || !_mind.TryGetMind(headRevolutionary, out var mindId, out _))
        {
            return;
        }

        var uplinkTarget = _uplink.FindUplinkTarget(headRevolutionary);
        EntityUid? fallbackImplant = null;
        if (uplinkTarget == null)
        {
            fallbackImplant = _implants.AddImplant(headRevolutionary, "UplinkImplant");
            uplinkTarget = fallbackImplant;
        }

        if (uplinkTarget == null)
            return;

        if (!_uplink.AddUplink(
            headRevolutionary,
            rule.Comp.StartingBalance,
            rule.Comp.UplinkCurrencyId,
            rule.Comp.UplinkStoreId,
            uplinkTarget,
            out var setupEvent))
        {
            if (fallbackImplant != null)
                RemoveImplant(headRevolutionary, fallbackImplant.Value);

            return;
        }

        var roleBriefing = Loc.GetString("head-rev-briefing");
        if (setupEvent is { } setup)
        {
            if (setup.BriefingEntry is { } briefing)
                _antag.SendBriefing(headRevolutionary, briefing, Color.Red, null);

            if (setup.BriefingEntryShort is { } shortBriefing)
                roleBriefing += $"\n{shortBriefing}";
        }

        SetRoleBriefing(mindId, roleBriefing);
    }

    private void OnLieutenantImplanted(
        Entity<RevolutionaryLieutenantComponent> implant,
        ref ImplantImplantedEvent args)
    {
        var target = args.Implanted;
        if (!HasComp<RevolutionaryComponent>(target)
            || HasComp<HeadRevolutionaryComponent>(target)
            || HasComp<MindShieldComponent>(target)
            || HasComp<RevolutionaryLieutenantComponent>(target)
            || !_mind.TryGetMind(target, out var mindId, out _))
        {
            RemoveImplant(target, implant.Owner);
            return;
        }

        EnsureComp<RevolutionaryLieutenantComponent>(target);

        var briefing = Loc.GetString("rev-lieutenant-greeting");
        _antag.SendBriefing(target, briefing, Color.Red, LieutenantBriefingSound);
        SetRoleBriefing(mindId, briefing);
    }

    private void OnLieutenantRemoved(
        Entity<RevolutionaryLieutenantComponent> implant,
        ref ImplantRemovedEvent args)
    {
        var target = args.Implanted;
        if (HasOtherLieutenantImplant(target, implant.Owner))
            return;

        RemComp<RevolutionaryLieutenantComponent>(target);

        if (!_mind.TryGetMind(target, out var mindId, out _))
            return;

        var briefing = Loc.GetString(HasComp<HeadRevolutionaryComponent>(target)
            ? "head-rev-briefing"
            : "rev-briefing");
        SetRoleBriefing(mindId, briefing);
    }

    private bool HasOtherLieutenantImplant(EntityUid target, EntityUid removedImplant)
    {
        if (!TryComp<ImplantedComponent>(target, out var implanted))
            return false;

        foreach (var implant in implanted.ImplantContainer.ContainedEntities)
        {
            if (implant != removedImplant && HasComp<RevolutionaryLieutenantComponent>(implant))
                return true;
        }

        return false;
    }

    private void RemoveImplant(EntityUid target, EntityUid implant)
    {
        if (TryComp<ImplantedComponent>(target, out var implanted))
            _implants.ForceRemove((target, implanted), implant);
    }

    // Pirate - remove the physical lieutenant implant so its radio access is revoked too.
    private void OnRoleRemoved(RoleRemovedEvent args)
    {
        if (_role.MindHasRole<RevolutionaryRoleComponent>(args.MindId)
            || args.Mind.OwnedEntity is not { } target
            || TerminatingOrDeleted(target))
        {
            return;
        }

        RemComp<RevolutionaryLieutenantComponent>(target);

        if (!TryComp<ImplantedComponent>(target, out var implanted))
            return;

        var implants = new List<EntityUid>(implanted.ImplantContainer.ContainedEntities);
        foreach (var implant in implants)
        {
            if (HasComp<RevolutionaryLieutenantComponent>(implant))
                _implants.ForceRemove((target, implanted), implant);
        }
    }

    private void SetRoleBriefing(EntityUid mindId, string briefing)
    {
        if (_role.MindHasRole<RevolutionaryRoleComponent>(mindId, out var role))
            AddComp(role.Value, new RoleBriefingComponent { Briefing = briefing }, overwrite: true);
    }
}
