// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Atmos.EntitySystems;
using Content.Server._EinsteinEngines.Atmos.Components;
using Content.Server._EinsteinEngines.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Arenas;

public abstract partial class ArenaRuleSystem<T>
{
    [Dependency] private readonly IgniteFromGasSystem _gasIgnition = default!;
    [Dependency] private readonly MetaDataSystem _arenaMetadata = default!;
    [Dependency] private readonly GasTankSystem _gasTanks = default!;
    [Dependency] private readonly InventorySystem _arenaInventory = default!;
    [Dependency] private readonly IPrototypeManager _arenaPrototypes = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;

    private static readonly EntProtoId[] BreathingTanks = ["NitrogenTankFilled", "PlasmaTankFilledInternals"];

    // Arena spawns have no station job or species loadout. Keep the player's hands free for objectives.
    protected void EquipArenaBreathingGear(EntityUid mob, EntityUid? previousTank = null)
    {
        SealArenaUniform(mob);
        if (!TryComp<InternalsComponent>(mob, out var internals) || !HasComp<RespiratorComponent>(mob) ||
            internals.GasTankEntity != null || _respirator.CanMetabolizeInhaledAir(mob))
            return;

        foreach (var tankPrototype in BreathingTanks)
        {
            if (!_arenaPrototypes.Index(tankPrototype).TryGetComponent<GasTankComponent>(out var gas, Factory) ||
                !_respirator.CanMetabolizeInhaledAir(mob, gas.Air))
                continue;

            var position = Transform(mob).Coordinates;
            EntityUid? mask = null;
            if (internals.BreathTools.Count == 0)
            {
                mask = SpawnAttachedTo("ClothingMaskBreath", position);
                if (!_arenaInventory.TryEquip(mob, mask.Value, "mask", silent: true, force: true))
                {
                    QueueDel(mask.Value);
                    return;
                }
            }

            var tank = previousTank ?? SpawnAttachedTo(tankPrototype, position);
            var equipped = _arenaInventory.TryGetSlotEntity(mob, "suitstorage", out var stored) && stored == tank;
            if ((!equipped && !_arenaInventory.TryEquip(mob, tank, "suitstorage", silent: true, force: true)) ||
                !_gasTanks.ConnectToInternals((tank, Comp<GasTankComponent>(tank)), mob))
            {
                if (previousTank == null)
                    QueueDel(tank);
                if (mask != null)
                    QueueDel(mask.Value);
            }
            return;
        }
    }

    private void SealArenaUniform(EntityUid mob)
    {
        if (!TryComp<IgniteFromGasComponent>(mob, out var sensitivity) ||
            !_arenaInventory.TryGetSlotEntity(mob, "jumpsuit", out var uniform))
            return;

        // A sealed underlayer keeps the issued team colours and leaves the hands free for stage weapons.
        var lining = EnsureComp<IgniteFromGasImmunityComponent>(uniform.Value);
        if (lining.Parts?.IsSupersetOf(sensitivity.IgnitableBodyParts.Keys) == true)
            return;

        lining.Parts = [.. (lining.Parts ?? []), .. sensitivity.IgnitableBodyParts.Keys];
        _gasIgnition.UpdateIgniteImmunity(mob);
        var meta = MetaData(uniform.Value);
        var note = Loc.GetString("pirate-arena-sealed-uniform");
        if (!meta.EntityDescription.EndsWith(note, StringComparison.Ordinal))
            _arenaMetadata.SetEntityDescription(uniform.Value, $"{meta.EntityDescription}\n{note}".Trim(), meta);
    }

}
