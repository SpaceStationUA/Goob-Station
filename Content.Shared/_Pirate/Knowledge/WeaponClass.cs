// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Weapons.Ranged;
using Content.Shared.Examine;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

[Prototype]
public sealed partial class WeaponClassPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public EntProtoId Knowledge;

    [DataField]
    public SkillCurve MeleeDamage = new SumSkillCurve
    {
        Curves = new List<SkillCurve>
        {
            new LinearSkillCurve { CurveScale = 0.2f },
            new CubicSkillCurve
            {
                SkillOffset = -0.45f,
                CurveScale = 1.7f,
                CurveOffset = 0.95f,
            },
        },
    };

    [DataField]
    public SkillCurve AimSpeed = new SumSkillCurve
    {
        Curves = new List<SkillCurve>
        {
            new LinearSkillCurve { CurveScale = 0.2f },
            new CubicSkillCurve
            {
                SkillOffset = -0.45f,
                CurveScale = 2.7f,
                CurveOffset = 0.95f,
            },
        },
    };
}

[RegisterComponent, NetworkedComponent]
public sealed partial class WeaponClassComponent : Component
{
    [DataField(required: true)]
    public ProtoId<WeaponClassPrototype> Class;

    [DataField]
    public bool Examinable = true;
}

/// <summary>
/// Applies one direct skill lookup for the weapon involved in the current attack.
/// </summary>
public sealed class WeaponClassSystem : EntitySystem
{
    public static readonly ProtoId<WeaponClassPrototype> Unarmed = "Unarmed";

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    private EntityQuery<WeaponClassComponent> _weaponClassQuery;

    public override void Initialize()
    {
        base.Initialize();
        _weaponClassQuery = GetEntityQuery<WeaponClassComponent>();

        SubscribeLocalEvent<WeaponClassComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<WeaponClassComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<WeaponClassComponent, GetRecoilModifiersEvent>(OnGetRecoilModifiers);
    }

    private void OnExamined(Entity<WeaponClassComponent> ent, ref ExaminedEvent args)
    {
        if (!_knowledge.SkillsEnabled || !args.IsInDetailsRange || !ent.Comp.Examinable)
            return;

        var name = Loc.GetString(_prototypes.Index(ent.Comp.Class).Name);
        args.PushMarkup(Loc.GetString("knowledge-weapon-class-examine", ("class", name)));
    }

    private void OnGetMeleeDamage(Entity<WeaponClassComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (!_knowledge.SkillsEnabled)
            return;

        var prototype = _prototypes.Index(ent.Comp.Class);
        args.Damage *= prototype.MeleeDamage.GetCurve(_knowledge.GetKnowledgeLevel(args.User, prototype.Knowledge));
    }

    private void OnGetRecoilModifiers(Entity<WeaponClassComponent> ent, ref GetRecoilModifiersEvent args)
    {
        if (!_knowledge.SkillsEnabled || args.User == ent.Owner)
            return;

        var prototype = _prototypes.Index(ent.Comp.Class);
        args.Modifier /= prototype.AimSpeed.GetCurve(_knowledge.GetKnowledgeLevel(args.User, prototype.Knowledge));
    }

    public bool IsUnarmed(EntityUid user, EntityUid weapon)
        => user == weapon || _weaponClassQuery.TryComp(weapon, out var component) && component.Class == Unarmed;

    public int GetSkillLevel(Entity<WeaponClassComponent> weapon, EntityUid user)
    {
        var prototype = _prototypes.Index(weapon.Comp.Class);
        return _knowledge.GetKnowledgeLevel(user, prototype.Knowledge);
    }
}
