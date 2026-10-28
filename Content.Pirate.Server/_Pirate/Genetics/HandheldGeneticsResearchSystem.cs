// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Genetics.Console;
using Content.Server.Research.Systems;
using Content.Shared.Interaction;
using Content.Shared.Research.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.Genetics;

public sealed class HandheldGeneticsResearchSystem : EntitySystem
{
    [Dependency] private readonly ResearchSystem _research = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchClientComponent, AfterInteractEvent>(OnInteract,
            before: [typeof(GeneticsConsoleSystem)]);
        SubscribeLocalEvent<ResearchClientComponent, AfterActivatableUIOpenEvent>(OnUiOpened,
            before: [typeof(GeneticsConsoleSystem)]);
    }

    private void OnInteract(Entity<ResearchClientComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.CanReach)
            TryConnect(ent);
    }

    private void OnUiOpened(Entity<ResearchClientComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        TryConnect(ent);
    }

    private void TryConnect(Entity<ResearchClientComponent> scanner)
    {
        // Keep an existing server binding when the scanner moves to another grid.
        if (!HasComp<HandheldGeneticsScannerComponent>(scanner) || scanner.Comp.Server != null)
            return;

        if (_research.GetServers(scanner).FirstOrNull() is { } server)
            _research.RegisterClient(scanner, server, scanner.Comp, server);
    }
}
