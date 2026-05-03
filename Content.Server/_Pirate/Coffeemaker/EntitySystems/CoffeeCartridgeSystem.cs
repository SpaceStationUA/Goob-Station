using Content.Shared._Pirate.Coffeemaker;
using Content.Shared._Pirate.Coffeemaker.Components;
using Content.Shared.Examine;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Coffeemaker.EntitySystems;

public sealed class CoffeeCartridgeSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CoffeeCartridgeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<CoffeeCartridgeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CoffeeCartridgeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnComponentInit(Entity<CoffeeCartridgeComponent> ent, ref ComponentInit args)
    {
        UpdateVisual(ent.Owner, ent.Comp);
    }

    private void OnMapInit(Entity<CoffeeCartridgeComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Randomized)
            return;

        if (ent.Comp.FancyVariants.Count == 0)
            return;

        var picked = _random.Pick(ent.Comp.FancyVariants);
        _metaData.SetEntityName(ent.Owner, picked.Name);
        ent.Comp.Variant = picked.Variant;
        UpdateVisual(ent.Owner, ent.Comp);

        ent.Comp.Randomized = true;
    }

    private void UpdateVisual(EntityUid uid, CoffeeCartridgeComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, CoffeeCartridgeVisuals.Variant, component.Variant, appearance);
    }

    private void OnExamined(Entity<CoffeeCartridgeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Charges > 0)
        {
            args.PushMarkup(Loc.GetString("coffeemaker-cartridge-examine-charges",
                ("charges", ent.Comp.Charges)));
            return;
        }

        args.PushMarkup(Loc.GetString("coffeemaker-cartridge-examine-empty"));
    }
}
