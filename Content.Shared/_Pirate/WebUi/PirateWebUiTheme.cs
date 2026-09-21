// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.WebUi;

/// <summary>
///     Prototype for a CEF (webui) theme: maps a content-authored theme
///     name to the CSS class the pages apply (see tokens.css). Appended to
///     as new corporation skins appear.
/// </summary>
[Prototype]
public sealed class PirateWebThemePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Class = "";
}

/// <summary>
///     Grants an entity's webui apps a theme. Entity prototypes inherit it
///     (SyndiPDA line gets the Syndicate skin); entities without it use
///     PirateNtWeb.
/// </summary>
[RegisterComponent]
public sealed partial class PirateWebUiThemeComponent : Component
{
    [DataField]
    public ProtoId<PirateWebThemePrototype> WebThemeId = "PirateNtWeb";
}
