// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Voodoo;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Voodoo;

public sealed partial class VoodooDollChameleonSystem : SharedVoodooDollChameleonSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoodooDollChameleonComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(Entity<VoodooDollChameleonComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    protected override void UpdateSprite(EntityUid uid, EntityPrototype proto)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite)
            && proto.TryGetComponent(out SpriteComponent? otherSprite, Factory))
            _sprite.CopySprite((EntityUid.Invalid, otherSprite), (uid, sprite));
    }
}
