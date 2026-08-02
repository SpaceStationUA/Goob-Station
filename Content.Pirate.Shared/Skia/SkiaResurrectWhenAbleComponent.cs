// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Skia;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkiaResurrectWhenAbleComponent : Component
{
    [DataField]
    public float TimeToResurrect;

    [DataField, AutoNetworkedField]
    public TimeSpan? ResurrectAt;

    [DataField, AutoNetworkedField]
    public bool CanResurrect;

    [DataField]
    public LocId? ResurrectDesc;
}
