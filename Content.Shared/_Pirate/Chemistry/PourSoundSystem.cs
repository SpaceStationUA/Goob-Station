// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio.Systems;

namespace Content.Shared._Pirate.Chemistry;

public sealed class PourSoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private EntityQuery<PourSoundComponent> _query;

    public override void Initialize()
    {
        base.Initialize();
        _query = GetEntityQuery<PourSoundComponent>();
    }

    public void TryPlay(EntityUid held, EntityUid user)
    {
        if (_query.TryComp(held, out var comp))
            _audio.PlayPredicted(comp.Sound, held, user);
    }
}
