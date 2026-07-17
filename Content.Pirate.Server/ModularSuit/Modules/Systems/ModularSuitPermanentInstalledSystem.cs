using Content.Pirate.Shared.ModularSuit;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitPermanentInstalledSystem : EntitySystem
{
    [Dependency] private SharedModularSuitSystem _modularSuit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularSuitPermanentInstalledComponent, ModularSuitInstalledEvent>(OnModuleInstalled);
    }

    private void OnModuleInstalled(Entity<ModularSuitPermanentInstalledComponent> module, ref ModularSuitInstalledEvent args)
    {
        _modularSuit.SetModulePermanent(module.Owner, true);
    }
}
