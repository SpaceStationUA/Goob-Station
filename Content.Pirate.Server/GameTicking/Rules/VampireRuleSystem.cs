using Content.Server.Antag;
using Content.Server.Atmos.Components;
using Content.Server.Body.Systems;
using Content.Server.Body.Components;
using Content.Server._Pirate.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Pirate.Server.Roles;
using Content.Server.Roles;
using Content.Pirate.Server.Vampire;
using Content.Goobstation.Shared.Religion;
using Content.Goobstation.Shared.Overlays;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Pirate.Shared.Vampire.Components;
using Content.Pirate.Server.Vampirism.Components;
using Content.Goobstation.Common.Religion;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Mind;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Nutrition.Components;
using Content.Shared.Chemistry.Reaction;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using System.Text;

namespace Content.Pirate.Server.GameTicking.Rules;

public sealed partial class VampireRuleSystem : GameRuleSystem<VampireRuleComponent>
{
    [Dependency] private readonly VampireHelpers _vHelper = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly ObjectivesSystem _objective = default!;
    [Dependency] private readonly VampireSystem _vampire = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly BodySystem _body = default!;

    public readonly SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/_Pirate/Ambience/Antag/vampire_start.ogg");

    [ValidatePrototypeId<EntityPrototype>]
    private const string MindRole = "MindRoleVampire";

    public readonly ProtoId<AntagPrototype> VampirePrototypeId = "Vampire";

    public readonly ProtoId<NpcFactionPrototype> ChangelingFactionId = "Vampire";

    public readonly ProtoId<NpcFactionPrototype> NanotrasenFactionId = "NanoTrasen";

    public readonly ProtoId<CurrencyPrototype> Currency = "BloodEssence";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireRuleComponent, GetBriefingEvent>(OnGetBriefing);

        SubscribeLocalEvent<VampireRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
        SubscribeLocalEvent<VampireRuleComponent, ObjectivesTextPrependEvent>(OnTextPrepend);
        SubscribeLocalEvent<VampireComponent, ComponentShutdown>(OnVampireRemoved);
    }

    private void OnSelectAntag(EntityUid mindId, VampireRuleComponent comp, ref AfterAntagEntitySelectedEvent args)
    {
        var ent = args.EntityUid;

        _antag.SendBriefing(ent, MakeBriefing(ent), Color.Yellow, BriefingSound);
        MakeVampire(ent, comp);
    }
    public bool MakeVampire(EntityUid target, VampireRuleComponent rule)
    {
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        _role.MindAddRole(mindId, MindRole, mind, true);

        // briefing
        if (TryComp<MetaDataComponent>(target, out var metaData))
        {
            var briefing = Loc.GetString("vampire-role-greeting", ("name", metaData?.EntityName ?? "Unknown"));
            var briefingShort = Loc.GetString("vampire-role-greeting-short", ("name", metaData?.EntityName ?? "Unknown"));

            _antag.SendBriefing(target, briefing, Color.Red, BriefingSound);

            if (_role.MindHasRole<VampireRoleComponent>(mindId, out var mr))
                AddComp(mr.Value, new RoleBriefingComponent { Briefing = briefingShort }, overwrite: true);
        }
        // vampire stuff
        _npcFaction.RemoveFaction(target, NanotrasenFactionId, false);
        _npcFaction.AddFaction(target, ChangelingFactionId);

        // Remove any existing cure marker so the cure can be triggered again later if needed.
        RemComp<VampireCureComponent>(target);

        // make sure it's initial chems are set to max
        var vampireComponent = EnsureComp<VampireComponent>(target);
        EnsureComp<VampireIconComponent>(target);
        EnsureComp<VampireSpaceDamageComponent>(target);
        var vampireAlertComponent = EnsureComp<VampireAlertComponent>(target);
        var interfaceComponent = EnsureComp<UserInterfaceComponent>(target);

        if (HasComp<UserInterfaceComponent>(target))
            _uiSystem.SetUiState(target, VampireMutationUiKey.Key, new VampireMutationBoundUserInterfaceState(vampireComponent.VampireMutations, vampireComponent.CurrentMutation));

        // Track whether this entity already had pressure immunity before becoming a vampire,
        // so we can restore its prior state when curing vampirism.
        vampireComponent.HadPressureImmunityComponent = HasComp<PressureImmunityComponent>(target);
        EnsureComp<PressureImmunityComponent>(target);

        var vampire = new Entity<VampireComponent>(target, vampireComponent);

        RemComp<PerishableComponent>(vampire);
        RemComp<ThirstComponent>(vampire);

        vampireComponent.Balance = new() { { VampireComponent.CurrencyProto, 0 } };

        rule.VampireMinds.Add(mindId);

        EnsureComp<BloodSuckerComponent>(vampire);
        EnsureComp<WeakToHolyComponent>(vampire).AlwaysTakeHoly = true;
        _vampire.AddStartingAbilities(vampire);
        _vampire.MakeVulnerableToHoly(vampire);
        _alerts.ShowAlert(vampire, vampireAlertComponent.BloodAlert);
        _alerts.ShowAlert(vampire, vampireAlertComponent.StellarWeaknessAlert);

        Random random = new Random();

        foreach (var objective in rule.BaseObjectives)
            _mind.TryAddObjective(mindId, mind, objective);

        if (rule.EscapeObjectives.Count > 0)
        {
            var randomEscapeObjective = rule.EscapeObjectives[random.Next(rule.EscapeObjectives.Count)];
            _mind.TryAddObjective(mindId, mind, randomEscapeObjective);
        }

        if (rule.StealObjectives.Count > 0)
        {
            var randomEscapeObjective = rule.StealObjectives[random.Next(rule.StealObjectives.Count)];
            _mind.TryAddObjective(mindId, mind, randomEscapeObjective);
        }

        return true;
    }

    private void OnGetBriefing(Entity<VampireRuleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent == null
            || HasComp<BibleUserComponent>(ent)
            || !TryComp<BodyComponent>(ent, out var body)
            || _vHelper.TryGetBodyOrganEntityComps<StomachComponent>((ent.Value, body), out var stomachs))
            return;

        args.Append(MakeBriefing(ent.Value));
    }

    private string MakeBriefing(EntityUid ent)
    {
        if (TryComp<MetaDataComponent>(ent, out var metaData))
        {
            var briefing = Loc.GetString("vampire-role-greeting", ("name", metaData?.EntityName ?? "Unknown"));

            return briefing;
        }

        return "";
    }

    /// <summary>
    /// Handles cleanup when an entity stops being an antag vampire
    /// (e.g. cured by holy water).
    /// </summary>
    private void OnVampireRemoved(Entity<VampireComponent> ent, ref ComponentShutdown args)
    {
        var uid = ent.Owner;

        if (!TryComp<MetaDataComponent>(uid, out var meta) ||
            meta.EntityLifeStage >= EntityLifeStage.Terminating)
        {
            return;
        }

        // Clean up any vampire actions so they don't persist or duplicate on re-vamp.
        _vampire.CleanupVampireActions(uid, ent.Comp);

        // Restore factions back to NanoTrasen default.
        _npcFaction.RemoveFaction(uid, ChangelingFactionId, false);
        _npcFaction.AddFaction(uid, NanotrasenFactionId);

        // Clear vampire-specific alerts.
        if (TryComp<VampireAlertComponent>(uid, out var alertComp))
        {
            _alerts.ClearAlert(uid, alertComp.BloodAlert);
            _alerts.ClearAlert(uid, alertComp.StellarWeaknessAlert);
        }

        // Remove vampire-only components.
        RemComp<VampireIconComponent>(uid);
        RemComp<VampireSpaceDamageComponent>(uid);
        RemComp<VampireFangsExtendedComponent>(uid);
        RemComp<VampireHealingComponent>(uid);
        RemComp<VampireDeathsEmbraceComponent>(uid);
        RemComp<VampireSealthComponent>(uid);
        RemComp<VampireStrengthComponent>(uid);
        RemComp<BloodSuckerComponent>(uid);
        RemComp<VampireAlertComponent>(uid);

        // Remove unholy/holy-weakness markers added when becoming a vampire.
        RemComp<WeakToHolyComponent>(uid);
        if (TryComp<ReactiveComponent>(uid, out var reactive) && reactive.ReactiveGroups != null)
            reactive.ReactiveGroups.Remove("WeakToHoly");

        // Remove vision overlays granted to antag vampires.
        RemComp<NightVisionComponent>(uid);
        RemComp<ThermalVisionComponent>(uid);

        // Restore basic biological limitations; if the entity previously lacked these
        // (e.g. synthetic species), EnsureComp will be harmless.
        EnsureComp<PerishableComponent>(uid);
        EnsureComp<ThirstComponent>(uid);

        // Remove pressure immunity only if it was granted by the vampire role.
        if (!ent.Comp.HadPressureImmunityComponent)
            RemComp<PressureImmunityComponent>(uid);

        // NOTE: Vampire actions are tied to the removed component and will no longer
        // function without it. We intentionally leave any orphaned action entities
        // to avoid hard-coding their removal here.

        // Remove antag roles and objectives from the mind.
        if (_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            _role.MindRemoveRole<VampireRoleComponent>(mindId);
            _mind.ClearObjectives(mindId, mind);

            // Best-effort: if a vampire game rule is active, forget this mind.
            var ruleEnt = _antag.ForceGetGameRuleEnt<VampireRuleComponent>("Vampire");
            if (TryComp(ruleEnt, out VampireRuleComponent? rule))
                rule.VampireMinds.Remove(mindId);
        }
    }

    private void OnTextPrepend(EntityUid uid, VampireRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        var mostDrainedName = string.Empty;
        var mostDrained = 0f;

        foreach (var vamp in EntityQuery<VampireComponent>())
        {
            if (!_mind.TryGetMind(vamp.Owner, out var mindId, out var mind))
                continue;

            if (!TryComp<MetaDataComponent>(vamp.Owner, out var metaData))
                continue;

            if (vamp.TotalBloodDrank > mostDrained)
            {
                mostDrained = vamp.TotalBloodDrank;
                mostDrainedName = _objective.GetTitle((mindId, mind), metaData.EntityName);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString($"roundend-prepend-vampire-drained{(!string.IsNullOrWhiteSpace(mostDrainedName) ? "-named" : "")}", ("name", mostDrainedName), ("number", mostDrained)));

        args.Text = sb.ToString();
    }
}
