// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Robust.Shared.Random;

namespace Content.Shared._Pirate.Durability;

public sealed partial class DurabilitySystem
{
    private void OnExamined(Entity<DurabilityComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup("durability"))
        {
            var state = Loc.GetString($"durability-state-{ent.Comp.DurabilityState.ToString().ToLowerInvariant()}");
            args.PushMarkup(Loc.GetString(
                "durability-examine-condition",
                ("color", AssociatedColors[ent.Comp.DurabilityState].ToHex()),
                ("state", state)));

            if (HasComp<Content.Shared.Weapons.Melee.MeleeWeaponComponent>(ent))
            {
                args.PushMarkup(Loc.GetString(
                    "durability-examine-weapon",
                    ("color", AssociatedColors[ent.Comp.DurabilityState].ToHex()),
                    ("mod", $"{GetModifier(ent.Comp):0.00}")));
            }

            if (HasComp<Content.Shared.Weapons.Ranged.Components.GunComponent>(ent))
            {
                args.PushMarkup(Loc.GetString(
                    "durability-examine-gun",
                    ("color", AssociatedColors[ent.Comp.DurabilityState].ToHex()),
                    ("mod", $"{GetModifier(ent.Comp):0.00}")));
            }

            args.PushMarkup(GetRepairDescription(ent.Comp));
        }
    }

    private string GetRepairDescription(DurabilityComponent comp)
    {
        if (!comp.Repairable)
            return Loc.GetString("durability-repair-irreparable");

        var requirements = new List<string>();
        if (comp.RepairTool is { } tool)
            requirements.Add(Loc.GetString($"durability-tool-{tool.Id.ToLowerInvariant()}"));

        foreach (var materialId in comp.RepairMaterials.Keys)
        {
            if (_prototypes.Resolve(materialId, out var material))
                requirements.Add($"{Loc.GetString(material.Name)} {Loc.GetString(material.Unit)}");
        }

        if (requirements.Count == 0)
            return Loc.GetString("durability-repair-irreparable");

        return Loc.GetString(
            requirements.Count == 1 ? "durability-repair-needed-single" : "durability-repair-needed-multiple",
            ("requirements", string.Join(", ", requirements)));
    }

    private void OnInteractUsing(Entity<DurabilityComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Target != ent.Owner || args.Handled || !ent.Comp.Repairable ||
            ent.Comp.Damage <= -ent.Comp.MaxRepairBonus)
        {
            return;
        }

        if (TryComp(args.Used, out ToolComponent? tool) && ent.Comp.RepairTool is { } quality &&
            _tool.HasQuality(args.Used, quality, tool))
        {
            args.Handled = _tool.UseTool(
                args.Used,
                args.User,
                args.Target,
                ent.Comp.RepairDoAfter,
                [quality],
                new RepairToolDoAfterEvent(),
                out _,
                ent.Comp.FuelCost,
                tool);
            return;
        }

        if (!HasComp<MaterialComponent>(args.Used) ||
            !TryComp(args.Used, out PhysicalCompositionComponent? composition))
        {
            return;
        }

        var repair = ent.Comp.RepairMaterials
            .FirstOrDefault(pair => composition.MaterialComposition.ContainsKey(pair.Key.Id));
        if (repair.Equals(default(KeyValuePair<Robust.Shared.Prototypes.ProtoId<MaterialPrototype>, Vector2>)))
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.RepairDoAfter,
            new RepairItemDoAfterEvent(repair.Value),
            ent.Owner,
            args.Target,
            args.Used));
    }

    private void OnRepairItemDoAfter(Entity<DurabilityComponent> ent, ref RepairItemDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used || Deleted(used))
            return;

        var amount = PredictedRandom(ent, 0x52455049).NextFloat(args.MinMax.X, args.MinMax.Y);
        if (!DamageEntity(ent, -amount, ent.Comp, used: used))
            return;

        if (TryComp(used, out StackComponent? stack))
            _stack.ReduceCount((used, stack), 1);
        else
            PredictedQueueDel(used);

        args.Handled = true;
    }

    private void OnRepairToolDoAfter(Entity<DurabilityComponent> ent, ref RepairToolDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used || Deleted(used) ||
            ent.Comp.RepairTool is null || !TryComp(used, out ToolComponent? tool))
        {
            return;
        }

        var amount = PredictedRandom(ent, 0x52455054)
            .NextFloat(ent.Comp.ToolRepairAmount.X, ent.Comp.ToolRepairAmount.Y);
        DamageEntity(ent, -amount, ent.Comp, used: used);
        _tool.PlayToolSound(used, tool, args.User);
        args.Handled = true;
    }
}
