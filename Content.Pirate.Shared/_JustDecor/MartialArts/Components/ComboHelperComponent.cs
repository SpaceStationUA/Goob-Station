using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.GameStates;
using Content.Goobstation.Common.MartialArts;

namespace Content.Pirate.Shared._JustDecor.MartialArts.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ComboHelperComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("prototype"), AutoNetworkedField]
    public ProtoId<CqcComboHelperPrototype>? Prototype;

    [ViewVariables(VVAccess.ReadWrite), DataField("enabled"), AutoNetworkedField]
    public bool Enabled = true;

    [DataField("toggleAction"), AutoNetworkedField]
    public EntityUid? ToggleAction;
}

[Prototype("cqcComboHelper")]
public sealed partial class CqcComboHelperPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField("combos")]
    private List<ComboHelperEntry> _combos = new();

    public IReadOnlyList<ComboHelperEntry> Combos => _combos;

    /// <summary>
    /// Texture for the background of the helper.
    /// </summary>
    [DataField("background")]
    public SpriteSpecifier? Background;
}

[DataDefinition]
public sealed partial class ComboHelperEntry
{
    [DataField("comboId", required: true)]
    public ProtoId<ComboPrototype> ComboId = default!;

    [DataField("name")]
    public string? Name;

    [DataField("icons")]
    public List<SpriteSpecifier>? Icons;
}
