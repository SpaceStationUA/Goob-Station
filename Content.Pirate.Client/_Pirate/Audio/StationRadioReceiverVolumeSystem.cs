// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.StationRadio.Components;
using Content.Shared._Pirate.CCVars;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Pirate.Client._Pirate.Audio;

/// <summary>
/// Applies a client-side volume setting only to station radio receiver audio.
/// </summary>
public sealed class StationRadioReceiverVolumeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;

    private float _volumeOffsetDb;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesAfter.Add(typeof(AudioSystem));

        Subs.CVar(
            _configuration,
            PirateVars.StationRadioReceiverVolume,
            gain => _volumeOffsetDb = SharedAudioSystem.GainToVolume(gain),
            true);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<AudioComponent, TransformComponent>();
        while (query.MoveNext(out _, out var audio, out var xform))
        {
            if (!HasComp<StationRadioReceiverComponent>(xform.ParentUid))
                continue;

            audio.Volume = audio.Params.Volume + _volumeOffsetDb;
        }
    }
}
