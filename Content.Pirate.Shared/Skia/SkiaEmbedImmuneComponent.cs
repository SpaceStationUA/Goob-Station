// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Skia;

[RegisterComponent, NetworkedComponent]
public sealed partial class SkiaEmbedImmuneComponent : Component
{
    [DataField(required: true)]
    public EntityWhitelist ImmuneTo = default!;
}
