// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.StationRecords;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.StationRecords;

public sealed class RecordConsoleSoundSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RecordConsoleSoundsComponent, RecordConsoleSoundMessage>(OnSound);
    }

    private void OnSound(Entity<RecordConsoleSoundsComponent> ent, ref RecordConsoleSoundMessage args)
    {
        if (_timing.CurTime < ent.Comp.NextSound || ent.Comp.Get(args.Sound) is not { } sound)
            return;

        ent.Comp.NextSound = _timing.CurTime + ent.Comp.Cooldown;
        _audio.PlayPredicted(sound, ent, args.Actor);
    }
}
