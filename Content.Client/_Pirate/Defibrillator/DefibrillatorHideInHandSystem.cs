// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Defibrillator;
using Content.Shared.Hands;

namespace Content.Client._Pirate.Defibrillator;

/// <summary>
/// Prevents belt defibrillators from rendering a held in-hand sprite while they are carried in a hand.
/// </summary>
public sealed class DefibrillatorHideInHandSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DefibrillatorHideInHandComponent, GetInhandVisualsEvent>(OnGetInhandVisuals);
    }

    private void OnGetInhandVisuals(Entity<DefibrillatorHideInHandComponent> ent, ref GetInhandVisualsEvent args)
    {
        args.Layers.Clear();
    }
}
