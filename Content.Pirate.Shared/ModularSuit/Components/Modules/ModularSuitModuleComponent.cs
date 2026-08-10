using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.ModularSuit;

[Access(typeof(SharedModularSuitSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ModularSuitModuleComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public string ModuleId = string.Empty;

    [DataField]
    public char ModulePrefix = 'N';

    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();

    [DataField]
    public SuitPartType? ModulePart = default!;

    [DataField]
    public float PowerUsage;

    [DataField]
    public float PowerInstanceUsage;

    [DataField, AutoNetworkedField]
    public bool CanBeDisabled = true;

    [DataField("permanent"), AutoNetworkedField]
    public bool IsPermanent;

    [DataField("active"), ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public bool IsActive;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool WasActive;

    [DataField]
    public SpriteSpecifier? VerbIcon;
}
