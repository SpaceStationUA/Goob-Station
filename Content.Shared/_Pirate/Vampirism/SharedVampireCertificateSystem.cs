using Content.Shared.Examine;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Interaction.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Vampirism;

/// <summary>
/// Handles opening/closing the vampire certificate and the text shown when it is examined open.
/// </summary>
public sealed class SharedVampireCertificateSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireCertificateComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<VampireCertificateComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUseInHand(Entity<VampireCertificateComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_timing.IsFirstTimePredicted)
            return;

        args.Handled = true;
        ent.Comp.IsClosed = !ent.Comp.IsClosed;
        Dirty(ent);
    }

    private void OnExamined(Entity<VampireCertificateComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.IsClosed)
            return;

        var profile = ent.Comp.OwnerProfile;

        var ownerName = profile?.Name ?? ent.Comp.OwnerName;
        if (!string.IsNullOrEmpty(ownerName))
            args.PushMarkup($"Власник: {ownerName}", 49);

        if (profile != null && _prototypeManager.TryIndex<SpeciesPrototype>(profile.Species, out var species))
            args.PushMarkup($"Раса: {Loc.GetString(species.Name)}", 48);

        if (profile != null)
            args.PushMarkup($"Стать: {profile.Gender}", 47);

        if (!string.IsNullOrEmpty(ent.Comp.PassportId))
            args.PushMarkup($"PID: {ent.Comp.PassportId}", 46);

        args.PushMarkup("Статус: офіційно визнаний вампір", 45);
    }
}
