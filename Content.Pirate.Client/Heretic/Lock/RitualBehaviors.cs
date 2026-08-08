// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Shitcode.Heretic;
using Content.Shared.Heretic.Prototypes;

namespace Content.Pirate.Client.Heretic.Lock;

// Client-side definitions only keep ritual prototype deserialization in sync with the server.
public sealed partial class RitualEldritchIdBehavior : RitualCustomBehavior
{
    public override bool Execute(RitualData args, out string? outstr)
    {
        outstr = null;
        return true;
    }

    public override void Finalize(RitualData args) { }
}

public sealed partial class RitualLockAscendBehavior : RitualSacrificeBehavior;

public sealed partial class RitualLabyrinthPortalBehavior : RitualCustomBehavior
{
    public override bool Execute(RitualData args, out string? outstr)
    {
        outstr = null;
        return true;
    }

    public override void Finalize(RitualData args) { }
}

public sealed partial class RitualShatteredBehavior : RitualSacrificeBehavior;
