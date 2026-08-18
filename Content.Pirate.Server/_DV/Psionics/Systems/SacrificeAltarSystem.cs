using Content.Server._DV.Psionics.Components;
using Content.Server.Administration.Logs;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Shared._DV.Psionics.Components;
using Content.Goobstation.Common.Religion;
using Content.Shared.Buckle.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Psionics.Glimmer;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DV.Psionics.Systems;

/// <summary>
/// Handles sacrificing a buckled psionic on an altar to reduce glimmer.
/// A player with psionics or clerical training can right-click the altar and
/// choose "Sacrifice Psionic" from the context menu.
/// After a DoAfter delay, the sacrifice is performed.
/// </summary>
public sealed class SacrificeAltarSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GlimmerSystem _glimmer = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SacrificeAltarComponent, GetVerbsEvent<AlternativeVerb>>(AddSacrificeVerb);
        SubscribeLocalEvent<SacrificeAltarComponent, SacrificeDoAfterEvent>(OnDoAfter);
    }

    private void AddSacrificeVerb(EntityUid uid, SacrificeAltarComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Only psionics or people with clerical training (Chaplain) can sacrifice.
        if (!HasComp<PotentialPsionicComponent>(args.User)
            && !HasComp<BibleUserComponent>(args.User))
            return;

        // Check if someone is buckled to the altar.
        if (!TryComp<StrapComponent>(uid, out var strap) || strap.BuckledEntities.Count == 0)
            return;

        // Find the first buckled psionic.
        EntityUid? target = null;
        foreach (var buckled in strap.BuckledEntities)
        {
            if (HasComp<PotentialPsionicComponent>(buckled) || HasComp<PsionicComponent>(buckled))
            {
                target = buckled;
                break;
            }
        }

        if (target == null)
            return;

        AlternativeVerb verb = new()
        {
            Act = () => StartSacrifice(uid, target.Value, args.User, component),
            Text = Loc.GetString("sacrifice-altar-verb-sacrifice"),
            Priority = 1
        };

        args.Verbs.Add(verb);
    }

    private void StartSacrifice(EntityUid altar, EntityUid target, EntityUid user, SacrificeAltarComponent component)
    {
        if (!TryComp<StrapComponent>(altar, out var strap) || !strap.BuckledEntities.Contains(target))
        {
            _popup.PopupClient(Loc.GetString("sacrifice-altar-target-not-buckled"), user, user);
            return;
        }

        // Start the DoAfter — the psionic check happens after this delay.
        var ev = new SacrificeDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(EntityManager, user, component.DoAfterDuration, ev, altar, target)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out _))
            return;

        _popup.PopupEntity(
            Loc.GetString("sacrifice-altar-begin", ("user", user), ("target", target)),
            altar, PopupType.MediumCaution);
    }

    private void OnDoAfter(EntityUid uid, SacrificeAltarComponent component, ref SacrificeDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var target = args.Target ?? uid;
        var user = args.User;

        // Re-validate after the DoAfter delay — psionic check happens HERE.
        if (!TryComp<StrapComponent>(uid, out var strap) || !strap.BuckledEntities.Contains(target))
        {
            _popup.PopupClient(Loc.GetString("sacrifice-altar-target-not-buckled"), user, user);
            return;
        }

        if (!HasComp<PotentialPsionicComponent>(target) && !HasComp<PsionicComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("sacrifice-altar-target-not-psionic"), user, user);
            return;
        }

        if (TryComp<MobStateComponent>(target, out var mobState) && mobState.CurrentState == MobState.Dead)
        {
            _popup.PopupClient(Loc.GetString("sacrifice-altar-target-already-dead"), user, user);
            return;
        }

        // Announce the sacrifice to everyone nearby.
        _popup.PopupEntity(
            Loc.GetString("sacrifice-altar-announce", ("user", user), ("target", target)),
            uid, PopupType.LargeCaution);

        // Reduce glimmer.
        var glimmerReduction = _random.Next(component.GlimmerReductionMin, component.GlimmerReductionMax + 1);
        _glimmer.Glimmer -= glimmerReduction;

        var coords = Transform(target).Coordinates;

        // Spawn bluespace crystals.
        var crystalCount = _random.Next(component.BsCrystalMin, component.BsCrystalMax + 1);
        for (var i = 0; i < crystalCount; i++)
        {
            Spawn("MaterialBSCrystal1", coords);
        }

        // Spawn ectoplasm.
        Spawn("Ectoplasm", coords);

        // Log the sacrifice.
        _adminLog.Add(LogType.Psionics,
            LogImpact.High,
            $"{ToPrettyString(user):player} sacrificed {ToPrettyString(target):player} on {ToPrettyString(uid):player}, reducing glimmer by {glimmerReduction}");

        // Gib the target.
        _body.GibBody(target, gibOrgans: true);

        // Play a sound.
        _audio.PlayPvs("/Audio/Effects/hallelujah.ogg", uid);
    }
}

/// <summary>
/// DoAfter event for the psionic sacrifice ritual.
/// </summary>
public sealed partial class SacrificeDoAfterEvent : SimpleDoAfterEvent;
