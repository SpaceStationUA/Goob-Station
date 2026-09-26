// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Pirate.UIKit;

/// <summary>
///     Client-side bridge for embedding the CEF evidence board inside the
///     station records console window. Content.Client cannot reference
///     Content.Pirate.Client, so the pirate client system registers a
///     provider here at Initialize (same pattern as PdaThemeHost).
/// </summary>
public static class EvidenceBoardHost
{
    public static Action<Control, EntityUid>? Provider;
    public static Action? CloseAll;

    public static void Toggle(Control parent, EntityUid console) => Provider?.Invoke(parent, console);
}
