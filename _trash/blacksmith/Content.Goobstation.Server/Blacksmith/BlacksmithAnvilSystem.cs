using System.Linq;
using Content.Goobstation.Shared.Blacksmith;
using Content.Shared.Lathe;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Goobstation.Server.Blacksmith;

public sealed class BlacksmithAnvilSystem : SharedBlacksmithAnvilSystem
{
    /// <summary>
    /// Weight multiplier for exclusive masterwork-tier buffs at knowledge level 2.
    /// </summary>
    private const double MasterworkWeightMultiplier = 4.0;

    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlacksmithAnvilComponent, LatheQueueRecipeMessage>(OnQueueRecipe);
        SubscribeLocalEvent<BlacksmithAnvilComponent, LatheProducedEvent>(OnLatheProduced);
    }

    private void OnQueueRecipe(Entity<BlacksmithAnvilComponent> ent, ref LatheQueueRecipeMessage args)
    {
        ent.Comp.LastCrafter = args.Actor;
    }

    private void OnLatheProduced(Entity<BlacksmithAnvilComponent> ent, ref LatheProducedEvent args)
    {
        if (!HasComp<MeleeWeaponComponent>(args.Result))
            return;

        var level = 0;
        if (ent.Comp.LastCrafter != null &&
            TryComp<BlacksmithKnowledgeComponent>(ent.Comp.LastCrafter.Value, out var knowledge))
            level = knowledge.Level;

        var resultProto = MetaData(args.Result).EntityPrototype?.ID;
        var modifiers = GetModifierProtos(ent.Comp)
            .Where(p => p.AllowedPrototypes.Count == 0 ||
                        resultProto != null && p.AllowedPrototypes.Contains(resultProto))
            .ToList();

        if (modifiers.Count == 0)
            return;

        // Level 0 (any department, no book): 2 debuffs, no buffs.
        // Level 1: 1 buff + 1 debuff. Level 2: 2 buffs.
        var rolled = level switch
        {
            >= 2 => PickTwoBuffs(modifiers, boostMasterwork: true),
            1 => PickBuffAndDebuff(modifiers, allowExclusive: true),
            _ => PickTwoDebuffs(modifiers),
        };

        if (rolled.Count == 0)
            return;

        SetModifiers(args.Result, rolled);
    }

    /// <summary>
    /// Level 0 / all departments without guidebook: 2 debuffs, no buffs.
    /// </summary>
    private List<ProtoId<BlacksmithWeaponModifierPrototype>> PickTwoDebuffs(
        List<BlacksmithWeaponModifierPrototype> modifiers)
    {
        var debuffs = modifiers.Where(m => m.IsDebuff).ToList();
        var first = PickWeighted(debuffs);
        if (first == null)
            return new List<ProtoId<BlacksmithWeaponModifierPrototype>>();

        var secondPool = debuffs
            .Where(d => d.ID != first.ID && !Conflicts(first, d))
            .ToList();
        var second = PickWeighted(secondPool);

        var result = new List<ProtoId<BlacksmithWeaponModifierPrototype>> { first.ID };
        if (second != null)
            result.Add(second.ID);
        return result;
    }

    /// <summary>
    /// Level 1: 1 buff + 1 debuff.
    /// </summary>
    private List<ProtoId<BlacksmithWeaponModifierPrototype>> PickBuffAndDebuff(
        List<BlacksmithWeaponModifierPrototype> modifiers,
        bool allowExclusive)
    {
        var buffs = modifiers.Where(m => !m.IsDebuff && (allowExclusive || !m.Exclusive)).ToList();
        var debuffs = modifiers.Where(m => m.IsDebuff).ToList();

        var buff = PickWeighted(buffs);
        if (buff == null)
            return new List<ProtoId<BlacksmithWeaponModifierPrototype>>();

        if (buff.Exclusive)
            return new List<ProtoId<BlacksmithWeaponModifierPrototype>> { buff.ID };

        var compatibleDebuffs = debuffs.Where(d => !Conflicts(buff, d)).ToList();
        var debuff = PickWeighted(compatibleDebuffs);

        var result = new List<ProtoId<BlacksmithWeaponModifierPrototype>> { buff.ID };
        if (debuff != null)
            result.Add(debuff.ID);
        return result;
    }

    /// <summary>
    /// Level 2: 2 buffs + 0 debuffs. Exclusive still replaces the pair.
    /// </summary>
    private List<ProtoId<BlacksmithWeaponModifierPrototype>> PickTwoBuffs(
        List<BlacksmithWeaponModifierPrototype> modifiers,
        bool boostMasterwork)
    {
        var buffs = modifiers.Where(m => !m.IsDebuff).ToList();
        var first = PickWeighted(buffs, boostMasterwork);
        if (first == null)
            return new List<ProtoId<BlacksmithWeaponModifierPrototype>>();

        if (first.Exclusive)
            return new List<ProtoId<BlacksmithWeaponModifierPrototype>> { first.ID };

        var secondPool = buffs
            .Where(b => b.ID != first.ID && !b.Exclusive && !Conflicts(first, b))
            .ToList();
        var second = PickWeighted(secondPool, boostMasterwork);

        var result = new List<ProtoId<BlacksmithWeaponModifierPrototype>> { first.ID };
        if (second != null)
            result.Add(second.ID);
        return result;
    }

    private BlacksmithWeaponModifierPrototype? PickWeighted(
        List<BlacksmithWeaponModifierPrototype> pool,
        bool boostMasterwork = false)
    {
        if (pool.Count == 0)
            return null;

        double Weight(BlacksmithWeaponModifierPrototype p)
        {
            var w = p.Chance;
            // Level 2 only boosts masterwork, not legendary.
            if (boostMasterwork && p.ID == "BlacksmithMasterwork")
                w *= MasterworkWeightMultiplier;
            return w;
        }

        var total = pool.Sum(Weight);
        if (total <= 0)
            return _random.Pick(pool);

        var roll = _random.NextDouble() * total;
        var acc = 0.0;
        foreach (var proto in pool)
        {
            acc += Weight(proto);
            if (roll <= acc)
                return proto;
        }

        return pool[^1];
    }

    private static bool Conflicts(BlacksmithWeaponModifierPrototype a, BlacksmithWeaponModifierPrototype b)
    {
        return a.IncompatibleWith.Contains(b.ID) || b.IncompatibleWith.Contains(a.ID);
    }

    private List<BlacksmithWeaponModifierPrototype> GetModifierProtos(BlacksmithAnvilComponent? comp)
    {
        if (comp != null && comp.Modifiers.Count > 0)
        {
            return comp.Modifiers
                .Select(id => _prototypes.Index(id))
                .ToList();
        }

        return _prototypes.EnumeratePrototypes<BlacksmithWeaponModifierPrototype>().ToList();
    }
}
