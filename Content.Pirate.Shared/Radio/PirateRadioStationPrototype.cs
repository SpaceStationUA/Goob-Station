using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Radio;

/// <summary>
/// A curated internet-radio station. These are the hand-picked stations that
/// always exist even with no network access; the server may merge them with
/// stations discovered through radio-browser.info.
/// </summary>
[Prototype("radioStation")]
public sealed partial class PirateRadioStationPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Human-readable station name.</summary>
    [DataField(required: true)]
    public string Label { get; private set; } = default!;

    /// <summary>Short genre/tag line shown under the name, e.g. "ambient".</summary>
    [DataField]
    public string Genre { get; private set; } = "";

    /// <summary>Direct <c>https://</c> stream URL. Must be an open-codec
    /// (Ogg/Opus) stream: stock CEF cannot decode MP3/AAC/HLS.</summary>
    [DataField(required: true)]
    public string Url { get; private set; } = default!;

    /// <summary>Shown first / marked as a favourite in the picker.</summary>
    [DataField]
    public bool Featured { get; private set; }
}
