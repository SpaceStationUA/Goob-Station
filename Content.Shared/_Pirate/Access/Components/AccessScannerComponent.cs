// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Access.Components;

/// <summary>
/// Sends device-link signals for access cards found in its configured local radius.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(Systems.AccessScannerSystem))]
[AutoGenerateComponentState]
public sealed partial class AccessScannerComponent : Component
{
    [DataField]
    public ProtoId<SourcePortPrototype> ActivePort = "AccessActive";

    [DataField]
    public ProtoId<SourcePortPrototype> NamePort = "AccessName";

    [DataField]
    public ProtoId<SourcePortPrototype> JobPort = "AccessJob";

    [ViewVariables]
    public HashSet<EntityUid> Scanned = [];

    [ViewVariables]
    public bool Active;

    [DataField, AutoNetworkedField]
    public int Setting;

    [DataField(required: true)]
    public List<AccessScannerSetting> Settings = [];

    [DataField]
    public ProtoId<ToolQualityPrototype> SettingTool = "Screwing";

    [DataField]
    public SoundSpecifier? CycleSound = new SoundPathSpecifier("/Audio/Machines/lightswitch.ogg");

    /// <summary>
    /// Invalidates an already scheduled callback when the component is removed and re-added.
    /// </summary>
    [ViewVariables]
    public int ScanGeneration;
}

[DataRecord]
public partial record struct AccessScannerSetting(float Range, float Power);

/// <summary>
/// Prevents a card from being detected by an access scanner.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AccessScannerBlacklistComponent : Component;
