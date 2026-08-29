using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Systems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server._DV.Roles;

/// <summary>
/// Grants psionic powers or components to a job's mob.
/// Supports either raw components via <see cref="Components"/> or a full power entity
/// prototype via <see cref="PowerPrototype"/> (which gets proper action buttons,
/// psionic registration, and power pool additions via <see cref="SharedPsionicSystem.InitializePowerComponents"/>).
/// </summary>
public sealed partial class AddPsionicComponentSpecial : JobSpecial
{
    /// <summary>
    /// Raw components to add. Use this for simple component additions that don't need
    /// psionic power initialization (e.g. innate telepathy via Psionic component).
    /// </summary>
    [DataField]
    public ComponentRegistry Components { get; private set; } = new();

    /// <summary>
    /// A psionic power entity prototype to grant (e.g. HealingWordEntity, DispelPowerEntity).
    /// When set, this takes precedence over <see cref="Components"/> and properly initializes
    /// the power through the psionic system.
    /// </summary>
    [DataField]
    public EntProtoId? PowerPrototype { get; private set; }

    /// <summary>
    /// If this is true then existing components will be removed and replaced with these ones.
    /// Only applies to <see cref="Components"/>.
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

        // If a power prototype is specified, use the proper psionic initialization path.
        if (PowerPrototype is { } powerProtoId)
        {
            var protoMan = IoCManager.Resolve<IPrototypeManager>();
            if (!protoMan.Resolve(powerProtoId, out var powerProto))
                return;

            entMan.AddComponents(mob, powerProto, removeExisting: false);

            var psionicSystem = entMan.System<SharedPsionicSystem>();
            psionicSystem.InitializePowerComponents(mob, powerProto);
            return;
        }

        // Fallback: add raw components without power initialization.
        entMan.AddComponents(mob, Components, removeExisting: RemoveExisting);
    }
}
