// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Movement.Components;
using Robust.Shared.Audio;

namespace Content.Shared._Pirate.Movement.Systems;

public sealed class ClawFootstepsSystem : EntitySystem
{
    private EntityQuery<ClawFootstepsComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<ClawFootstepsComponent>();
    }

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
