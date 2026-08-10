// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Skia;

/// <summary>
/// Creates a replacement objective once this objective is fulfilled.
/// </summary>
[RegisterComponent]
public sealed partial class SkiaRerollAfterCompletionComponent : Component
{
    public bool Rerolled;

    [DataField]
    public EntityUid MindUid;

    [DataField(required: true)]
    public EntProtoId RerollObjectivePrototype = default!;

    [DataField]
    public LocId? RerollObjectiveMessage;
}
