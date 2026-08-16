using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.Blacksmith;

/// <summary>
/// Player knowledge from studying the blacksmith guidebook.
/// Level 1: anvil crafts get 1 buff + 1 debuff.
/// Level 2: anvil crafts get 2 buffs + 0 debuffs, with boosted masterwork chance.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlacksmithKnowledgeComponent : Component
{
    /// <summary>
    /// 0 = unread, 1 = studied once, 2 = studied twice (max).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Level;
}

[Serializable, NetSerializable]
public sealed partial class BlacksmithStudyDoAfterEvent : SimpleDoAfterEvent;
