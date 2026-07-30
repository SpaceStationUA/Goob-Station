// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Heretic.Lock;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.Heretic.Lock;

public sealed class EldritchIdCardSystem : SharedEldritchIdCardSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EldritchIdCardComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EldritchIdCardComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(Entity<EldritchIdCardComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent);
    }

    private void OnAfterAutoHandleState(Entity<EldritchIdCardComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent);
    }

    protected override void UpdateSprite(Entity<EldritchIdCardComponent> ent)
    {
        if (ent.Comp.CurrentProto == null)
            return;

        var dummy = Spawn(ent.Comp.CurrentProto);
        _sprite.CopySprite(dummy, ent.Owner);
        Del(dummy);
    }
}
