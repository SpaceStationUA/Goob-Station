// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Genetics.Console;
using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Client.Genetics.UI;

public sealed class GeneticsSequenceEditor
{
    private readonly List<PendingEdit> _pending = new();
    private List<SequenceState> _authoritative = new();
    private NetEntity? _subject;
    private ulong? _revision;

    public List<SequenceState> Sequences { get; private set; } = new();
    public bool Pending => _pending.Count != 0;

    public bool SetState(GeneticsConsoleState state, NetEntity? actor)
    {
        // Ignore stale BUI snapshots, including replays during rollback.
        if (_revision is {} revision && state.Revision <= revision)
            return false;

        _revision = state.Revision;
        if (_subject != state.Subject)
            _pending.Clear();
        _subject = state.Subject;
        _authoritative = new List<SequenceState>(state.Sequences);

        if (actor is {} user && state.AcknowledgedEdits.TryGetValue(user, out var acknowledged))
        {
            var index = _pending.FindIndex(edit => edit.Id == acknowledged);
            if (index >= 0)
                _pending.RemoveRange(0, index + 1);
        }

        Rebuild();
        return true;
    }

    public GeneticsConsoleSetBaseMessage? SetBase(uint sequence, uint index, GeneticsCycle cycle)
    {
        if (sequence >= Sequences.Count || index >= Sequences[(int) sequence].Bases.Length ||
            Sequences[(int) sequence].OriginalBases[(int) index] != 'X')
            return null;

        var number = Sequences[(int) sequence].Number;
        var id = Guid.NewGuid();
        _pending.Add(new PendingEdit(id, number, index, cycle));
        Rebuild();
        return new GeneticsConsoleSetBaseMessage(sequence, index, cycle)
        {
            EditId = id,
            Subject = _subject,
            Number = number
        };
    }

    public GeneticsConsoleResetSequenceMessage? ResetSequence(uint sequence)
    {
        if (sequence >= Sequences.Count)
            return null;

        var number = Sequences[(int) sequence].Number;
        var id = Guid.NewGuid();
        _pending.Add(new PendingEdit(id, number, null, GeneticsCycle.Reset));
        Rebuild();
        return new GeneticsConsoleResetSequenceMessage(sequence)
        {
            EditId = id,
            Subject = _subject,
            Number = number
        };
    }

    private void Rebuild()
    {
        Sequences = new List<SequenceState>(_authoritative);
        foreach (var edit in _pending)
        {
            var index = Sequences.FindIndex(sequence => sequence.Number == edit.Number);
            if (index < 0)
                continue;

            var sequence = Sequences[index];
            if (edit.Index is not {} baseIndex)
            {
                Sequences[index] = sequence with { Bases = sequence.OriginalBases };
                continue;
            }

            if (baseIndex >= sequence.Bases.Length || sequence.OriginalBases[(int) baseIndex] != 'X')
                continue;

            var bases = sequence.Bases.ToCharArray();
            bases[baseIndex] = GeneticsConsoleSystem.CycleBase(bases[baseIndex], edit.Cycle);
            Sequences[index] = sequence with { Bases = new string(bases) };
        }
    }

    private readonly record struct PendingEdit(Guid Id, int Number, uint? Index, GeneticsCycle Cycle);
}
