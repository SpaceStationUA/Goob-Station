// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Genetics.Mutations;

namespace Content.Pirate.Shared.Genetics.Console;

public sealed partial class GeneticsConsoleSetBaseMessage
{
    public Guid EditId;
    public NetEntity? Subject;
    public int? Number;
}

public sealed partial class GeneticsConsoleResetSequenceMessage
{
    public Guid EditId;
    public NetEntity? Subject;
    public int? Number;
}

public sealed partial class GeneticsConsoleState
{
    public readonly NetEntity? Subject;
    public readonly ulong Revision;
    public readonly Dictionary<NetEntity, Guid> AcknowledgedEdits = new();

    public GeneticsConsoleState(List<SequenceState> sequences, NetEntity? subject, ulong revision,
        Dictionary<NetEntity, Guid>? acknowledgedEdits = null) : this(sequences)
    {
        Subject = subject;
        Revision = revision;
        AcknowledgedEdits = acknowledgedEdits ?? new();
    }
}
