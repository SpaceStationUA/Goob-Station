// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Movement.Components;
using Robust.Shared.Audio;

namespace Content.Shared._Pirate.Movement.Systems;

/// <summary>
/// Swaps the barefoot tile footstep sound for a clawed one on species that have claws rather than
/// bare feet. Driven by <see cref="SharedMoverController"/> rather than by an event, so that mobs
/// without <see cref="ClawFootstepsComponent"/> cost nothing more than a component lookup per step.
/// </summary>
public sealed class ClawFootstepsSystem : EntitySystem
{
    private EntityQuery<ClawFootstepsComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<ClawFootstepsComponent>();
    }

    /// <summary>
    /// Returns the claw equivalent of a barefoot tile sound, or the sound unchanged when this mob has
    /// no claws or the tile's barefoot sound has no claw counterpart.
    /// </summary>
    public SoundSpecifier? GetClawSound(EntityUid uid, SoundSpecifier? barestep)
    {
        if (barestep is not SoundCollectionSpecifier { Collection: { } collection } ||
            !_query.TryComp(uid, out var comp) ||
            !comp.Replacements.TryGetValue(collection, out var claw))
        {
            return barestep;
        }

        return claw;
    }
}
