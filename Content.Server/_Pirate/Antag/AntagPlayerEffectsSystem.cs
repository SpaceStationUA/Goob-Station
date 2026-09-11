// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag;
using Content.Shared.EntityEffects;

namespace Content.Server._Pirate.Antag;

public sealed class AntagPlayerEffectsSystem : EntitySystem
{
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagPlayerEffectsComponent, AfterAntagEntitySelectedEvent>(OnEntitySelected);
    }

    private void OnEntitySelected(Entity<AntagPlayerEffectsComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        foreach (var package in ent.Comp.Packages)
            _effects.TryApplyEffect(args.EntityUid, package);

        if (ent.Comp.Effects is { } effects)
            _effects.ApplyEffects(args.EntityUid, effects);
    }
}
