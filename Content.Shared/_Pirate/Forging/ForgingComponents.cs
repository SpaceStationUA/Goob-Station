// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Forging;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ForgedItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<ForgedItemPrototype> Item;

    [DataField, AutoNetworkedField]
    public bool Completed;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ForgingAnvilComponent : Component
{
    [DataField]
    public float IngotRange = 0.7f;

    [DataField]
    public int CostScale = 1;

    [DataField]
    public FixedPoint2 WorkScale = 1;

    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/_Pirate/Weapons/Melee/forging/tink.ogg");
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MetalIngotComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class MetallicComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public float MinTemp;

    [DataField(required: true), AutoNetworkedField]
    public float IdealTemp;

    [DataField(required: true), AutoNetworkedField]
    public ProtoId<MetalPrototype>? Metal;

    [DataField]
    public float DamageHoldingTemp = 353.15f;

    /// <summary>
    /// Damage dealt at each interval while this hot item is actually held.
    /// Pirate handles this with per-held-item timers instead of a global component scan.
    /// </summary>
    [DataField]
    public DamageSpecifier HoldingDamage = new()
    {
        DamageDict =
        {
            { "Heat", 5 },
        },
    };

    [DataField]
    public TimeSpan HoldingDamageInterval = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public bool Workable;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MetallicPopupsComponent : Component
{
    [DataField(required: true)]
    public LocId HeatedPopup;

    [DataField(required: true)]
    public LocId CooledPopup;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MetallicTagsComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<TagPrototype>> Workable = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<TagPrototype>> Unworkable = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class WorkableComponent : Component
{
    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Blunt";

    [DataField(required: true), AutoNetworkedField]
    public FixedPoint2 Remaining;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId Result;

    [DataField, AutoNetworkedField]
    public int Amount = 1;
}

[RegisterComponent]
public sealed partial class BurnableForgedComponent : Component
{
    [DataField]
    public float BurnTemp = 450f;

    [DataField]
    public EntProtoId BurnedPrototype = "FoodBurned";

    [DataField]
    public LocId BurnedPrefix = "burned-name-text";

    [DataField]
    public LocId BurnedPopup = "burned-popup-text";

    [DataField]
    public SoundSpecifier? BurnSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");
}

/// <summary>
/// Replaces Trauma's damage-over-time bloomery timer with one scheduled completion.
/// </summary>
[RegisterComponent]
public sealed partial class BloomerySmelterComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(60);
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ItemSlotHeaterComponent : Component
{
    [DataField(required: true)]
    public string Slot = string.Empty;

    [DataField(required: true)]
    public float HeatChange;

    [DataField]
    public float MaxTemp = 300f;

    [DataField]
    public TimeSpan Update = TimeSpan.FromSeconds(1);
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DamageOnHoldingImmuneComponent : Component;

/// <summary>
/// One-shot replacement used for slow crafting transitions without periodic damage or polling.
/// </summary>
[RegisterComponent]
public sealed partial class ScheduledEntityReplacementComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Result;

    [DataField(required: true)]
    public TimeSpan Duration;
}

[Serializable, NetSerializable]
public enum MetallicVisuals : byte
{
    Layer,
}

[Serializable, NetSerializable]
public enum AnvilUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AnvilStartItemMessage(
    ProtoId<MetalPrototype> metal,
    ProtoId<ForgedItemPrototype> item) : BoundUserInterfaceMessage
{
    public readonly ProtoId<MetalPrototype> Metal = metal;
    public readonly ProtoId<ForgedItemPrototype> Item = item;
}
