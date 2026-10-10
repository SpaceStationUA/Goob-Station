// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.CustomMarkings;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidAppearanceComponent
{
    /// <summary>
    /// The custom markings this body wears, lowest first. The client fetches their art by hash and draws them
    /// (CustomMarkingSystem).
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CustomMarking> CustomMarkings = new();
}
