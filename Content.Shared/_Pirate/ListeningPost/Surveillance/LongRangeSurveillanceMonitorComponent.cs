// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ListeningPost;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LongRangeSurveillanceMonitorComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? TargetGrid;
}
