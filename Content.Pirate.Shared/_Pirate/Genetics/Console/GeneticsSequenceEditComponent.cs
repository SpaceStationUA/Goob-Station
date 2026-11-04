// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Pirate.Shared.Genetics.Console;

// Keep acknowledgements server-side; genome data stays in BUI state.
[RegisterComponent, Access(typeof(GeneticsConsoleSystem))]
public sealed partial class GeneticsSequenceEditComponent : Component
{
    public ulong Revision;
    public readonly Dictionary<NetEntity, Guid> AcknowledgedEdits = new();
}
