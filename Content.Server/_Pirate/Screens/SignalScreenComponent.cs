// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.Screens;

/// <summary>
/// Displays values received through a device-link sink port, with a per-screen rate limit.
/// </summary>
[RegisterComponent, Access(typeof(SignalScreenSystem))]
[AutoGenerateComponentPause]
public sealed partial class SignalScreenComponent : Component
{
    [DataField]
    public ProtoId<SinkPortPrototype> TextPort = "Text";

    [DataField]
    public TimeSpan ChangeCooldown = TimeSpan.FromSeconds(0.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextChange;
}
