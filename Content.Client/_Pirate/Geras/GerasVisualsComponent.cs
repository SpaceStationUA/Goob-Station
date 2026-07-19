// SPDX-FileCopyrightText: 2026 Pirate Station Contributors
//
// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;

namespace Content.Client._Pirate.Geras;

[RegisterComponent]
[Access(typeof(GerasVisualsSystem))]
public sealed partial class GerasVisualsComponent : Component
{
    public ShaderInstance? Shader;
}
