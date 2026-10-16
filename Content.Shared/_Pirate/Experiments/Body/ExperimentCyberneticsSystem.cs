// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared._Shitmed.Body.Part;

namespace Content.Shared._Pirate.Experiments.Body;

/// <summary>
/// Uses the original Experiment cyberlimb sprites with native surgical prosthetics.
/// </summary>
public sealed class ExperimentCyberneticsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, BodyPartAddedEvent>(OnPartAdded, before: [typeof(SharedBodySystem)]);
    }

    private void OnPartAdded(Entity<HumanoidAppearanceComponent> ent, ref BodyPartAddedEvent args)
    {
        if (!TryComp<BodyPartComponent>(args.Part, out var part) || part.BaseLayerId is not { } original)
            return;

        // Pirate: Starlight selects prosthetic sprites by species; Shitmed stores one layer ID per part.
        var suffix = original.StartsWith("MobCyberneticBishop") ? original["MobCyberneticBishop".Length..]
            : original.StartsWith("ExperimentCyber") ? original["ExperimentCyber".Length..]
            : null;
        if (suffix is not ("LArm" or "RArm" or "LHand" or "RHand" or "LLeg" or "RLeg" or "LFoot" or "RFoot"))
            return;

        part.BaseLayerId = ent.Comp.Species == "Experiment" ? "ExperimentCyber" + suffix : "MobCyberneticBishop" + suffix;
        if (TryComp<BodyPartAppearanceComponent>(args.Part, out var appearance))
        {
            appearance.ID = part.BaseLayerId;
            Dirty(args.Part, appearance);
        }
        Dirty(args.Part, part);
    }
}
