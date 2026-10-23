// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Genetics.Trigger;

/// <summary>
/// Pirate: genetics effects are fixed prototype data on both client and server.
/// The local EntityEffect hierarchy cannot be sent through NetSerializer, so only
/// component presence is networked; clients read the same effects from their prototypes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GeneticsEffectsOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField]
    public EntityEffect[] Effects = [];

    [DataField]
    public float Scale = 1f;
}

public sealed class GeneticsEffectsOnTriggerSystem : XOnTriggerSystem<GeneticsEffectsOnTriggerComponent>
{
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;

    protected override void OnTrigger(Entity<GeneticsEffectsOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        _effects.ApplyEffects(target, ent.Comp.Effects, ent.Comp.Scale);
        args.Handled = true;
    }
}
