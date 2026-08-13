// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Audio;

public sealed class SequencedSoundLoopSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public void StartLoop(Entity<SequencedSoundLoopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Running)
            return;

        ent.Comp.Running = true;
        ent.Comp.LoopStarted = false;
        ent.Comp.MidIndex = 0;
        ent.Comp.NextPlayTime = _timing.CurTime + ent.Comp.StartLength;

        _audio.PlayPvs(ent.Comp.StartSound, ent.Owner, ent.Comp.StartParams ?? ent.Comp.Params);
    }

    public void StopLoop(Entity<SequencedSoundLoopComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !ent.Comp.Running)
            return;

        if (ent.Comp.LoopStarted && !TerminatingOrDeleted(ent.Owner))
            _audio.PlayPvs(ent.Comp.EndSound, ent.Owner, ent.Comp.Params);

        ent.Comp.Running = false;
        ent.Comp.LoopStarted = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SequencedSoundLoopComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Running || now < comp.NextPlayTime)
                continue;

            if (comp.MidLength <= TimeSpan.Zero)
            {
                comp.NextPlayTime = TimeSpan.MaxValue;
                continue;
            }

            comp.LoopStarted = true;
            PlayNextMid((uid, comp));
            comp.NextPlayTime = now + comp.MidLength;
        }
    }

    private void PlayNextMid(Entity<SequencedSoundLoopComponent> ent)
    {
        if (ent.Comp.MidSounds is not { } collection
            || !_proto.TryIndex(collection, out var proto)
            || proto.PickFiles.Count == 0)
            return;

        var index = ent.Comp.MidIndex % proto.PickFiles.Count;
        ent.Comp.MidIndex = index + 1;

        _audio.PlayPvs(new ResolvedCollectionSpecifier(collection, index), ent.Owner, ent.Comp.Params);
    }
}
