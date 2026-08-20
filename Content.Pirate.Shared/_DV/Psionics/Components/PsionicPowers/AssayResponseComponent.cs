using Robust.Shared.GameStates;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

/// <summary>
///     Attach to an entity to rewrite or append to the result of a psionic Assay scan.
///     Lets a scanned entity refuse to reveal anything (replace the whole result) or
///     add a snarky comment at the end of the report.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AssayResponseComponent : Component
{
    /// <summary>
    ///     If set, the entire assay result (including the "no powers" message) is
    ///     replaced with this locale string.
    /// </summary>
    [DataField]
    public LocId? ReplaceMessage;

    /// <summary>
    ///     If set, this locale string is appended to the end of the assay result.
    /// </summary>
    [DataField]
    public LocId? AppendMessage;
}
