// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Instruments;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.CartridgeLoader.Cartridges;

/// <summary>D.E.T.O.M.A.T.I.X. cartridge state.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DetomatixCartridgeComponent : Component
{
    [DataField]
    public int Charges = 4;

    [DataField]
    public int MaxCharges = 4;

    [DataField]
    public List<ProtoId<SampledSongPrototype>> Songs = new()
    {
        "PirateSongHavaNagilaBars12",
    };

    /// <summary>Music reaches beyond the blast radius to warn bystanders.</summary>
    [DataField]
    public float SongRange = 12f;

    [DataField]
    public float SongVolume = 8f;

    [DataField]
    public TimeSpan DetonationDelay = TimeSpan.FromSeconds(0.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRefresh;
}
