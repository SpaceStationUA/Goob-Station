// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Trigger.Components.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CreateHitmanCardOnTriggerComponent : BaseXOnTriggerComponent;
