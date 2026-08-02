// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Skia;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkiaScreamComponent : Component
{
    [DataField]
    public float Radius = 7f;

    [DataField]
    public bool LineOfSight = true;

    [DataField]
    public float PenetratingRadius = 2f;

    [DataField]
    public SoundSpecifier AbilitySound = new SoundPathSpecifier("/Audio/_Pirate/Skia/Effects/creepyshriek.ogg");

    [DataField]
    public EntProtoId Effect = "SkiaScreechEffect";

    [DataField]
    public EntProtoId ActionId = "ActionPsychokineticScreamSkia";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

[ByRefEvent]
public sealed partial class SkiaScreamActionEvent : InstantActionEvent;
