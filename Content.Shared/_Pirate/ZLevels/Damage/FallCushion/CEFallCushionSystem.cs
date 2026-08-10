namespace Content.Shared._Pirate.ZLevels.Damage.FallCushion;

/// <summary>
/// Applies <see cref="CEFallCushionComponent"/> landing modifiers.
/// </summary>
public sealed class CEFallCushionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEFallCushionComponent, CEZFallingDamageCalculateEvent>(OnFallingDamageCalculate);
    }

    private void OnFallingDamageCalculate(Entity<CEFallCushionComponent> ent, ref CEZFallingDamageCalculateEvent args)
    {
        // Cushions only apply to other falling entities.
        if (ent.Owner == args.Fallen)
            return;

        args.DamageMultiplier *= ent.Comp.DamageMultiplier;
        args.StunMultiplier *= ent.Comp.StunMultiplier;
    }
}
