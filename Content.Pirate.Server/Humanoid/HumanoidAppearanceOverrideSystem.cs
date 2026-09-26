// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Pirate.Shared.Humanoid;
using Content.Shared._EinsteinEngines.HeightAdjust;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Humanoid;

public sealed class HumanoidAppearanceOverrideSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MarkingManager _markings = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly HeightAdjustSystem _heightAdjust = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceOverrideComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HumanoidAppearanceOverrideComponent> ent, ref MapInitEvent args)
    {
        RemCompDeferred<HumanoidAppearanceOverrideComponent>(ent);

        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        if (ent.Comp.HairStyles is { } styles)
            RerollHair(ent, humanoid, styles);

        if (ent.Comp.RemoveFacialHair)
        {
            humanoid.MarkingSet.RemoveCategory(MarkingCategories.FacialHair);
            humanoid.MarkingSet.RemoveCategory(MarkingCategories.FacialHairSpecial);
        }

        var species = _proto.Index(humanoid.Species);
        if (ent.Comp.HeightCm is { } height)
            _humanoid.SetHeight(ent, _random.NextFloat(height.X, height.Y) / species.AverageHeight, false, humanoid);

        if (ent.Comp.Width is { } width)
            _humanoid.SetWidth(ent, _random.NextFloat(width.X, width.Y), false, humanoid);

        // LoadProfile does the same: the component values alone don't rescale the sprite and fixtures.
        _heightAdjust.SetScale(ent, new Vector2(humanoid.Width, humanoid.Height));
        Dirty(ent, humanoid);
    }

    private void RerollHair(EntityUid uid, HumanoidAppearanceComponent humanoid, ProtoId<Content.Shared.Dataset.DatasetPrototype> styles)
    {
        var color = humanoid.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var current) && current.Count > 0
            ? current[0].MarkingColors.FirstOrDefault(Color.White)
            : Color.White;

        humanoid.MarkingSet.RemoveCategory(MarkingCategories.Hair);
        humanoid.MarkingSet.RemoveCategory(MarkingCategories.HairSpecial);

        var options = _proto.Index(styles).Values
            .Where(id => _markings.Markings.TryGetValue(id, out var marking) &&
                         _markings.CanBeApplied(humanoid.Species, humanoid.Sex, marking, _proto))
            .ToList();

        if (options.Count > 0)
            _humanoid.AddMarking(uid, _random.Pick(options), color, false, humanoid: humanoid);
    }
}
