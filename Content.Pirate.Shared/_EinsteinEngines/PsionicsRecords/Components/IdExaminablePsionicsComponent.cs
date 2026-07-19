/// <summary>
/// Pirate psionics extras for IdExaminable entities.
/// </summary>

using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Access.Components;

/// <summary>
/// Component for storing psionics-related data for IdExaminable entities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IdExaminablePsionicsComponent : Component
{
	[DataField]
	public ProtoId<RadioChannelPrototype> RadioChannel = "Science";

	[DataField]
	public uint MaxStringLength = 256;
}

