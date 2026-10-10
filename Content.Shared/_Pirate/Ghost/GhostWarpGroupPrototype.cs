using Content.Shared.Radio;
using Content.Shared.Roles;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Ghost;
[Prototype("ghostWarpGroup")]
public sealed partial class GhostWarpGroupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;
    // Lower values win; this breaks ties when a player matches several groups.
    [DataField]
    public int Priority;
    [DataField]
    public HashSet<EntProtoId> MindRoles = new();
    [DataField]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();
    [DataField]
    public EntityWhitelist? Whitelist;
    [DataField]
    public HashSet<EntProtoId> Prototypes = new();
    [DataField]
    public Color? Color;
    [DataField]
    public ProtoId<RadioChannelPrototype>? RadioChannel;
}
