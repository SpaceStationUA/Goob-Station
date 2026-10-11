using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Pirate.Server.Botany.Components;
using Content.Pirate.Shared.Botany.PlantAnalyzer;
using Content.Server.Paper;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Localization;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;

namespace Content.Pirate.Server.Botany.Systems;

public sealed partial class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    // Pirate: retain printed reports alongside the Omu scan state.
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly LabelSystem _labels = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PlantAnalyzerComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<PlantAnalyzerComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerPrintMessage>(OnPrint);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PlantAnalyzerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.ScannedEntity is not { } target)
                continue;

            if (!TryComp<PlantHolderComponent>(target, out _))
                continue;

            if (!_ui.IsUiOpen(uid, PlantAnalyzerUiKey.Key))
            {
                StopUsingAnalyzer(uid, comp);
                continue;
            }

            if (comp.NextUpdate > _timing.CurTime)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.UpdateInterval;

            if (Deleted(target) || !IsValidTarget(target))
            {
                StopUsingAnalyzer(uid, comp);
                continue;
            }

            if (!_cell.HasDrawCharge(uid))
            {
                StopUsingAnalyzer(uid, comp);
                continue;
            }

            if (!TryComp(target, out TransformComponent? targetXform))
            {
                StopUsingAnalyzer(uid, comp);
                continue;
            }

            if (!_transform.InRange(xform.Coordinates, targetXform.Coordinates, comp.Settings.MaxScanRange))
            {
                if (comp.OutOfRangeSince == null)
                {
                    comp.OutOfRangeSince = _timing.CurTime;
                }
                else if (_timing.CurTime - comp.OutOfRangeSince.Value >= TimeSpan.FromSeconds(5.0))
                {
                    comp.ScannedEntity = null;
                    comp.OutOfRangeSince = null;
                    SendNoDataState(uid, comp);
                    continue;
                }

                SendOutOfRangeState(uid, comp);
                continue;
            }

            comp.OutOfRangeSince = null;
            SendScannedState(uid, comp, target);
        }
    }

    private void OnAfterInteract(Entity<PlantAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach)
            return;

        if (!IsValidTarget(target))
            return;

        if (!_cell.HasDrawCharge(ent.Owner, user: args.User))
            return;

        var scanDelay = ent.Comp.Settings.ScanDelay;

        // If scan delay is zero or negative, perform the scan instantly instead of starting a DoAfter
        if (scanDelay <= 0f)
        {
            if (!IsValidTarget(target))
                return;

            ent.Comp.ScannedEntity = target;
            ent.Comp.NextUpdate = TimeSpan.Zero;

            _audio.PlayPvs(ent.Comp.ScanningEndSound, ent.Owner);
            SendScannedState(ent.Owner, ent.Comp, target);
            _ui.TryOpenUi(ent.Owner, PlantAnalyzerUiKey.Key, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            scanDelay,
            new PlantAnalyzerDoAfterEvent(),
            ent,
            target: target,
            used: ent)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 0.01f,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!IsValidTarget(target))
            return;

        if (!_cell.HasDrawCharge(ent.Owner, user: args.User))
            return;

        ent.Comp.ScannedEntity = target;
        ent.Comp.NextUpdate = TimeSpan.Zero;
        ent.Comp.OutOfRangeSince = null;

        _audio.PlayPvs(ent.Comp.ScanningEndSound, ent.Owner);
        SendScannedState(ent.Owner, ent.Comp, target);
        if (_cell.HasDrawCharge(ent.Owner))
        {
            _ui.TryOpenUi(ent.Owner, PlantAnalyzerUiKey.Key, args.User);
        }
        else
        {
            StopUsingAnalyzer(ent.Owner, ent.Comp);
        }

        args.Handled = true;
    }

    private bool IsValidTarget(EntityUid target)
    {
        // Pirate: empty trays still expose water, nutrients, pests and soil chemistry.
        return HasComp<PlantHolderComponent>(target) || TryGetSeedData(target, out _, out _);
    }

    private void OnDropped(Entity<PlantAnalyzerComponent> ent, ref DroppedEvent args)
    {
        StopUsingAnalyzer(ent);
    }

    private void StopUsingAnalyzer(Entity<PlantAnalyzerComponent> ent)
    {
        StopUsingAnalyzer(ent.Owner, ent.Comp);
    }

    private void StopUsingAnalyzer(EntityUid uid, PlantAnalyzerComponent comp)
    {
        comp.ScannedEntity = null;
        comp.OutOfRangeSince = null;
        _ui.CloseUi(uid, PlantAnalyzerUiKey.Key);
    }

    private void OnUiClosed(EntityUid uid, PlantAnalyzerComponent comp, BoundUIClosedEvent args)
    {
        if (!args.UiKey.Equals(PlantAnalyzerUiKey.Key))
            return;

        if (comp.ScannedEntity == null)
            return;

        comp.ScannedEntity = null;
        comp.OutOfRangeSince = null;
        SendNoDataState(uid, comp);
    }

    private void SendOutOfRangeState(EntityUid uid, PlantAnalyzerComponent comp)
    {
        _ui.SetUiState(
            uid,
            PlantAnalyzerUiKey.Key,
            new PlantAnalyzerScannedState(
                status: PlantAnalyzerStatus.OutOfRange,
                name: null,
                scanType: PlantAnalyzerScanType.None,
                entity: null,
                health: null,
                maxHealth: null,
                water: null,
                nutrition: null,
                dead: false));
    }

    private void SendNoDataState(EntityUid uid, PlantAnalyzerComponent comp)
    {
        _ui.SetUiState(
            uid,
            PlantAnalyzerUiKey.Key,
            new PlantAnalyzerScannedState(
                status: PlantAnalyzerStatus.NoData,
                name: null,
                scanType: PlantAnalyzerScanType.None,
                entity: null,
                health: null,
                maxHealth: null,
                water: null,
                nutrition: null,
                dead: false));
    }

    private bool TryGetSeedData(EntityUid target, [NotNullWhen(true)] out SeedData? seed, out PlantAnalyzerScanType scanType)
    {
        seed = null;
        scanType = PlantAnalyzerScanType.None;

        if (TryComp<PlantHolderComponent>(target, out var plantHolder) && plantHolder.Seed != null)
        {
            seed = plantHolder.Seed;
            scanType = PlantAnalyzerScanType.Plant;
            return true;
        }

        if (TryComp<ProduceComponent>(target, out var produceComp) && produceComp.Seed != null)
        {
            seed = produceComp.Seed;
            scanType = PlantAnalyzerScanType.Produce;
            return true;
        }

        if (TryComp<SeedComponent>(target, out var seedComp) && _botany.TryGetSeed(seedComp, out seed))
        {
            scanType = PlantAnalyzerScanType.Seed;
            return true;
        }

        return false;
    }

    private IEnumerable<PlantReagentEntry> BuildReagentEntries(SeedData seed, bool inherent)
    {
        if (seed.Chemicals == null)
            yield break;

        foreach (var (id, chem) in seed.Chemicals)
        {
            if (chem.Inherent != inherent)
                continue;

            var name = id;
            var colorHex = "#FFFFFF";

            if (_prototype.TryIndex<ReagentPrototype>(id, out var reagentProto))
            {
                name = reagentProto.LocalizedName;
                colorHex = reagentProto.SubstanceColor.ToHex();
            }

            float amount = (float) chem.Min;
            if (chem.PotencyDivisor > 0)
            {
                amount += seed.Potency / chem.PotencyDivisor;
            }

            amount = MathF.Min(amount, (float) chem.Max);
            if (amount < chem.Min)
                amount = (float) chem.Min;

            yield return new PlantReagentEntry(id, name, colorHex, amount)
            {
                DisplayAmount = amount.ToString("0.##")
            };
        }
    }

    private IEnumerable<PlantReagentEntry> BuildGasEntries(Dictionary<Content.Shared.Atmos.Gas, float> gases)
    {
        foreach (var (gas, amount) in gases)
        {
            if (amount <= 0f)
                continue;

            var gasId = ((int)gas).ToString();
            var colorHex = "#FFFFFF";
            var gasName = gas.ToString();

            if (_prototype.TryIndex<GasPrototype>(gasId, out var gasProto))
            {
                gasName = Loc.GetString(gasProto.Name);
                colorHex = gasProto.Color;
                if (!colorHex.StartsWith("#"))
                    colorHex = "#" + colorHex;
            }

            yield return new PlantReagentEntry(gasId, gasName, colorHex, amount)
            {
                DisplayAmount = amount.ToString("0.##")
            };
        }
    }

    private List<string>? BuildMissingGasesList(EntityUid target, SeedData seed, PlantHolderComponent? plantHolder)
    {
        if (plantHolder == null || seed?.ConsumeGasses == null || seed.ConsumeGasses.Count == 0)
            return null;

        var missingGases = new List<string>();

        // Try to get the atmosphere
        var air = _atmosphere.GetContainingMixture(target, true, true) ?? GasMixture.SpaceGas;

        foreach (var (gasId, requiredAmount) in seed.ConsumeGasses)
        {
            var gasAmount = air.GetMoles(gasId);
            if (gasAmount < requiredAmount)
            {
                var gasProtoId = ((int)gasId).ToString();
                var gasName = gasId.ToString();

                if (_prototype.TryIndex<GasPrototype>(gasProtoId, out var gasProto))
                {
                    gasName = Loc.GetString(gasProto.Name);
                }

                missingGases.Add(gasName);
            }
        }

        return missingGases.Count > 0 ? missingGases : null;
    }

    private string? BuildWarningsText(PlantHolderComponent? component)
    {
        if (component == null)
            return null;

        var warnings = new List<string>();

        if (component.WaterLevel < 20f)
            warnings.Add(Loc.GetString("plant-analyzer-warning-low-water"));

        if (component.Toxins > 40f)
            warnings.Add(Loc.GetString("plant-analyzer-warning-high-toxin"));

        if (component.PestLevel >= 5f)
            warnings.Add(Loc.GetString("plant-analyzer-warning-pest"));

        if (component.WeedLevel >= 5f)
            warnings.Add(Loc.GetString("plant-analyzer-warning-weeds"));

        if (component.ImproperHeat)
            warnings.Add(Loc.GetString("plant-analyzer-warning-improper-temperature"));

        if (component.ImproperPressure)
            warnings.Add(Loc.GetString("plant-analyzer-warning-improper-pressure"));

        if (component.NutritionLevel < 20f)
            warnings.Add(Loc.GetString("plant-analyzer-warning-low-nutrition"));

        if (component.Dead)
            warnings.Add(Loc.GetString("plant-analyzer-warning-dead"));

        if (component.Seed != null && component.Health < component.Seed.Endurance * 0.25f)
            warnings.Add(Loc.GetString("plant-analyzer-warning-low-health"));

        if (component.MissingGas > 0)
            warnings.Add(Loc.GetString("plant-analyzer-warning-missing-gas"));

        return warnings.Count > 0 ? string.Join("\n", warnings.Select(w => $"⚠ {w}")) : null;
    }

    private string? BuildResistancesText(SeedData seed)
    {
        var lines = new List<string>
        {
            $"{Loc.GetString("plant-analyzer-resist-pest")}: {seed.PestTolerance:#0.##}",
            $"{Loc.GetString("plant-analyzer-resist-toxin")}: {seed.ToxinsTolerance:#0.##}",
            $"{Loc.GetString("plant-analyzer-resist-weed")}: {seed.WeedTolerance:#0.##}"
        };

        return string.Join("\n", lines);
    }

    private List<string>? BuildMutationTargets(SeedData seed)
    {
        if (seed.MutationPrototypes == null || seed.MutationPrototypes.Count == 0)
            return null;

        var targets = new List<string>();
        foreach (var targetId in seed.MutationPrototypes)
        {
            if (_prototype.TryIndex(targetId, out SeedPrototype? prototype))
            {
                targets.Add(Loc.GetString(prototype.DisplayName));
            }
            else
            {
                targets.Add(targetId.ToString());
            }
        }

        return targets.Count > 0 ? targets : null;
    }

    private string? BuildAllTraitsText(SeedData seed)
    {
        var traits = new List<string>();

        // Seedless
        if (seed.Seedless)
            traits.Add($"• {Loc.GetString("plant-analyzer-trait-seedless")}\n[font size=10][color=#666666]{Loc.GetString("plant-analyzer-trait-seedless-desc")}[/color][/font]");

        // Ligneous
        if (seed.Ligneous)
            traits.Add($"• {Loc.GetString("plant-analyzer-trait-ligneous")}\n[font size=10][color=#666666]{Loc.GetString("plant-analyzer-trait-ligneous-desc")}[/color][/font]");

        // Non-viable (only show if not viable)
        if (!seed.Viable)
            traits.Add($"• {Loc.GetString("plant-analyzer-trait-non-viable")}\n[font size=10][color=#666666]{Loc.GetString("plant-analyzer-trait-non-viable-desc")}[/color][/font]");

        // Slippery
        if (seed.Mutations != null && seed.Mutations.Any(m => m.Name == "Slippery"))
            traits.Add($"• {Loc.GetString("plant-analyzer-trait-slippery")}\n[font size=10][color=#666666]{Loc.GetString("plant-analyzer-trait-slippery-desc")}[/color][/font]");

        // CanScream
        if (seed.CanScream)
            traits.Add($"• {Loc.GetString("plant-analyzer-trait-scream")}\n[font size=10][color=#666666]{Loc.GetString("plant-analyzer-trait-scream-desc")}[/color][/font]");

        // TurnIntoKudzu
        if (seed.TurnIntoKudzu)
            traits.Add($"• {Loc.GetString("plant-analyzer-trait-kudzu")}\n[font size=10][color=#666666]{Loc.GetString("plant-analyzer-trait-kudzu-desc")}[/color][/font]");

        // Auto harvest
        if (seed.HarvestRepeat == HarvestType.SelfHarvest)
            traits.Add($"• {Loc.GetString("plant-analyzer-trait-auto-harvest")}\n[font size=10][color=#666666]{Loc.GetString("plant-analyzer-trait-auto-harvest-desc")}[/color][/font]");

        // Check mutations for Sentient
        if (seed.Mutations != null && seed.Mutations.Count > 0)
        {
            foreach (var mutation in seed.Mutations)
            {
                if (mutation.Name == "Sentient")
                    traits.Add($"• {Loc.GetString("plant-analyzer-trait-sentient")}\n[font size=10][color=#666666]{Loc.GetString("plant-analyzer-trait-sentient-desc")}[/color][/font]");
            }
        }

        return traits.Count > 0 ? string.Join("\n", traits) : Loc.GetString("plant-analyzer-traits-none");
    }

    private string BuildName(SeedData seed, PlantAnalyzerScanType scanType)
    {
        var name = scanType switch
        {
            PlantAnalyzerScanType.Seed => Loc.GetString("plant-analyzer-scanned-seed-name", ("name", Loc.GetString(seed.Name))),
            PlantAnalyzerScanType.Plant => Loc.GetString(seed.DisplayName),
            PlantAnalyzerScanType.Produce => Loc.GetString(seed.Name),
            _ => Loc.GetString(seed.DisplayName)
        };
        return name;
    }

    private void SendScannedState(EntityUid uid, PlantAnalyzerComponent comp, EntityUid target)
    {
        if (!_cell.HasDrawCharge(uid))
        {
            StopUsingAnalyzer(uid, comp);
            return;
        }

        var state = GatherScannedState(comp, target);
        if (state != null)
            _ui.SetUiState(uid, PlantAnalyzerUiKey.Key, state);
    }

    private PlantAnalyzerScannedState? GatherScannedState(PlantAnalyzerComponent comp, EntityUid target)
    {
        TryGetSeedData(target, out var seed, out var scanType);
        TryComp<PlantHolderComponent>(target, out var plantHolder);
        if (seed == null && plantHolder == null)
            return null;

        if (plantHolder != null)
            scanType = PlantAnalyzerScanType.Plant;

        var name = seed != null ? BuildName(seed, scanType) : Loc.GetString("plant-analyzer-component-no-seed");
        var maxHealth = seed?.Endurance;

        // Pirate: use the same calculation as harvesting, including zero and negative modifiers.
        int? yield = seed == null ? null : seed.ProductPrototypes.Count == 0
            ? 0
            : BotanySystem.CalculateTotalYield(seed.Yield, plantHolder?.YieldMod ?? 1);

        var mutationTargetsList = seed != null ? BuildMutationTargets(seed) : null;
        var resistancesTextLocal = seed != null ? BuildResistancesText(seed) : null;
        var allTraitsTextLocal = seed != null ? BuildAllTraitsText(seed) : null;
        var inherentReagentsList = seed != null ? BuildReagentEntries(seed, true).ToList() : new List<PlantReagentEntry>();
        var mutatedReagentsList = seed != null ? BuildReagentEntries(seed, false).ToList() : new List<PlantReagentEntry>();
        var consumedGasesList = seed != null ? BuildGasEntries(seed.ConsumeGasses).ToList() : new List<PlantReagentEntry>();
        var exudedGasesList = seed != null ? BuildGasEntries(seed.ExudeGasses).ToList() : new List<PlantReagentEntry>();
        var potencyLocal = seed != null ? seed.Potency : 0f;
        var missingGasesList = seed != null ? BuildMissingGasesList(target, seed, plantHolder) : null;

        return new PlantAnalyzerScannedState(
                PlantAnalyzerStatus.Active,
                name,
                scanType,
                GetNetEntity(target),
                plantHolder?.Health,
                maxHealth,
                plantHolder?.WaterLevel,
                plantHolder?.NutritionLevel,
                plantHolder?.Dead ?? false,
                toxinLevel: plantHolder?.Toxins,
                weedLevel: plantHolder?.WeedLevel,
                pestLevel: plantHolder?.PestLevel,
                yield: yield,
                mutationTargets: mutationTargetsList,
                warningsText: BuildWarningsText(plantHolder),
                resistancesText: resistancesTextLocal,
                allTraitsText: allTraitsTextLocal,
                inherentReagents: inherentReagentsList,
                mutatedReagents: mutatedReagentsList,
                consumedGases: consumedGasesList,
                exudedGases: exudedGasesList,
                potency: potencyLocal,
                maturation: seed?.Maturation,
                production: seed?.Production,
                pestResistance: seed?.PestTolerance,
                toxinResistance: seed?.ToxinsTolerance,
                weedResistance: seed?.WeedTolerance,
                harvestReady: plantHolder?.Harvest ?? false,
                idealTemperature: seed?.IdealHeat,
                temperatureTolerance: seed?.HeatTolerance,
                lowPressureTolerance: seed?.LowPressureTolerance,
                highPressureTolerance: seed?.HighPressureTolerance,
                missingGases: missingGasesList)
        {
            // Pirate: preserve tray details, growth requirements and printable genetics.
            SeedDisplayName = seed?.DisplayName,
            Age = plantHolder?.Seed == null ? null : plantHolder.Age,
            Lifespan = seed?.Lifespan,
            WaterConsumption = seed?.WaterConsumption,
            NutrientConsumption = seed?.NutrientConsumption,
            IdealLight = seed?.IdealLight,
            LightTolerance = seed?.LightTolerance,
            Products = seed?.ProductPrototypes.Select(id => id.ToString()).ToList(),
            SoilReagents = BuildSoilReagents(plantHolder),
            Mutating = plantHolder?.MutationLevel > 0,
            PrintReadyAt = comp.PrintReadyAt,
        };
    }

    private List<PlantReagentEntry>? BuildSoilReagents(PlantHolderComponent? holder)
    {
        if (holder?.SoilSolution is not { } soil)
            return null;

        var entries = new List<PlantReagentEntry>();
        foreach (var reagent in soil.Comp.Solution.Contents)
        {
            var id = reagent.Reagent.Prototype.Id;
            var name = id;
            var color = "#FFFFFF";
            if (_prototype.TryIndex<ReagentPrototype>(id, out var prototype))
            {
                name = prototype.LocalizedName;
                color = prototype.SubstanceColor.ToHex();
            }

            entries.Add(new PlantReagentEntry(id, name, color, (float) reagent.Quantity));
        }
        return entries;
    }

    private void OnPrint(EntityUid uid, PlantAnalyzerComponent comp, PlantAnalyzerPrintMessage args)
    {
        if (_timing.CurTime < comp.PrintReadyAt)
        {
            _popup.PopupEntity(Loc.GetString("plant-analyzer-printer-not-ready"), uid, args.Actor);
            return;
        }

        if (comp.ScannedEntity is not { } target)
            return;

        if (Deleted(target) || !_cell.HasDrawCharge(uid))
            return;

        if (!TryGetSeedData(target, out var seed, out _))
            return;

        var state = GatherScannedState(comp, target);
        if (state == null)
            return;

        var printed = Spawn(comp.MachineOutput, Transform(uid).Coordinates);
        _hands.PickupOrDrop(args.Actor, printed, checkActionBlocker: false);
        if (!TryComp<PaperComponent>(printed, out var paper))
            return;

        var seedName = Loc.GetString(seed.DisplayName);
        (string, object)[] parameters = [
            ("seedName", seedName),
            ("produce", PlantAnalyzerLocalizationHelper.ProduceToLocalizedStrings(seed.ProductPrototypes.Select(id => id.ToString()).ToList(), _prototype).Plural),
            ("water", seed.WaterConsumption.ToString("0.00")),
            ("nutrients", seed.NutrientConsumption.ToString("0.00")),
            ("toxins", seed.ToxinsTolerance.ToString("0.00")),
            ("pests", seed.PestTolerance.ToString("0.00")),
            ("weeds", seed.WeedTolerance.ToString("0.00")),
            ("gasesIn", PlantAnalyzerLocalizationHelper.GasesToLocalizedStrings(seed.ConsumeGasses.Keys.ToList(), _atmosphere)),
            ("kpa", ((seed.LowPressureTolerance + seed.HighPressureTolerance) / 2).ToString("0.00")),
            ("kpaTolerance", ((seed.HighPressureTolerance - seed.LowPressureTolerance) / 2).ToString("0.00")),
            ("temp", seed.IdealHeat.ToString("0.00")),
            ("tempTolerance", seed.HeatTolerance.ToString("0.00")),
            ("lightLevel", seed.IdealLight.ToString("0.00")),
            ("lightTolerance", seed.LightTolerance.ToString("0.00")),
            ("yield", state.Yield ?? -1),
            ("potency", seed.Potency.ToString("0.##")),
            ("chemicals", PlantAnalyzerLocalizationHelper.ChemicalsToLocalizedStrings(seed.Chemicals.Keys.ToList(), _prototype)),
            ("gasesOut", PlantAnalyzerLocalizationHelper.GasesToLocalizedStrings(seed.ExudeGasses.Keys.ToList(), _atmosphere)),
            ("endurance", seed.Endurance.ToString("0.00")),
            ("lifespan", seed.Lifespan.ToString("0.00")),
            ("seeds", seed.Seedless ? "no" : "yes"),
            ("viable", seed.Viable ? "yes" : "no"),
            ("kudzu", seed.TurnIntoKudzu ? "yes" : "no"),
            ("indent", "    "),
            ("nl", "\n"),
        ];

        _paper.SetContent(printed, Loc.GetString("plant-analyzer-printout", parameters));
        _labels.Label(printed, seedName);
        _audio.PlayPvs(comp.SoundPrint, uid, AudioParams.Default
            .WithVariation(0.25f)
            .WithVolume(3f)
            .WithRolloffFactor(2.8f)
            .WithMaxDistance(4.5f));

        comp.PrintReadyAt = _timing.CurTime + comp.PrintCooldown;
        SendScannedState(uid, comp, target);
    }
}
