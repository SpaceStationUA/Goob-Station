using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server._DV.Roles;

public sealed partial class AddPsionicComponentSpecial : JobSpecial
{
    [DataField(required: true)]
    public ComponentRegistry Components { get; private set; } = new();

    /// <summary>
    /// If this is true then existing components will be removed and replaced with these ones.
    /// </summary>
    [DataField]
    public bool RemoveExisting = true;

    public override void AfterEquip(EntityUid mob)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();

        // Pirate: only entities with psionic potential may receive granted psionic powers.
        if (!entMan.HasComponent<PotentialPsionicComponent>(mob))
            return;

        var ev = new PsionicRollAttemptEvent();
        entMan.EventBus.RaiseLocalEvent(mob, ref ev);

        if (!ev.CanRoll)
            return;

        entMan.AddComponents(mob, Components, removeExisting: RemoveExisting);
    }
}
