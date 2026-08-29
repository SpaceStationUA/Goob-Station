// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Pirate.Knowledge.Quality;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Forging;

[Prototype]
public sealed partial class ForgingCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;
}

[Prototype]
public sealed partial class ForgedItemPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ForgedItemPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField, NeverPushInheritance]
    public bool Abstract { get; private set; }

    [DataField(required: true)]
    public ProtoId<ForgingCategoryPrototype> Category;

    [DataField]
    public EntProtoId? Result;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public ProtoId<TagPrototype>? Tag;

    [DataField]
    public ProtoId<ConstructionGraphPrototype>? Construction;

    [DataField]
    public EntProtoId? Finished;

    [DataField]
    public FixedPoint2 Work = 100;

    [DataField]
    public int Amount = 1;

    [DataField]
    public int Cost = 1;

    [DataField]
    public ResPath? Sprite;

    [DataField]
    public ProtoId<QualityPrototype>? QualityPrototype;

    [DataField]
    public Dictionary<EntProtoId, int> Skills = new()
    {
        { "MetalworkingKnowledge", 1 },
    };

    [DataField]
    public HashSet<ProtoId<MetalPrototype>>? Whitelist;

    [DataField]
    public HashSet<ProtoId<MetalPrototype>>? Blacklist;

    public string DisplayName(IPrototypeManager prototypes)
        => Result is { } result ? prototypes.Index(result).Name : Name;
}

[Prototype]
public sealed partial class MetalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name = string.Empty;

    [DataField(required: true)]
    public Color Color;

    [DataField(required: true)]
    public string IngotSprite = string.Empty;

    [DataField(required: true)]
    public float Density;

    [DataField(required: true)]
    public float WorkingTemp;

    [DataField(required: true)]
    public float MeltTemp;

    [DataField(required: true)]
    public float WorkingRange;

    [DataField]
    public float MaxTempModifier = 3f;

    public float MinTemp => WorkingTemp - WorkingRange;
    public float MaxTemp => WorkingTemp + WorkingRange * MaxTempModifier;

    [DataField]
    public FixedPoint2 WorkScale = 1;

    [DataField]
    public FixedPoint2 Durability = 1;

    [DataField]
    public float Speed = 1f;

    [DataField]
    public double Price = 1;

    [DataField]
    public Dictionary<string, FixedPoint2> Damage = new();

    [DataField]
    public Dictionary<string, FixedPoint2> DamageBonus = new();

    [DataField(required: true)]
    public EntProtoId Overheated;

    [DataField]
    public int MasteryOffset;
}
