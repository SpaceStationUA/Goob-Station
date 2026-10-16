// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;

namespace Content.Shared._Pirate.Body.Chips;

public sealed class HormoneChipExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HormoneChipComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<HormoneChipComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;
        using (args.PushGroup(nameof(HormoneChipComponent)))
        {
            args.PushMarkup(Loc.GetString("hormone-chip-examine"));

            if (comp.CritThresholdModifier != 0)
                PushChange(ref args, "hormone-chip-examine-crit", comp.CritThresholdModifier > 0, comp.CritThresholdModifier);

            if (!MathHelper.CloseTo(comp.StamcritMultiplier, 1f))
                PushChange(ref args, "hormone-chip-examine-stamina", comp.StamcritMultiplier > 1f, Percent(comp.StamcritMultiplier - 1f));

            if (!MathHelper.CloseTo(comp.HeatThresholdMultiplier, 1f))
                PushChange(ref args, "hormone-chip-examine-heat", comp.HeatThresholdMultiplier > 1f, Percent(comp.HeatThresholdMultiplier - 1f));

            if (!MathHelper.CloseTo(comp.ColdThresholdMultiplier, 1f))
                PushChange(ref args, "hormone-chip-examine-cold", comp.ColdThresholdMultiplier < 1f, Percent(1f - comp.ColdThresholdMultiplier));

            if (comp.LightStep)
                args.PushMarkup(Loc.GetString("hormone-chip-examine-light-step"));
        }
    }

    private static int Percent(float fraction) => (int) MathF.Round(fraction * 100f);

    private void PushChange(ref ExaminedEvent args, string effect, bool good, int amount)
    {
        args.PushMarkup(Loc.GetString(effect + (good ? "-up" : "-down"), ("amount", Math.Abs(amount))));
    }
}
