// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.StationEvents.Components;
using Content.Server.Antag;
using Content.Server.Communications;
using Content.Server.StationEvents.Events;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.StationEvents.GameRules;

public sealed class FugitiveRule : StationEventSystem<FugitiveRuleComponent>
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FugitiveRuleComponent, AfterAntagEntitySelectedEvent>(OnEntitySelected);
    }

    protected override void ActiveTick(EntityUid uid, FugitiveRuleComponent comp, GameRuleComponent rule, float frameTime)
    {
        if (comp.NextAnnounce is not { } next || next > Timing.CurTime)
            return;

        var announcement = Loc.GetString(comp.Announcement);
        var sender = Loc.GetString(comp.Sender);
        ChatSystem.DispatchGlobalAnnouncement(announcement, sender: sender, colorOverride: comp.Color);

        var query = EntityQueryEnumerator<TransformComponent, CommunicationsConsoleComponent>();
        var consoles = new List<TransformComponent>();
        while (query.MoveNext(out var console, out var xform, out _))
        {
            if (StationSystem.GetOwningStation(console, xform) != comp.Station ||
                HasComp<GhostComponent>(console) ||
                HasComp<StationAiHeldComponent>(console))
            {
                continue;
            }

            consoles.Add(xform);
        }

        foreach (var xform in consoles)
        {
            SpawnReport(comp, xform);
        }

        comp.NextAnnounce = null;
        RemCompDeferred(uid, comp);
    }

    private void OnEntitySelected(Entity<FugitiveRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var (uid, comp) = ent;
        if (comp.NextAnnounce != null)
        {
            Log.Error("Fugitive rule spawning multiple fugitives is not supported.");
            return;
        }

        var fugitive = args.EntityUid;
        comp.Report = GenerateReport(fugitive, comp).ToMarkup();
        comp.Station = StationSystem.GetOwningStation(fugitive);
        comp.NextAnnounce = Timing.CurTime + comp.AnnounceDelay;

        _popup.PopupEntity(Loc.GetString("fugitive-spawn"), fugitive, fugitive);

        var report = SpawnReport(comp, Transform(fugitive));
        if (_inventory.TryGetSlotEntity(fugitive, "back", out var backpack))
            _storage.Insert(backpack.Value, report, out _, playSound: false);
        else
            _hands.TryPickup(fugitive, report);
    }

    private Entity<PaperComponent> SpawnReport(FugitiveRuleComponent rule, TransformComponent xform)
    {
        var report = Spawn(rule.ReportPaper, xform.Coordinates);
        var paper = Comp<PaperComponent>(report);
        var entity = (report, paper);
        _paper.SetContent(entity, rule.Report);
        return entity;
    }

    private FormattedMessage GenerateReport(EntityUid uid, FugitiveRuleComponent rule)
    {
        var report = new FormattedMessage();
        report.AddMarkupOrThrow(Loc.GetString("fugitive-report-title"));
        report.PushNewline();
        report.PushNewline();
        report.AddMarkupOrThrow(Loc.GetString("fugitive-report-first-line"));
        report.PushNewline();
        report.PushNewline();

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            report.AddMarkupOrThrow(Loc.GetString("fugitive-report-inhuman", ("name", uid)));
            return report;
        }

        var species = PrototypeManager.Index(humanoid.Species);
        report.AddMarkupOrThrow(Loc.GetString("fugitive-report-morphotype", ("species", Loc.GetString(species.Name))));
        report.PushNewline();
        report.AddMarkupOrThrow(Loc.GetString("fugitive-report-age", ("age", humanoid.Age)));
        report.PushNewline();
        report.AddMarkupOrThrow(Loc.GetString("fugitive-report-sex", ("sex", humanoid.Sex.ToString())));
        report.PushNewline();

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            report.AddMarkupOrThrow(Loc.GetString("fugitive-report-weight", ("weight", Math.Round(physics.FixturesMass))));
            report.PushNewline();
        }

        report.AddMarkupOrThrow(RobustRandom.Next(0, 2) switch
        {
            0 => Loc.GetString("fugitive-report-detail-dna", ("dna", GetDna(uid))),
            _ => Loc.GetString("fugitive-report-detail-prints", ("prints", GetPrints(uid))),
        });
        report.PushNewline();
        report.PushNewline();
        report.AddMarkupOrThrow(Loc.GetString("fugitive-report-crimes-header"));
        report.PushNewline();
        AddCharges(report, rule);
        report.PushNewline();
        report.AddMarkupOrThrow(Loc.GetString("fugitive-report-last-line"));

        return report;
    }

    private string GetDna(EntityUid uid)
    {
        return CompOrNull<DnaComponent>(uid)?.DNA ?? "?";
    }

    private string GetPrints(EntityUid uid)
    {
        return CompOrNull<FingerprintComponent>(uid)?.Fingerprint ?? "?";
    }

    private void AddCharges(FormattedMessage report, FugitiveRuleComponent rule)
    {
        var crimeTypes = PrototypeManager.Index(rule.CrimesDataset);
        var crimes = new HashSet<LocId>();
        var total = RobustRandom.Next(rule.MinCrimes, rule.MaxCrimes + 1);
        while (crimes.Count < total)
        {
            crimes.Add(RobustRandom.Pick(crimeTypes));
        }

        foreach (var crime in crimes)
        {
            var count = RobustRandom.Next(rule.MinCounts, rule.MaxCounts + 1);
            report.AddMarkupOrThrow(Loc.GetString("fugitive-report-crime", ("crime", Loc.GetString(crime)), ("count", count)));
            report.PushNewline();
        }
    }
}
