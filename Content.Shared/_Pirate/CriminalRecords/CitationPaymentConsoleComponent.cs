// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CriminalRecords;

[RegisterComponent]
public sealed partial class CitationPaymentConsoleComponent : Component
{
    [DataField]
    public TimeSpan ResultTime = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan? ResultEnd;
}

[Serializable, NetSerializable]
public enum CitationPaymentConsoleVisuals : byte
{
    Status,
}

[Serializable, NetSerializable]
public enum CitationPaymentConsoleStatus : byte
{
    Idle,
    Accepted,
    Denied,
}

public enum CitationPaymentConsoleVisualLayers : byte
{
    Screen,
}
