// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Implants.Components;

// Pirate: a dedicated extractor must never implant an implant.
public sealed partial class ImplanterComponent
{
    [DataField]
    public bool ExtractOnly;
}
