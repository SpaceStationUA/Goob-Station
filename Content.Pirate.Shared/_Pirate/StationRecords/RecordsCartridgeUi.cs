// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.StationRecords;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared._Pirate.StationRecords;

[Serializable, NetSerializable]
public enum RecordsCartridgeTab : byte
{
    Security,
    Crew,
    Psionics,
}

[Serializable, NetSerializable]
public sealed class RecordsCartridgeUiState : BoundUserInterfaceState
{
    public RecordsCartridgeTab Tab;

    public List<RecordsCartridgePage> Pages = new();
}

[Serializable, NetSerializable]
public sealed class RecordsCartridgePage
{
    public RecordsCartridgeTab Tab;
    // Null when the PDA is not on a station.
    public Dictionary<uint, string>? RecordListing;
    public uint? SelectedKey;
    public uint SelectionRequest;
    public string? Name;
    public List<RecordLogEntry>? Log;
    public bool CanEdit;

    public bool HasRecord => SelectedKey != null && Name != null && Log != null;

    public RecordsCartridgePage WithRecordOf(RecordsCartridgePage record)
    {
        return new RecordsCartridgePage
        {
            Tab = Tab,
            RecordListing = RecordListing,
            CanEdit = CanEdit,
            SelectedKey = record.SelectedKey,
            SelectionRequest = record.SelectionRequest,
            Name = record.Name,
            Log = record.Log,
        };
    }
}

[Serializable, NetSerializable]
public sealed class RecordsCartridgeUiMessageEvent(RecordsCartridgeTab tab, BoundUserInterfaceMessage? message = null)
    : CartridgeMessageEvent
{
    public readonly RecordsCartridgeTab Tab = tab;
    public readonly BoundUserInterfaceMessage? Message = message;
}
