using System.Collections.Generic;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Yautja;

/// <summary>
/// Мисливська пастка Яутжа: ставиться, активується як міна, ріже ногу при настанні.
/// Після спрацювання лишається використаною і більше не активується.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class YautjaHuntingTrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Used;

    [DataField]
    public DamageSpecifier TriggerDamage = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Slash", 50 },
        },
    };
}
