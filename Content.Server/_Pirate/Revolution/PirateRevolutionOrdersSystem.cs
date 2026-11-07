// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Fax;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Revolution;

[RegisterComponent]
public sealed partial class PirateRevolutionOrdersComponent : Component
{
    public TimeSpan NextEvent;
    public int EventsSent;
    public readonly HashSet<string> UsedOrders = new();

    // Minds that already got a directive this round. One directive per person.
    public readonly HashSet<EntityUid> Targeted = new();
}

[RegisterComponent]
public sealed partial class PirateRevolutionSalaryCutsComponent : Component
{
    public readonly Dictionary<string, int> Cuts = new();
}

public enum PirateRevolutionEventKind : byte
{
    Order,
    Salary,
    Tax,
    Termination,
    Passport,
    Crime,
}

public sealed record PirateRevolutionOrder(string Id, string[] Jobs)
{
    public string Key => $"pirate-rev-order-{Id}";

    public string DescriptionKey => $"pirate-rev-order-desc-{Id}";
}

public sealed partial class PirateRevolutionOrdersSystem : GameRuleSystem<PirateRevolutionOrdersComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly StationSystem _stations = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    private static readonly TimeSpan FirstEventDelay = TimeSpan.FromMinutes(10);

    public const int SalaryCutPercent = 40;
    public const int MaxSalaryCut = 80;

    private static readonly string[] Departments = { "Cargo", "Engineering", "Medical", "Security", "Science", "Service" };

    private static readonly string[] AnyJob = Array.Empty<string>();
    private static readonly string[] ScienceJobs = { "Scientist", "ResearchAssistant", "ResearchDirector" };
    private static readonly string[] DoctorJobs = { "MedicalDoctor", "MedicalIntern", "Paramedic", "ChiefMedicalOfficer" };
    private static readonly string[] SurgeonJobs = { "MedicalDoctor", "MedicalIntern", "ChiefMedicalOfficer" };
    private static readonly string[] EngineerJobs = { "StationEngineer", "AtmosphericTechnician", "TechnicalAssistant" };
    private static readonly string[] EngineeringJobs = { "StationEngineer", "AtmosphericTechnician", "TechnicalAssistant", "ChiefEngineer" };
    private static readonly string[] CargoStaffJobs = { "CargoTechnician", "SalvageSpecialist", "ShaftMiner" };

    public static readonly PirateRevolutionOrder[] Orders =
    {
        new("slimes", ScienceJobs),
        new("music", AnyJob),
        new("plushies", AnyJob),
        new("cash", AnyJob),
        new("gloves", AnyJob),
        new("maintenance", AnyJob),
        new("cryo", DoctorJobs),
        new("research", ScienceJobs),
        new("anomalies", ScienceJobs),
        new("salvage", new[] { "SalvageSpecialist", "ShaftMiner" }),
        new("trade", new[] { "CargoTechnician", "Quartermaster" }),
        new("meat", new[] { "Chef" }),
        new("botany", new[] { "Botanist" }),
        new("radio", AnyJob),
        new("eng-modsuits", EngineerJobs),
        new("eng-tools", EngineeringJobs),
        new("eng-radsuits", EngineerJobs),
        new("atmos-canisters", new[] { "AtmosphericTechnician" }),
        new("med-paid", DoctorJobs),
        new("med-sensors", AnyJob),
        new("med-certificates", DoctorJobs),
        new("med-screening", DoctorJobs),
        new("med-surgery", SurgeonJobs),
        new("chem-dispensers", new[] { "Chemist" }),
        new("service-paid-food", new[] { "Chef", "Bartender" }),
        new("service-alcohol-log", new[] { "Bartender" }),
        new("service-taste-test", new[] { "Chef" }),
        new("cargo-approval", new[] { "Quartermaster", "CargoTechnician" }),
        new("cargo-sales-tax", CargoStaffJobs),
    };

    // Orders state for the admin command when no orders rule runs, for example in other game modes.
    private PirateRevolutionOrdersComponent? _manualOrders;

    public override void Initialize()
    {
        base.Initialize();
        InitializeDirectives();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _manualOrders = null);
    }

    protected override void Started(EntityUid uid, PirateRevolutionOrdersComponent comp, GameRuleComponent rule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, rule, args);
        comp.NextEvent = Timing.CurTime + FirstEventDelay;
    }

    protected override void ActiveTick(EntityUid uid, PirateRevolutionOrdersComponent comp, GameRuleComponent rule, float frameTime)
    {
        if (comp.EventsSent >= 15 || Timing.CurTime < comp.NextEvent)
            return;

        comp.EventsSent++;
        var sent = RobustRandom.Next(6) switch
        {
            0 => SendOrder(comp),
            1 => CutSalary(),
            _ => TryIssueDirective(comp, RobustRandom.Pick(Enum.GetValues<PirateRevolutionDirectiveKind>())),
        };

        if (!sent && !SendOrder(comp))
            CutSalary();

        Schedule(comp);
    }

    private void Schedule(PirateRevolutionOrdersComponent comp)
    {
        comp.NextEvent = Timing.CurTime + TimeSpan.FromSeconds(RobustRandom.Next(300, 1801));
    }

    public string? ForceEvent(PirateRevolutionEventKind kind, string? orderId = null)
    {
        var rules = new List<PirateRevolutionOrdersComponent>();
        var query = EntityQueryEnumerator<PirateRevolutionOrdersComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var rule, out _))
            rules.Add(rule);

        var comp = rules.Count > 0 ? rules[0] : _manualOrders ??= new PirateRevolutionOrdersComponent();
        var refusal = kind switch
        {
            PirateRevolutionEventKind.Order when orderId != null => TrySendOrder(comp, orderId),
            PirateRevolutionEventKind.Order => SendOrder(comp) ? null : "no order is left that the crew on the station can get",
            PirateRevolutionEventKind.Salary => CutSalary() ? null : $"no department with crew on the station is under the {MaxSalaryCut}% cap",
            PirateRevolutionEventKind.Tax => Directive(PirateRevolutionDirectiveKind.Tax),
            PirateRevolutionEventKind.Termination => Directive(PirateRevolutionDirectiveKind.Termination),
            PirateRevolutionEventKind.Passport => Directive(PirateRevolutionDirectiveKind.Passport),
            PirateRevolutionEventKind.Crime => Directive(PirateRevolutionDirectiveKind.Crime),
            _ => "unknown event",
        };

        if (refusal == null)
        {
            foreach (var rule in rules)
            {
                Schedule(rule);
            }
        }

        return refusal;

        string? Directive(PirateRevolutionDirectiveKind directive)
        {
            return TryIssueDirective(comp, directive) ? null : "nobody on the station fits this directive";
        }
    }

    public HashSet<string> GetPresentJobs()
    {
        var jobs = new HashSet<string>();
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent>();
        while (query.MoveNext(out var mob, out _, out var mobState))
        {
            if (_mobState.IsAlive(mob, mobState) && _mind.TryGetMind(mob, out var mindId, out _)
                && _job.MindTryGetJobId(mindId, out var job) && job is { } jobId)
            {
                jobs.Add(jobId);
            }
        }

        return jobs;
    }

    public static bool IsEligible(PirateRevolutionOrder order, HashSet<string> presentJobs)
    {
        return order.Jobs.Length == 0 || order.Jobs.Any(presentJobs.Contains);
    }

    private bool SendOrder(PirateRevolutionOrdersComponent comp)
    {
        var jobs = GetPresentJobs();
        var available = new List<PirateRevolutionOrder>();
        foreach (var order in Orders)
        {
            if (!comp.UsedOrders.Contains(order.Id) && IsEligible(order, jobs))
                available.Add(order);
        }

        if (available.Count == 0)
            return false;

        DispatchOrder(comp, RobustRandom.Pick(available));
        return true;
    }

    // An admin may send an order again, but not to a station without its jobs.
    private string? TrySendOrder(PirateRevolutionOrdersComponent comp, string id)
    {
        if (Array.Find(Orders, o => o.Id == id) is not { } order)
            return $"unknown order '{id}'";

        if (!IsEligible(order, GetPresentJobs()))
            return $"order '{id}' needs one of these jobs on the station: {string.Join(", ", order.Jobs)}";

        DispatchOrder(comp, order);
        return null;
    }

    private void DispatchOrder(PirateRevolutionOrdersComponent comp, PirateRevolutionOrder selected)
    {
        comp.UsedOrders.Add(selected.Id);
        var order = Loc.GetString(selected.Key);
        Announce(Loc.GetString("pirate-rev-order-announcement", ("order", order)), Loc.GetString("pirate-rev-order-sender"));
        SendCommandFax(Loc.GetString("pirate-rev-order-fax-name"), Loc.GetString("pirate-rev-order-sender"),
            Loc.GetString("pirate-rev-order-fax", ("order", order)),
            selected.Jobs.Length == 0 ? null : JobDepartments(selected.Jobs));
    }

    private void Announce(string message, string sender)
    {
        _chat.DispatchGlobalAnnouncement(message, sender: sender, colorOverride: Color.Gold);
    }

    private bool CutSalary()
    {
        var stations = new List<EntityUid>();
        var query = EntityQueryEnumerator<StationBankAccountComponent>();
        while (query.MoveNext(out var station, out _))
            stations.Add(station);
        if (stations.Count == 0)
            return false;

        var present = new HashSet<string>();
        foreach (var job in GetPresentJobs())
        {
            if (_job.TryGetAllDepartments(job, out var departments))
                present.UnionWith(departments.Select(department => department.ID));
        }

        var selected = RobustRandom.Pick(stations);
        var cuts = EnsureComp<PirateRevolutionSalaryCutsComponent>(selected);
        var available = Departments
            .Where(department => present.Contains(department)
                && cuts.Cuts.GetValueOrDefault(department) + SalaryCutPercent <= MaxSalaryCut)
            .ToList();
        if (available.Count == 0)
            return false;

        var department = RobustRandom.Pick(available);
        var total = cuts.Cuts.GetValueOrDefault(department) + SalaryCutPercent;
        cuts.Cuts[department] = total;
        var sender = Loc.GetString("pirate-rev-order-sender");
        var message = Loc.GetString("pirate-rev-salary-cut", ("department", Loc.GetString($"department-{department}")),
            ("percent", SalaryCutPercent), ("total", total));
        Announce(message, sender);
        SendCommandFax(Loc.GetString("pirate-rev-order-fax-name"), sender, message, new[] { department });
        return true;
    }
}
