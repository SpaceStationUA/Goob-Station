// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Storage;

namespace Content.Server.Storage.EntitySystems;

public sealed partial class StorageSystem
{
    /// <summary>
    /// Copies storage configuration without copying references to stored items.
    /// </summary>
    public void CopyComponent(Entity<StorageComponent?> source, EntityUid target)
    {
        if (!Resolve(source, ref source.Comp))
            return;

        var targetComp = EnsureComp<StorageComponent>(target);
        targetComp.Grid = new List<Box2i>(source.Comp.Grid);
        targetComp.MaxItemSize = source.Comp.MaxItemSize;
        targetComp.QuickInsert = source.Comp.QuickInsert;
        targetComp.QuickInsertCooldown = source.Comp.QuickInsertCooldown;
        targetComp.OpenUiCooldown = source.Comp.OpenUiCooldown;
        targetComp.ClickInsert = source.Comp.ClickInsert;
        targetComp.OpenOnActivate = source.Comp.OpenOnActivate;
        targetComp.AreaInsert = source.Comp.AreaInsert;
        targetComp.AreaInsertRadius = source.Comp.AreaInsertRadius;
        targetComp.Whitelist = source.Comp.Whitelist;
        targetComp.Blacklist = source.Comp.Blacklist;
        targetComp.StorageInsertSound = source.Comp.StorageInsertSound;
        targetComp.StorageRemoveSound = source.Comp.StorageRemoveSound;
        targetComp.StorageOpenSound = source.Comp.StorageOpenSound;
        targetComp.StorageCloseSound = source.Comp.StorageCloseSound;
        targetComp.DefaultStorageOrientation = source.Comp.DefaultStorageOrientation;
        targetComp.HideStackVisualsWhenClosed = source.Comp.HideStackVisualsWhenClosed;
        targetComp.SilentStorageUserTag = source.Comp.SilentStorageUserTag;
        targetComp.ShowVerb = source.Comp.ShowVerb;

        UpdateOccupied((target, targetComp));
        Dirty(target, targetComp);

        var targetUi = EnsureComp<UserInterfaceComponent>(target);
        UI.SetUi((target, targetUi), StorageComponent.StorageUiKey.Key,
            new InterfaceData("StorageBoundUserInterface"));
    }
}
