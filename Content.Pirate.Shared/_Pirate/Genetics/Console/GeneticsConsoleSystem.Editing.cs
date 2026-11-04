// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Database;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Shared.Genetics.Console;

public sealed partial class GeneticsConsoleSystem
{
    private void OnSetBase(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleSetBaseMessage args)
    {
        // The client keeps pending edits until server state confirms them.
        if (_net.IsClient)
            return;

        AcknowledgeSequenceEdit(ent.Owner, args.Actor, args.EditId);
        if (GetWorkableMob(ent.Owner) is not {} mob ||
            args.Subject is {} subject && subject != GetNetEntity(mob) ||
            _genome.GetSequence(mob, args.Sequence) is not {} sequence ||
            args.Number is {} number && _mutation.GetRoundData(sequence.Mutation)?.Number != number ||
            args.Index >= sequence.Bases.Length ||
            sequence.OriginalBases[(int) args.Index] != 'X')
        {
            // Acknowledge rejected edits so the client can restore server state.
            UpdateUI(ent.Owner);
            return;
        }

        _builder.Clear();
        _builder.Append(sequence.Bases);
        var i = (int) args.Index;
        _builder[i] = CycleBase(sequence.Bases[i], args.Cycle);
        sequence.Bases = _builder.ToString();
        UpdateUI(ent.Owner);
    }

    private void OnResetSequence(Entity<GeneticsConsoleComponent> ent, ref GeneticsConsoleResetSequenceMessage args)
    {
        if (_net.IsClient)
            return;

        AcknowledgeSequenceEdit(ent.Owner, args.Actor, args.EditId);
        if (GetWorkableMob(ent.Owner) is not {} mob ||
            args.Subject is {} subject && subject != GetNetEntity(mob) ||
            _genome.GetSequence(mob, args.Index) is not {} sequence ||
            args.Number is {} number && _mutation.GetRoundData(sequence.Mutation)?.Number != number ||
            sequence.Bases == sequence.OriginalBases)
        {
            UpdateUI(ent.Owner);
            return;
        }

        _adminLog.Add(LogType.Genetics, LogImpact.Low, $"{mob:target} sequence {args.Index} was reset by {args.Actor:user} using console {ent.Owner:console}");
        sequence.Bases = sequence.OriginalBases;
        UpdateUI(ent.Owner);
    }

    private void AcknowledgeSequenceEdit(EntityUid console, EntityUid actor, Guid editId)
    {
        if (editId != Guid.Empty)
            EnsureComp<GeneticsSequenceEditComponent>(console).AcknowledgedEdits[GetNetEntity(actor)] = editId;
    }

    private void OnSequenceUiClosed(Entity<GeneticsConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_net.IsServer && args.UiKey.Equals(GeneticsConsoleUiKey.Key) &&
            TryComp<GeneticsSequenceEditComponent>(ent, out var edits))
            edits.AcknowledgedEdits.Remove(GetNetEntity(args.Actor));
    }
}
