// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.StatusIcon;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Heretic;

[RegisterComponent, NetworkedComponent]
public sealed partial class GhoulComponent : Component
{
    /// <summary>
    ///     Total health for ghouls.
    /// </summary>
    [DataField] public FixedPoint2 TotalHealth = 50;

    [DataField]
    public bool DropOrgansOnDeath = true;

    [DataField]
    public EntProtoId? SpawnOnDeathPrototype;

    /// <summary>
    ///     Whether ghoul should be given a bloody blade
    /// </summary>
    [DataField]
    public bool GiveBlade;

    /// <summary>
    ///     Whether ghoulification should replace humanoid colors with the default grey appearance.
    /// </summary>
    [DataField]
    public bool ChangeAppearance = true; // Pirate: Shattered must preserve its glass appearance.

    [DataField]
    public LocId? ExamineMessage = "examine-system-cant-see-entity";

    [DataField]
    public EntityUid? BoundWeapon;

    [DataField]
    public EntProtoId BladeProto = "HereticBladeFleshGhoul";

    [DataField]
    public SoundSpecifier? BladeDeleteSound = new SoundCollectionSpecifier("gib");

    [DataField]
    public LocId GhostRoleName = "ghostrole-ghoul-name";

    [DataField]
    public LocId GhostRoleDesc = "ghostrole-ghoul-desc";

    [DataField]
    public LocId GhostRoleRules = "ghostrole-ghoul-rules";
}
