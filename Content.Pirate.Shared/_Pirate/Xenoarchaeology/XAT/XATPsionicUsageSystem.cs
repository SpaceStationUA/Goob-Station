using Content.Shared._DV.Psionics.Events;
using Content.Shared._Pirate.Xenoarchaeology.XAT.Components;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Artifact.XAT;

namespace Content.Shared._Pirate.Xenoarchaeology.XAT;

/// <summary>
/// System for xeno artifact trigger that requires a psionic power to be activated near the artifact node.
/// Uses the <see cref="PsionicPowerUsedEvent"/> that is raised whenever a psionic power is used
/// (the same event metapsionic pulse detection listens to) - but only to trigger artifact nodes,
/// without any popups or feedback to the psionic.
/// </summary>
public sealed class XATPsionicUsageSystem : BaseXATSystem<XATPsionicUsageComponent>
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsionicPowerUsedEvent>(OnPowerUsed);
    }

    private void OnPowerUsed(PsionicPowerUsedEvent ev)
    {
        var userCoords = Transform(ev.User).Coordinates;

        var query = EntityQueryEnumerator<XATPsionicUsageComponent, XenoArtifactNodeComponent>();
        while (query.MoveNext(out var nodeUid, out var trigger, out var node))
        {
            if (node.Attached is not { } artifactUid)
                continue;

            if (!TryComp<XenoArtifactComponent>(artifactUid, out var artifactComp))
                continue;

            var artifact = (artifactUid, artifactComp);

            // Only trigger if the psionic power was used within range of the node.
            if (!_xform.InRange(Transform(nodeUid).Coordinates, userCoords, trigger.Range))
                continue;

            if (!CanTrigger(artifact, (nodeUid, node)))
                continue;

            Trigger(artifact, (nodeUid, trigger, node));
        }
    }
}
