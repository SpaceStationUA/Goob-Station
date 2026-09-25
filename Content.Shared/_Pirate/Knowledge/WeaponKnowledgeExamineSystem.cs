// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Parry;
using Content.Shared.Blocking;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Adds a detailed examine verb to weapons that lists which skills affect them
/// and how the examiner's own skill levels currently modify them.
/// </summary>
public sealed class WeaponKnowledgeExamineSystem : EntitySystem
{
    private const string VerbIcon = "/Textures/Interface/VerbIcons/information.svg.192dpi.png";
    private const string GoodColor = "#5fbf5f";
    private const string BadColor = "#e05555";

    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeaponClassComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, WeaponClassComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        var ent = new Entity<WeaponClassComponent>(uid, component);
        if (!_knowledge.SkillsEnabled || !ent.Comp.Examinable || !args.CanInteract || !args.CanAccess ||
            args.User == ent.Owner)
            return;

        var user = args.User;
        var weaponClass = _prototypes.Index(ent.Comp.Class);
        var level = _knowledge.GetKnowledgeLevel(user, weaponClass.Knowledge);

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("knowledge-weapon-class-examine",
            ("class", Loc.GetString(weaponClass.Name))));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("knowledge-weapon-examine-level",
            ("mastery", SharedKnowledgeSystem.GetMasteryString(SharedKnowledgeSystem.GetMastery(level))),
            ("level", level)));

        if (HasComp<MeleeWeaponComponent>(ent))
        {
            AddEffect(msg, "knowledge-weapon-examine-melee",
                weaponClass.MeleeDamage.GetCurve(level),
                weaponClass.MeleeDamage.GetCurve(100),
                higherIsBetter: true);
        }

        if (HasComp<GunComponent>(ent))
        {
            // Recoil consumers divide the spread by the curve, so show the resulting spread multiplier.
            AddEffect(msg, "knowledge-weapon-examine-spread",
                1f / weaponClass.AimSpeed.GetCurve(level),
                1f / weaponClass.AimSpeed.GetCurve(100),
                higherIsBetter: false);

            AddHolderSkillEffect<AimSpeedKnowledgeComponent>(msg, user, KnowledgeGameplaySystem.ShootingKnowledge,
                "knowledge-weapon-examine-shooting", c => c.Curve, inverse: true, higherIsBetter: false);
        }

        if (HasComp<BlockingComponent>(ent))
        {
            AddHolderSkillEffect<BlockFractionKnowledgeComponent>(msg, user, KnowledgeGameplaySystem.ShieldKnowledge,
                "knowledge-weapon-examine-block", c => c.Curve, inverse: false, higherIsBetter: true);
        }

        // ParrySystem resolves its skill through this weapon's class, so the class level is the one it checks.
        if (TryComp<ParryComponent>(ent, out var parry))
        {
            // A cost above 1 can never be paid, which is how a weapon opts out of parrying or reflecting.
            if (parry.ParryExhaustionCost <= 1f)
                AddRequirement(msg, "knowledge-weapon-examine-parry", parry.ParryMinSkill, level);

            if (parry.ReflectExhaustionCost <= 1f && parry.Reflects != ReflectType.None)
                AddRequirement(msg, "knowledge-weapon-examine-reflect", parry.ReflectMinSkill, level);
        }

        _examine.AddDetailedExamineVerb(args, ent.Comp, msg,
            Loc.GetString("knowledge-weapon-examine-verb-text"),
            VerbIcon,
            Loc.GetString("knowledge-weapon-examine-verb-message"));
    }

    /// <summary>
    /// Adds a line for a general skill that lives on the holder rather than on the weapon class,
    /// e.g. basic marksmanship or shield handling.
    /// </summary>
    private void AddHolderSkillEffect<T>(
        FormattedMessage msg,
        EntityUid user,
        EntProtoId skillId,
        string locId,
        Func<T, SkillCurve> getCurve,
        bool inverse,
        bool higherIsBetter) where T : Component, new()
    {
        if (!_prototypes.TryIndex(skillId, out var skillProto) ||
            !skillProto.TryGetComponent<T>(out var protoEffect, Factory))
            return;

        // Holders that never gained the skill entity get no modifier at all, matching KnowledgeGameplaySystem.
        var current = 1f;
        if (_knowledge.GetKnowledge(user, skillId) is { } skill && TryComp<T>(skill.Owner, out var effect))
            current = Apply(getCurve(effect).GetCurve(skill.Comp.NetLevel), inverse);

        var max = Apply(getCurve(protoEffect).GetCurve(100), inverse);
        AddEffect(msg, locId, current, max, higherIsBetter, ("skill", skillProto.Name));
    }

    private static float Apply(float curve, bool inverse)
        => inverse ? 1f / curve : curve;

    private void AddEffect(
        FormattedMessage msg,
        string locId,
        float current,
        float max,
        bool higherIsBetter,
        (string, object)? extra = null)
    {
        var currentText = FormatMultiplier(current, higherIsBetter);
        var maxText = FormatMultiplier(max, higherIsBetter);

        msg.PushNewline();
        msg.AddMarkupOrThrow(extra is { } arg
            ? Loc.GetString(locId, ("current", currentText), ("max", maxText), arg)
            : Loc.GetString(locId, ("current", currentText), ("max", maxText)));
    }

    private void AddRequirement(FormattedMessage msg, string locId, int required, int level)
    {
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString(locId,
            ("required", required),
            ("met", level >= required ? "yes" : "no")));
    }

    private static string FormatMultiplier(float value, bool higherIsBetter)
    {
        var text = $"×{value:0.00}";
        if (MathF.Abs(value - 1f) < 0.005f)
            return text;

        var good = value > 1f == higherIsBetter;
        return $"[color={(good ? GoodColor : BadColor)}]{text}[/color]";
    }
}
