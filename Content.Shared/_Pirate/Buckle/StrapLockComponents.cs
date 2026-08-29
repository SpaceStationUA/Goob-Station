// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Buckle;

/// <summary>
/// Makes an unlocked strap require a holder until construction locks it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(StrapLockSystem))]
public sealed partial class StrapLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Locked;

    [DataField, AutoNetworkedField]
    public int RequiredHands = 2;

    [DataField]
    public ProtoId<EntityEffectPrototype> DropEffect = "CrucifixDropped";

    [DataField, AutoNetworkedField]
    public List<EntityUid> VirtualItems = new();
}

/// <summary>
/// Marks an entity that cannot act while it is being held on or locked to a strap.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StrapLockedComponent : Component;

/// <summary>
/// Links a buckled entity to the person currently holding it on the strap.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(StrapLockSystem))]
public sealed partial class StrapLockHeldComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Holder;

    [DataField]
    public bool Unsafe = true;
}

/// <summary>
/// Tracks the bounded holder-to-strap relationship while a target is being raised.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(StrapLockSystem))]
public sealed partial class StrapLockHoldingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Strap;

    [DataField, AutoNetworkedField]
    public EntityUid Buckled;

    [DataField, AutoNetworkedField]
    public float Range = 2f;

    [DataField, AutoNetworkedField]
    public ProtoId<EntityEffectPrototype> DropEffect;
}

/// <summary>
/// Restores a construction node when the strapped entity is removed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ChangeNodeOnUnstrapComponent : Component
{
    [DataField(required: true)]
    public string Node = string.Empty;
}
