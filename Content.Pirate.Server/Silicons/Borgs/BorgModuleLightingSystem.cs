using Content.Shared.Interaction;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Tools.Systems;
using Content.Pirate.Shared.Silicons.Borgs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Pirate.Server.Silicons.Borgs;

public sealed class BorgModuleLightingSystem : SharedBorgModuleLightingSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgLightingInstalledComponent, InteractUsingEvent>(OnBorgInteractUsing);

        SubscribeLocalEvent<BorgModuleLightingComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<BorgModuleLightingComponent, UpdateBorgModuleLightingMessage>(OnUpdateLighting);

        SubscribeLocalEvent<BorgModuleLightingComponent, BorgModuleInstalledEvent>(OnModuleInstalled);
        SubscribeLocalEvent<BorgModuleLightingComponent, BorgModuleUninstalledEvent>(OnModuleUninstalled);

        SubscribeLocalEvent<BorgModuleLightingComponent, BorgModuleSelectedEvent>(OnModuleSelected);
    }

    private void OnModuleInstalled(Entity<BorgModuleLightingComponent> ent, ref BorgModuleInstalledEvent args)
    {
        var installed = EnsureComp<BorgLightingInstalledComponent>(args.ChassisEnt);
        installed.ModuleEntity = ent.Owner;
        SyncInstalledState(args.ChassisEnt, installed, ent);

        ApplyLighting(args.ChassisEnt, ent);
    }

    private void OnModuleUninstalled(Entity<BorgModuleLightingComponent> ent, ref BorgModuleUninstalledEvent args)
    {
        RemComp<BorgLightingInstalledComponent>(args.ChassisEnt);
        RemoveLighting(args.ChassisEnt);
    }

    private void OnModuleSelected(Entity<BorgModuleLightingComponent> ent, ref BorgModuleSelectedEvent args)
    {
        OpenUi(args.Chassis, ent.Owner);
    }

    private void OnBorgInteractUsing(Entity<BorgLightingInstalledComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tool.HasQuality(args.Used, SharedToolSystem.PulseQuality))
            return;

        if (ent.Comp.ModuleEntity is not {} module || !TryComp<BorgModuleLightingComponent>(module, out var lighting))
            return;

        args.Handled = true;
        OpenUi(args.User, module);
    }

    private void OpenUi(EntityUid user, EntityUid module)
    {
        if (_ui.IsUiOpen(module, BorgModuleLightingUiKey.Key))
            return;

        _ui.OpenUi(module, BorgModuleLightingUiKey.Key, user);
    }

    private void OnUIOpened(Entity<BorgModuleLightingComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUiState(ent);
    }

    private void OnUpdateLighting(Entity<BorgModuleLightingComponent> ent, ref UpdateBorgModuleLightingMessage args)
    {
        ent.Comp.LightColor = args.LightColor;
        ent.Comp.DiscoMode = args.DiscoMode;
        ent.Comp.CycleRate = Math.Clamp(args.CycleRate, 0.01f, 0.5f);
        Dirty(ent.Owner, ent.Comp);

        UpdateUiState(ent);

        if (!TryComp<BorgModuleComponent>(ent, out var module) || module.InstalledEntity is not {} chassis)
            return;

        if (TryComp<BorgLightingInstalledComponent>(chassis, out var installed))
            SyncInstalledState(chassis, installed, ent);

        ApplyLighting(chassis, ent);
    }

    private void SyncInstalledState(EntityUid chassis, BorgLightingInstalledComponent installed, Entity<BorgModuleLightingComponent> module)
    {
        installed.CurrentColor = module.Comp.LightColor;
        installed.DiscoMode = module.Comp.DiscoMode;
        installed.CycleRate = module.Comp.CycleRate;
        Dirty(chassis, installed);
    }

    private void UpdateUiState(Entity<BorgModuleLightingComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, BorgModuleLightingUiKey.Key))
            return;

        var state = new BorgModuleLightingBoundUserInterfaceState(ent.Comp.LightColor, ent.Comp.DiscoMode, ent.Comp.CycleRate);
        _ui.SetUiState(ent.Owner, BorgModuleLightingUiKey.Key, state);
    }
}
