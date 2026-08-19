using Content.Server._DV.Psionics.Components;
using Content.Server.Administration.Logs;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Events.PowerDoAfterEvents;
using Content.Goobstation.Common.Religion;
using Content.Shared.Buckle.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.IdentityManagement;
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

        // Check if someone is buckled to the altar by scanning nearby entities.
        EntityUid? target = null;
        var buckleQuery = EntityQueryEnumerator<BuckleComponent>();
        while (buckleQuery.MoveNext(out var buckledUid, out var buckleComp))
        {
            if (buckleComp.BuckledTo != uid)
                continue;

            if (HasComp<PotentialPsionicComponent>(buckledUid) || HasComp<PsionicComponent>(buckledUid))
            {
                target = buckledUid;
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
        if (!TryComp<BuckleComponent>(target, out var buckleComp) || buckleComp.BuckledTo != altar)
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
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out _))
            return;

        _popup.PopupEntity(
            Loc.GetString("sacrifice-altar-begin", ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
            altar, PopupType.MediumCaution);
    }

    private void OnDoAfter(EntityUid uid, SacrificeAltarComponent component, ref SacrificeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        var target = args.Target ?? uid;
        var user = args.User;

        // Re-validate after the DoAfter delay — psionic check happens HERE.
        if (!TryComp<BuckleComponent>(target, out var buckleComp) || buckleComp.BuckledTo != uid)
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
            Loc.GetString("sacrifice-altar-announce", ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
            uid, PopupType.LargeCaution);

        // Normalize min/max so reversed mapper-configured ranges don't throw.
        var glimMin = Math.Min(component.GlimmerReductionMin, component.GlimmerReductionMax);
        var glimMax = Math.Max(component.GlimmerReductionMin, component.GlimmerReductionMax);
        var glimmerReduction = _random.Next(glimMin, glimMax + 1);
        _glimmer.Glimmer -= glimmerReduction;

        var coords = Transform(target).Coordinates;

        // Spawn bluespace crystals.
        var crMin = Math.Min(component.BsCrystalMin, component.BsCrystalMax);
        var crMax = Math.Max(component.BsCrystalMin, component.BsCrystalMax);
        var crystalCount = _random.Next(crMin, crMax + 1);
        for (var i = 0; i < crystalCount; i++)
        {
            Spawn("MaterialBSCrystal1", coords);
        }

        // Spawn ectoplasm.
        Spawn("Ectoplasm", coords);

        // Log the sacrifice.
        _adminLog.Add(LogType.Psionics,
            LogImpact.High,
            $"{ToPrettyString(user):player} sacrificed {ToPrettyString(target):player} on {ToPrettyString(uid)}, reducing glimmer by {glimmerReduction}");

        // Gib the target.
        _body.GibBody(target, gibOrgans: true);

        // Play a sound.
        _audio.PlayPvs(component.SacrificeSound, uid);
    }
}
