using Content.Shared._Pirate.Coffeemaker.Components;
using Content.Shared.Fluids.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server._Pirate.Coffeemaker.EntitySystems;

/// <summary>
/// Keeps throw-spill behavior in sync with coffee cup lid state.
/// Closed lid should not spill on landing, opened lid should.
/// </summary>
public sealed class CoffeeCupSpillSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoffeeCupVisualsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CoffeeCupVisualsComponent, OpenableOpenedEvent>(OnOpened);
        SubscribeLocalEvent<CoffeeCupVisualsComponent, OpenableClosedEvent>(OnClosed);
    }

    private void OnMapInit(Entity<CoffeeCupVisualsComponent> ent, ref MapInitEvent args)
    {
        SyncSpillOnThrow(ent.Owner);
    }

    private void OnOpened(Entity<CoffeeCupVisualsComponent> ent, ref OpenableOpenedEvent args)
    {
        SyncSpillOnThrow(ent.Owner);
    }

    private void OnClosed(Entity<CoffeeCupVisualsComponent> ent, ref OpenableClosedEvent args)
    {
        SyncSpillOnThrow(ent.Owner);
    }

    private void SyncSpillOnThrow(EntityUid uid)
    {
        if (!TryComp<OpenableComponent>(uid, out var openable) ||
            !TryComp<SpillableComponent>(uid, out var spillable))
        {
            return;
        }

        spillable.SpillWhenThrown = openable.Opened;
    }
}
