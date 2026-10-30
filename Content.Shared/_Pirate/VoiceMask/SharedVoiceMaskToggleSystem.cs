// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Lock;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.VoiceMask;

// Shared verbs let the client predict this toggle instead of the hair-layer verb.
public abstract class SharedVoiceMaskToggleSystem : EntitySystem
{
    [Dependency] private LockSystem _lock = default!;

    // Must outrank the hair-layer verb (priority 1).
    private const int VerbPriority = 2;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoiceMaskToggleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    private void OnGetAltVerbs(Entity<VoiceMaskToggleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || _lock.IsLocked(ent.Owner))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Active ? "voice-mask-verb-disable" : "voice-mask-verb-enable"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Priority = VerbPriority,
            Act = () => Toggle(ent, user),
        });
    }

    protected virtual void Toggle(Entity<VoiceMaskToggleComponent> ent, EntityUid user)
    {
    }
}
