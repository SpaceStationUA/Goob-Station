// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
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
/// Adds a detailed examine verb to weapons that lists every skill affecting them,
/// ordered by how much that skill can change the weapon between unskilled and master,
/// along with what the examiner's own levels currently do.
/// </summary>
public sealed class WeaponKnowledgeExamineSystem : EntitySystem
{
    private const string VerbIcon = "/Textures/Interface/students-cap.svg.192dpi.png";

    /// <summary>
    /// One color per mastery tier, matching the character editor except that unskilled is red instead of grey.
    /// </summary>
    private static readonly string[] MasteryColors =
    [
        "#E36D6D",
        "#79D279",
        "#5ABBEF",
        "#B58CFF",
        "#FFD166",
    ];

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
        if (!_knowledge.SkillsEnabled || !component.Examinable || !args.CanInteract || !args.CanAccess ||
            args.User == uid)
            return;

        var user = args.User;
        var weaponClass = _prototypes.Index(component.Class);
        var skills = new Dictionary<EntProtoId, SkillEntry>();

        if (HasComp<MeleeWeaponComponent>(uid))
        {
            AddClassEffect(skills, user, weaponClass.Knowledge, weaponClass.MeleeDamage,
                "knowledge-weapon-examine-effect-melee", inverse: false);
        }

        if (HasComp<GunComponent>(uid))
        {
            // Recoil consumers divide the spread by the curve, so show the resulting spread multiplier.
            AddClassEffect(skills, user, weaponClass.Knowledge, weaponClass.AimSpeed,
                "knowledge-weapon-examine-effect-spread", inverse: true);

            AddHolderEffect<AimSpeedKnowledgeComponent>(skills, user, KnowledgeGameplaySystem.ShootingKnowledge,
                c => c.Curve, "knowledge-weapon-examine-effect-spread", inverse: true);
        }

        if (HasComp<BlockingComponent>(uid))
        {
            AddHolderEffect<BlockFractionKnowledgeComponent>(skills, user, KnowledgeGameplaySystem.ShieldKnowledge,
                c => c.Curve, "knowledge-weapon-examine-effect-block", inverse: false);
        }

        // ParrySystem resolves its skill through this weapon's class, so the class skill is the one it checks.
        if (TryComp<ParryComponent>(uid, out var parry))
        {
            // A cost above 1 can never be paid, which is how a weapon opts out of parrying or reflecting.
            if (parry.ParryExhaustionCost <= 1f)
            {
                GetEntry(skills, user, weaponClass.Knowledge)?.Requirements
                    .Add(new Requirement("knowledge-weapon-examine-effect-parry", parry.ParryMinSkill));
            }

            if (parry.ReflectExhaustionCost <= 1f && parry.Reflects != ReflectType.None)
            {
                GetEntry(skills, user, weaponClass.Knowledge)?.Requirements
                    .Add(new Requirement("knowledge-weapon-examine-effect-reflect", parry.ReflectMinSkill));
            }
        }

        if (skills.Count == 0)
            return;

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("knowledge-weapon-examine-header"));

        var index = 0;
        foreach (var entry in skills.Values.OrderByDescending(e => e.Impact))
        {
            index++;
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("knowledge-weapon-examine-skill",
                ("index", index),
                ("skill", entry.Name),
                ("mastery", SharedKnowledgeSystem.GetMasteryString(SharedKnowledgeSystem.GetMastery(entry.Level))),
                ("color", MasteryColor(entry.Level)),
                ("level", entry.Level)));

            foreach (var effect in entry.Effects.OrderByDescending(e => e.Impact))
            {
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("knowledge-weapon-examine-effect",
                    ("effect", Loc.GetString(effect.LocId)),
                    ("current", FormatMultiplier(effect.Current, entry.Level)),
                    ("min", FormatMultiplier(effect.Unskilled, 0)),
                    ("max", FormatMultiplier(effect.Master, 100))));
            }

            foreach (var requirement in entry.Requirements)
            {
                msg.PushNewline();
                msg.AddMarkupOrThrow(Loc.GetString("knowledge-weapon-examine-requirement",
                    ("effect", Loc.GetString(requirement.LocId)),
                    ("required", requirement.Level),
                    ("met", entry.Level >= requirement.Level ? "yes" : "no")));
            }
        }

        AddTotals(msg, skills.Values);

        _examine.AddDetailedExamineVerb(args, component, msg,
            Loc.GetString("knowledge-weapon-examine-verb-text"),
            VerbIcon,
            Loc.GetString("knowledge-weapon-examine-verb-message"));
    }

    /// <summary>
    /// Every consumer multiplies its skill modifier into the same value, so an effect driven by several skills
    /// (e.g. spread from marksmanship and the weapon class) ends up as the product of their multipliers.
    /// </summary>
    private void AddTotals(FormattedMessage msg, IEnumerable<SkillEntry> skills)
    {
        var totals = skills
            .SelectMany(s => s.Effects)
            .GroupBy(e => e.LocId)
            .Where(g => g.Count() > 1)
            .Select(g => new Effect(g.Key,
                g.Aggregate(1f, (acc, e) => acc * e.Current),
                g.Aggregate(1f, (acc, e) => acc * e.Unskilled),
                g.Aggregate(1f, (acc, e) => acc * e.Master)))
            .OrderByDescending(e => e.Impact)
            .ToList();

        if (totals.Count == 0)
            return;

        foreach (var total in totals)
        {
            msg.PushNewline();
            msg.AddMarkupOrThrow(Loc.GetString("knowledge-weapon-examine-total",
                ("effect", Loc.GetString(total.LocId)),
                ("current", FormatMultiplier(total.Current, ProgressLevel(total)))));
        }
    }

    /// <summary>
    /// Where a combined value sits between its unskilled and master ends, as a 0-100 level for coloring.
    /// Measured on a log scale because the parts multiply.
    /// </summary>
    private static int ProgressLevel(Effect effect)
    {
        var range = MathF.Log(effect.Master / effect.Unskilled);
        if (MathF.Abs(range) < 0.0001f)
            return 100;

        var progress = MathF.Log(effect.Current / effect.Unskilled) / range;
        return (int) MathF.Round(Math.Clamp(progress, 0f, 1f) * 100f);
    }

    /// <summary>
    /// Adds an effect driven by the weapon class curve, which always applies, even at level 0.
    /// </summary>
    private void AddClassEffect(
        Dictionary<EntProtoId, SkillEntry> skills,
        EntityUid user,
        EntProtoId skillId,
        SkillCurve curve,
        string locId,
        bool inverse)
    {
        if (GetEntry(skills, user, skillId) is not { } entry)
            return;

        entry.Effects.Add(new Effect(locId,
            Apply(curve.GetCurve(entry.Level), inverse),
            Apply(curve.GetCurve(0), inverse),
            Apply(curve.GetCurve(100), inverse)));
    }

    /// <summary>
    /// Adds an effect from a general holder skill such as marksmanship or shield handling.
    /// </summary>
    private void AddHolderEffect<T>(
        Dictionary<EntProtoId, SkillEntry> skills,
        EntityUid user,
        EntProtoId skillId,
        Func<T, SkillCurve> getCurve,
        string locId,
        bool inverse) where T : Component, new()
    {
        if (!_prototypes.TryIndex(skillId, out var skillProto) ||
            !skillProto.TryGetComponent<T>(out var protoEffect, Factory) ||
            GetEntry(skills, user, skillId) is not { } entry)
            return;

        // Holders that never gained the skill entity get no modifier at all, matching KnowledgeGameplaySystem.
        var current = 1f;
        if (_knowledge.GetKnowledge(user, skillId) is { } skill && TryComp<T>(skill.Owner, out var effect))
            current = Apply(getCurve(effect).GetCurve(skill.Comp.NetLevel), inverse);

        var curve = getCurve(protoEffect);
        entry.Effects.Add(new Effect(locId,
            current,
            Apply(curve.GetCurve(0), inverse),
            Apply(curve.GetCurve(100), inverse)));
    }

    private SkillEntry? GetEntry(Dictionary<EntProtoId, SkillEntry> skills, EntityUid user, EntProtoId skillId)
    {
        if (skills.TryGetValue(skillId, out var entry))
            return entry;

        if (!_prototypes.TryIndex(skillId, out var skillProto))
            return null;

        entry = new SkillEntry(skillProto.Name, _knowledge.GetKnowledgeLevel(user, skillId));
        skills[skillId] = entry;
        return entry;
    }

    private static float Apply(float curve, bool inverse)
        => inverse ? 1f / curve : curve;

    private static string MasteryColor(int level)
        => MasteryColors[Math.Clamp(SharedKnowledgeSystem.GetMastery(level), 0, MasteryColors.Length - 1)];

    private static string FormatMultiplier(float value, int level)
        => $"[color={MasteryColor(level)}]×{value:0.00}[/color]";

    private sealed class SkillEntry(string name, int level)
    {
        public readonly string Name = name;
        public readonly int Level = level;
        public readonly List<Effect> Effects = new();
        public readonly List<Requirement> Requirements = new();

        /// <summary>
        /// A skill that only unlocks something ranks below every skill that scales a number.
        /// </summary>
        public float Impact => Effects.Count == 0 ? 1f : Effects.Max(e => e.Impact);
    }

    private readonly record struct Effect(
        string LocId,
        float Current,
        float Unskilled,
        float Master)
    {
        public float Impact => MathF.Max(Unskilled, Master) / MathF.Max(MathF.Min(Unskilled, Master), 0.0001f);
    }

    private readonly record struct Requirement(string LocId, int Level);
}
