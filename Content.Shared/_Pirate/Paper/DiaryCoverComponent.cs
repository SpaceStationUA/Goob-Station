// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Paper;

// AppearanceComponent invokes a visualizer that requires paper sprite layers; keep page state here.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class DiaryCoverComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Open;

    [DataField, AutoNetworkedField]
    public bool PageLeft;

    [DataField, AutoNetworkedField]
    public bool PageRight;

    [DataField]
    public SoundSpecifier? OpenSound;

    [DataField]
    public SoundSpecifier? CloseSound;
}

[Serializable, NetSerializable]
public enum DiaryCoverLayers : byte
{
    Cover,
    PageLeft,
    PageRight,
}

[ByRefEvent]
public record struct DiaryCoverChangedEvent;
