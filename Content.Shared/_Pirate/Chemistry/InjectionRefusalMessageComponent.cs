// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Chemistry;

/// <summary>
/// Replaces the generic popup shown when an injector is used on a mob that has a bloodstream
/// but nothing to inject into, so the popup can explain why for this kind of mob.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InjectionRefusalMessageComponent : Component
{
    /// <summary>
    /// Shown to the user when they inject someone else. Gets the target as <c>$target</c>.
    /// </summary>
    [DataField(required: true)]
    public LocId Message;

    /// <summary>
    /// Shown when the user injects themselves.
    /// </summary>
    [DataField(required: true)]
    public LocId SelfMessage;
}
