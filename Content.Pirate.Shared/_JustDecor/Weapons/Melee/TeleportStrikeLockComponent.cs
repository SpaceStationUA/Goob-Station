using System;
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Pirate.Shared._JustDecor.Weapons.Melee;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TeleportStrikeLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityCoordinates ReturnCoordinates;

    [DataField, AutoNetworkedField]
    public Vector2 ReturnVelocity;

    [DataField, AutoNetworkedField]
    public TimeSpan ReturnTime;

    [DataField, AutoNetworkedField]
    public TimeSpan AttackTime;

    [DataField, AutoNetworkedField]
    public EntityUid Target;

    [DataField, AutoNetworkedField]
    public EntityUid Weapon;
}
