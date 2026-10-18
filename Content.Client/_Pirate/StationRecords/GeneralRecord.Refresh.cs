// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.StationRecords;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.StationRecords;

namespace Content.Client.StationRecords;

public sealed partial class GeneralRecord
{
    private GeneralStationRecord? _shownRecord;

    private RecordFieldWidths? _fieldWidths;

    public event Action<uint, byte[]?>? OnPrintPortrait;
    public event Action? OnUploadPortrait;
    public event Action<uint, byte[]>? OnStoreGeneratedPortrait;

    private RecordFieldWidths FieldWidths => _fieldWidths ??= new RecordFieldWidths(
        [SpeciesButton, NationalityButton, EmployerButton, GenderButton],
        [AgeEdit],
        [PersonJob.Parent!]);

    public void SetRecord(GeneralStationRecord record)
    {
        if (record.Equals(_shownRecord))
            return;

        PopulateRecord(record);
    }

    private void InitPortrait()
    {
        Portrait.OnPrint += (key, image) => OnPrintPortrait?.Invoke(key, image);
        Portrait.OnUpload += () => OnUploadPortrait?.Invoke();
        Portrait.OnStoreGenerated += (key, image) => OnStoreGeneratedPortrait?.Invoke(key, image);
    }

    public void SetPortrait(uint key, RecordPortraitData? portrait, GeneralStationRecord record, bool editable)
    {
        Portrait.SetPortrait(key, portrait, record.JobPrototype, record.Name, record.Age);
        Portrait.SetEditable(editable);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        UserInterfaceManager.DeferAction(() =>
        {
            if (!Disposed && IsInsideTree)
                FieldWidths.Update();
        });
    }
}
