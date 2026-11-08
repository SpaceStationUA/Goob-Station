using Content.Shared._Goobstation.Wizard.MagicMirror;
using Content.Shared._Pirate.BountyHunter;

namespace Content.Server._Pirate.BountyHunter;

public sealed class BountyHunterMirrorSystem : SharedBountyHunterMirrorSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public void CompleteUse(EntityUid mirror)
    {
        if (!TryComp<BountyHunterMirrorComponent>(mirror, out var restricted) || restricted.Used)
            return;

        restricted.Used = true;
        Dirty(mirror, restricted);
        _ui.CloseUi(mirror, WizardMirrorUiKey.Key);

        var transform = Transform(mirror);
        var replacement = Spawn("Mirror", transform.Coordinates);
        _transform.SetLocalRotation(replacement, transform.LocalRotation);
        QueueDel(mirror);
    }
}
