// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._Pirate.Weapons.Ranged;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.Weapons.Ranged;

public sealed class MultiMagazineGunSystem : SharedMultiMagazineGunSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, GunSystem.UpdateAmmoCounterEvent>(OnAmmoUpdate);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, GunSystem.AmmoCounterControlEvent>(OnAmmoControl);
    }

    private void OnAmmoUpdate(Entity<MultiMagazineAmmoProviderComponent> ent,
        ref GunSystem.UpdateAmmoCounterEvent args)
    {
        // A composite can contain multiple providers with the same control type. Route an
        // update through the slot marker attached while controls were collected so each control
        // is updated by its own provider.
        if (args.Control is SlotStatusControl slotControl)
        {
            if (!ent.Comp.Slots.TryGetValue(slotControl.SlotId, out var multiplier) ||
                !GetMagazineEntities(ent).TryGetValue(slotControl.SlotId, out var nested) ||
                nested is not { } uid)
            {
                return;
            }

            var update = new GunSystem.UpdateAmmoCounterEvent
            {
                FireCostMultiplier = multiplier ?? 1f,
                Control = slotControl.Content,
            };
            RaiseLocalEvent(uid, update);
            return;
        }

        foreach (var (slotId, nested) in GetMagazineEntities(ent))
        {
            if (nested is not { } uid)
                continue;

            if (ent.Comp.Slots[slotId] is { } multiplier)
            {
                var update = new GunSystem.UpdateAmmoCounterEvent
                {
                    FireCostMultiplier = multiplier,
                    Control = args.Control,
                };
                RaiseLocalEvent(uid, update);
                continue;
            }

            RaiseLocalEvent(uid, args);
        }
    }

    private void OnAmmoControl(Entity<MultiMagazineAmmoProviderComponent> ent,
        ref GunSystem.AmmoCounterControlEvent args)
    {
        var nested = GetMagazineEntities(ent);
        var loaded = 0;
        foreach (var (slotId, uid) in nested)
        {
            if (uid is not { } actual)
                continue;

            loaded++;
            // Use a fresh event per slot so two providers with the same control type cannot
            // overwrite one another while being collected.
            var slotEvent = new GunSystem.AmmoCounterControlEvent();
            RaiseLocalEvent(actual, slotEvent);

            foreach (var control in slotEvent.Controls)
            {
                args.Controls.Add(new SlotStatusControl(slotId, control));
            }

            if (slotEvent.Controls.Count == 0)
            {
                args.Controls.Add(new SlotStatusControl(slotId, new GunSystem.DefaultStatusControl()));
            }
        }

        if (loaded == 0 && args.Controls.Count == 0)
            args.Controls.Add(new GunSystem.DefaultStatusControl());
    }

    /// <summary>
    /// Keeps the nested provider identity next to its visual control. This is necessary because
    /// the UI update event only carries the control instance, not the slot that owns it.
    /// </summary>
    private sealed class SlotStatusControl : PanelContainer
    {
        public string SlotId { get; }
        public Control Content { get; }

        public SlotStatusControl(string slotId, Control content)
        {
            SlotId = slotId;
            Content = content;
            HorizontalExpand = true;
            VerticalExpand = true;
            AddChild(content);
        }
    }
}
