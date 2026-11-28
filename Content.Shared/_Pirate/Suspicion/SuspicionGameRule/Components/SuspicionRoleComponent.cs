// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared.Roles.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;

[RegisterComponent]
public sealed partial class SuspicionRoleComponent : BaseMindRoleComponent
{
    [ViewVariables]
    public SuspicionRole Role { get; set; } = SuspicionRole.Pending;
    /// <summary>
    /// SubRole of the normal role, lets you specialize a role more if you want. If null everything just uses the normal Role definition.
    /// </summary>
    [ViewVariables]
    public SuspicionSubRole? SubRole { get; set; } = null;
}

public static class SusRoleExtensions
{
    public static string GetRoleColor(this SuspicionRole role)
    {
        return role switch
        {
            SuspicionRole.Traitor => "red",
            SuspicionRole.Detective => "blue",
            SuspicionRole.Innocent => "green",
            SuspicionRole.Wildcard => "pink",
            _ => "white",
        };
    }
}

[Serializable, NetSerializable]
public enum SuspicionRole
{
    Pending,

    Traitor,
    Detective,
    Innocent,
    Wildcard,
}

[Serializable, NetSerializable]
public enum SuspicionSubRole
{
    Jester,
}
