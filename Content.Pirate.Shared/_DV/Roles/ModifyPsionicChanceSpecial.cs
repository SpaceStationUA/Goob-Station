using Content.Shared._DV.Psionics.Components;
using Content.Shared.Roles;

namespace Content.Shared._DV.Roles;

public sealed partial class ModifyPsionicChanceSpecial : JobSpecial
{
    /// <summary>
    /// The value to replace the JobChance with.
    /// </summary>
    [DataField]
    public float? JobBonusChance;

    /// <summary>
    /// If not null, it'll replace species bonus too.
    /// </summary>
    [DataField]
    public float? SpeciesBonusChance;

    /// <summary>
    /// If not null, it'll replace the mindbroken examine description used when the entity
    /// gets permanently mindbroken. Only applies if the entity already has psionic potential.
    /// </summary>
    [DataField]
    public LocId? MindbrokenExamineDesc;

    public override void AfterEquip(EntityUid mob)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (!entityManager.TryGetComponent(mob, out PotentialPsionicComponent? psionic))
            return;

        if (JobBonusChance is { } jobBonus)
            psionic.JobBonusChance = jobBonus;
        if (SpeciesBonusChance.HasValue)
            psionic.SpeciesBonusChance = SpeciesBonusChance.Value;
        if (MindbrokenExamineDesc is { } desc)
            psionic.MindbrokenExamineDesc = desc;
    }
}
