// SPDX-License-Identifier: MIT

namespace Content.Server._Pirate.MalfAI;

[RegisterComponent]
public sealed partial class MalfAiLockdownDoorComponent : Component
{
    public int ActiveLockdowns;
    public bool WasOpen;
    public bool? BoltsDown;
    public bool? Electrified;
    public bool? Safety;
}
