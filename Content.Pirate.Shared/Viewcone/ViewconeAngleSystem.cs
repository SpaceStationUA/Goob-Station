// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using Content.Pirate.Shared.Viewcone.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Pirate.Shared.Viewcone;

/// <summary>
/// Provides public API for getting the actual modified viewcone angle (including equipment etc) rather than just the base angle
/// </summary>
// Pirate: the source also relays through BodyComponent organs and scoped wieldables. GS2 has neither
// BodyRelayedEvent nor CursorOffsetRequiresWieldComponent.ViewAngleMultiplier, so those relays are omitted.
public sealed partial class ViewconeAngleSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private EntityQuery<ViewconeComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<ViewconeComponent>();

        SubscribeLocalEvent<StatusEffectContainerComponent, ModifyViewconeAngleEvent>(_status.RelayEvent);
        // Pirate: core InventorySystem.InitializeRelay cannot reference this event (it lives in the
        // Pirate module), so the inventory relay is raised here instead.
        SubscribeLocalEvent<InventoryComponent, ModifyViewconeAngleEvent>(OnInventoryModifyAngle);

        SubscribeLocalEvent<ViewconeModifierComponent, ExaminedEvent>(OnExamined);
        Subs.SubscribeWithRelay<ViewconeModifierComponent, ModifyViewconeAngleEvent>(OnModifyAngle, held: false);
        SubscribeLocalEvent<ViewconeModifierComponent, StatusEffectRelayedEvent<ModifyViewconeAngleEvent>>(OnEffectModifyAngle);
    }

    private void OnExamined(Entity<ViewconeModifierComponent> ent, ref ExaminedEvent args)
    {
        // 1.25 -> 25, 0.6 -> 40
        // Pirate fix: round instead of truncating (0.53 showed "48%"), and skip a no-op modifier.
        var percent = Math.Abs((int) Math.Round(ent.Comp.AngleModifier * 100f) - 100);
        if (percent == 0)
            return;

        var dir = ent.Comp.AngleModifier < 1f ? "decrease" : "increase";
        var loc = "viewcone-modifier-examine-" + dir;

        args.PushMarkup(Loc.GetString(loc, ("percent", percent)));
    }

    private void OnInventoryModifyAngle(Entity<InventoryComponent> ent, ref ModifyViewconeAngleEvent args)
    {
        _inventory.RelayEvent(ent, ref args);
    }

    private void OnModifyAngle(Entity<ViewconeModifierComponent> ent, ref ModifyViewconeAngleEvent args)
    {
        args.ModifyAngle(ent.Comp.AngleModifier);
    }

    private void OnEffectModifyAngle(Entity<ViewconeModifierComponent> ent, ref StatusEffectRelayedEvent<ModifyViewconeAngleEvent> args)
    {
        var ev = args.Args;
        ev.ModifyAngle(ent.Comp.AngleModifier);
        args.Args = ev; // holy dogshit please never ever do this
    }

    /// <summary>
    /// Returns the modified viewcone angle for an entity, calculated from the base,
    /// taking into account equipment & status effects & whatnot
    /// </summary>
    public float GetAngle(Entity<ViewconeComponent?> ent)
    {
        if (!_query.Resolve(ent, ref ent.Comp))
            return 0f;

        var ev = new ModifyViewconeAngleEvent();
        RaiseLocalEvent(ent, ref ev);

        // clamps to 0, 360 since this is could easily go over with stacking equipment items and shit
        return Math.Clamp(ent.Comp.BaseConeAngle * ev.AngleModifier, 0f, 360f);
    }
}

/// <summary>
/// Raised clientside by-ref and broadcast on an entity with a viewcone, and relayed to inventory & status effects.
/// Modifies their viewcone angle multiplicatively.
/// </summary>
[ByRefEvent]
public record struct ModifyViewconeAngleEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.MASK;

    private float _angleModifier = 1f;

    public float AngleModifier => _angleModifier;

    public void ModifyAngle(float angle)
    {
        _angleModifier *= angle;
    }
}
