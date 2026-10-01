// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.WebUi;

/// <summary>Client window → server: current/allowed themes for this device.</summary>
[Serializable, NetSerializable]
public sealed class PirateThemeListRequestEvent : EntityEventArgs
{
    public NetEntity Pda;
}

/// <summary>Server → client: the device's theme state for the picker page.</summary>
[Serializable, NetSerializable]
public sealed class PirateThemeStateEvent : EntityEventArgs
{
    public NetEntity Pda;
    public string Current = "";
    public List<string> Allowed = new();
}

/// <summary>Picker page → server: switch this device to a theme id.</summary>
[Serializable, NetSerializable]
public sealed class PirateThemeSetEvent : EntityEventArgs
{
    public NetEntity Pda;
    public string ThemeId = "";
}

/// <summary>PDA Settings tab button → server: open the device theme picker.</summary>
[Serializable, NetSerializable]
public sealed class PdaShowThemeMessage : BoundUserInterfaceMessage;
