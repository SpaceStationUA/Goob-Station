// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.Pirate.UIKit;

/// <summary>
///     Client-side bridge for embedding the CEF theme picker inside the PDA
///     settings tab. Content.Client cannot reference Content.Pirate.Client,
///     so the pirate client system registers a provider here at Initialize.
/// </summary>
public static class PdaThemeHost
{
    public static Action<Container, EntityUid>? Provider;
    public static Action? CloseAll;

    public static void Toggle(Container parent, EntityUid pda) => Provider?.Invoke(parent, pda);
}
