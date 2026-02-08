using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Pirate.Shared._JustDecor.Weapons.SmartRevolver;

/// <summary>
/// Компонент для розумного револьвера, що може вибирати цілі і стріляти рекошетними патронами.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SmartRevolverComponent : Component
{
    /// <summary>
    /// Вибрана ціль.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SelectedTarget;

    /// <summary>
    /// Максимальна дистанція для вибору цілі.
    /// </summary>
    [DataField]
    public float MaxTargetDistance = 50f;

    /// <summary>
    /// Чи показувати візуальний превью траєкторії (опціональна функція).
    /// </summary>
    [DataField]
    public bool ShowTrajectory = false;

    /// <summary>
    /// Мінімальна кількість відскоків для куль, випущених з цієї зброї.
    /// </summary>
    [DataField]
    public int MinRicochets = 1;

    /// <summary>
    /// Максимальна кількість відскоків для куль, випущених з цієї зброї.
    /// </summary>
    [DataField]
    public int MaxRicochets = 4;

    /// <summary>
    /// Список всіх доступних цілей для циклу.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> AvailableTargets = new();

    /// <summary>
    /// Поточний індекс у списку доступних цілей.
    /// </summary>
    [ViewVariables]
    public int CurrentTargetIndex = 0;

    /// <summary>
    /// Дія для циклу по цілях.
    /// </summary>
    [DataField]
    public EntityUid? CycleTargetAction;
}

/// <summary>
/// Івент для циклу по цілях.
/// </summary>
[DataDefinition]
public sealed partial class CycleSmartRevolverTargetEvent : InstantActionEvent
{
}
