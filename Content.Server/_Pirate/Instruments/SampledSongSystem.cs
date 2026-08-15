// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Instruments;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Instruments;

/// <summary>Plays baked songs from one-shot samples.</summary>
public sealed class SampledSongSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>Keeps soft notes audible.</summary>
    private const float MinVelocityVolume = -12f;

    /// <summary>Drops late notes to avoid catch-up chords.</summary>
    private const float LateNoteCutoff = 0.4f;

    public bool TryPlaySong(EntityUid uid,
        ProtoId<SampledSongPrototype> song,
        float? range = null,
        float? volume = null,
        bool loop = false)
    {
        if (!_proto.HasIndex(song))
            return false;

        var player = EnsureComp<SampledSongPlayerComponent>(uid);
        player.Song = song;
        player.StartTime = _timing.CurTime;
        player.Started = true;
        player.NextNote = 0;
        player.Loop = loop;

        if (range is { } r)
            player.Range = r;

        if (volume is { } v)
            player.Volume = v;

        return true;
    }

    public void StopSong(EntityUid uid)
    {
        RemComp<SampledSongPlayerComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SampledSongPlayerComponent>();
        while (query.MoveNext(out var uid, out var player))
        {
            if (!_proto.TryIndex(player.Song, out var song) || !_proto.TryIndex(song.Bank, out var bank))
            {
                Log.Warning($"Removing {ToPrettyString(uid)}'s song player, {player.Song} does not resolve.");
                RemCompDeferred<SampledSongPlayerComponent>(uid);
                continue;
            }

            // Components added from YAML start on their first tick.
            if (!player.Started)
            {
                player.StartTime = _timing.CurTime;
                player.Started = true;
            }

            var elapsed = (float) (_timing.CurTime - player.StartTime).TotalSeconds;

            while (player.NextNote < song.Notes.Count && song.Notes[player.NextNote].Time <= elapsed)
            {
                var note = song.Notes[player.NextNote];
                player.NextNote++;

                if (elapsed - note.Time <= LateNoteCutoff)
                    PlayNote(uid, bank, note, player);
            }

            if (player.NextNote < song.Notes.Count || elapsed < song.Duration)
                continue;

            if (!player.Loop)
            {
                RemCompDeferred<SampledSongPlayerComponent>(uid);
                continue;
            }

            player.StartTime = _timing.CurTime;
            player.NextNote = 0;
        }
    }

    /// <summary>Converts MIDI velocity to an engine volume offset.</summary>
    private static float VelocityVolume(byte velocity)
    {
        return Math.Max(MinVelocityVolume, 10f * MathF.Log10(Math.Max(velocity, (byte) 1) / 127f));
    }

    private void PlayNote(EntityUid uid, SampleBankPrototype bank, SampledNote note, SampledSongPlayerComponent player)
    {
        var (sample, pitch) = bank.Resolve(note.Key);
        var volume = player.Volume + VelocityVolume(note.Velocity);

        _audio.PlayEntity(sample,
            Filter.Empty().AddInRange(_xform.GetMapCoordinates(uid), player.Range),
            uid,
            true,
            AudioParams.Default
                .WithVolume(volume)
                .WithPitchScale(pitch)
                .WithMaxDistance(player.Range));
    }
}
