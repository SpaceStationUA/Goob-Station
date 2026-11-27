// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization; // Pirate

namespace Content.Goobstation.Shared.MartialArts.Components;
[RegisterComponent, NetworkedComponent] // Pirate: a deleted combo target is sent as null.
public sealed partial class CanPerformComboComponent : Component
{
    [DataField]
    public EntityUid? CurrentTarget;

    [DataField]
    public ProtoId<ComboPrototype> BeingPerformed;

    [DataField]
    public int LastAttacksLimit = 4;

    [DataField]
    public List<ComboAttackType> LastAttacks = new();

    [DataField]
    public List<ComboAttackType>? LastAttacksSaved = new();

    [DataField]
    public List<ComboPrototype> AllowedCombos = new();

    [DataField]
    public List<ProtoId<ComboPrototype>> RoundstartCombos = new();

    [DataField]
    public TimeSpan ResetTime = TimeSpan.Zero;

    [DataField]
    public int ConsecutiveGnashes;

    // Pirate: keep the generated state's wire name and fields for existing replays.
    [Serializable, NetSerializable]
    public sealed class CanPerformComboComponent_AutoState : IComponentState
    {
        public NetEntity? CurrentTarget;
        public ProtoId<ComboPrototype> BeingPerformed;
        public List<ComboAttackType> LastAttacks = default!;
        public int ConsecutiveGnashes;
    }
}
