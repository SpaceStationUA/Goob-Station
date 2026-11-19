// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.Clothing.Accessories;

public sealed class ArmbandSideSystem : EntitySystem
{
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private static readonly SpriteSpecifier VerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/flip.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmbandSideComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ArmbandSideComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnMapInit(Entity<ArmbandSideComponent> ent, ref MapInitEvent args)
    {
        Apply(ent);
    }

    private void OnGetVerbs(Entity<ArmbandSideComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("pirate-armband-switch-arm"),
            Icon = VerbIcon,
            Act = () => SetFlipped(ent, !ent.Comp.Flipped),
        });
    }

    public void SetFlipped(Entity<ArmbandSideComponent> ent, bool flipped)
    {
        if (ent.Comp.Flipped == flipped)
            return;

        ent.Comp.Flipped = flipped;
        Dirty(ent);
        Apply(ent);
    }

    private void Apply(Entity<ArmbandSideComponent> ent)
    {
        _clothing.SetEquippedPrefix(ent, ent.Comp.Flipped ? ent.Comp.Prefix : null);
        _appearance.SetData(ent, ArmbandSideVisuals.Flipped, ent.Comp.Flipped);
    }
}
