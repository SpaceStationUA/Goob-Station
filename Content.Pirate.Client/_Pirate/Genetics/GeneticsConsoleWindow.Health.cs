// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Utility;

namespace Content.Pirate.Client.Genetics.UI;

public sealed partial class GeneticsConsoleWindow
{
    private (int? Health, double Genetic, double Radiation, double Toxic)? _healthReadout;

    private void UpdateHealth(EntityUid? uid)
    {
        if (uid is not {} mob || !_entMan.TryGetComponent<DamageableComponent>(mob, out var damageable))
        {
            _healthReadout = null;
            MobHealth.Text = Loc.GetString("generic-not-available-shorthand");
            MobDamages.Visible = false;
            return;
        }

        int? health = null;
        if (_entMan.System<MobThresholdSystem>().TryGetDeadThreshold(mob, out var threshold) && threshold > 0)
        {
            health = (int) Math.Round(Math.Clamp(
                100d * (1d - damageable.TotalDamage.Double() / threshold.Value.Double()), 0d, 100d));
        }

        var genetic = GetRoundedDamage("Cellular");
        var radiation = GetRoundedDamage("Radiation");
        var toxic = GetRoundedDamage("Poison");
        var readout = (health, genetic, radiation, toxic);
        if (_healthReadout == readout)
            return;

        _healthReadout = readout;
        MobHealth.Text = health is {} percent
            ? $"{percent}%"
            : Loc.GetString("generic-not-available-shorthand");

        var message = new FormattedMessage();
        message.PushTag(new MarkupNode("bold", null, null));
        message.AddText("(");
        AddDamage(genetic, "genetics-console-damage-genetic", Color.FromHex("#D69CFF"));
        message.AddText(" / ");
        AddDamage(radiation, "genetics-console-damage-radiation", Color.FromHex("#FFD966"));
        message.AddText(" / ");
        AddDamage(toxic, "genetics-console-damage-toxic", Color.FromHex("#80D980"));
        message.AddText(")");
        message.Pop();
        MobDamages.SetMessage(message);
        MobDamages.Visible = true;
        return;

        double GetRoundedDamage(string type)
        {
            return damageable.Damage.DamageDict.TryGetValue(type, out var damage)
                ? Math.Round(damage.Double(), 1, MidpointRounding.AwayFromZero)
                : 0;
        }

        void AddDamage(double damage, LocId code, Color color)
        {
            message.PushColor(color);
            message.AddText($"{damage.ToString("0.#", CultureInfo.CurrentCulture)} {Loc.GetString(code)}");
            message.Pop();
        }
    }
}
