using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Shared._Pirate.Traits.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;

namespace Content.Server._Pirate.Traits.EntitySystems;

public sealed class SmokeSensitivitySystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private const string MaskSlot = "mask";
    private const string HeadSlot = "head";
    private const string CoughEmoteId = "Cough";
    private const float MinWaterVaporMoles = 0.000001f;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SmokeSensitivityComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (component.CoughInterval <= 0f)
            {
                component.Accumulator = 0f;
                continue;
            }

            component.Accumulator += frameTime;

            while (component.Accumulator >= component.CoughInterval)
            {
                component.Accumulator -= component.CoughInterval;

                if (IsProtected(uid))
                    continue;

                if (!HasWaterVapor(uid, xform))
                    continue;

                _chat.TryEmoteWithChat(uid, CoughEmoteId, forceEmote: true);
            }
        }
    }

    private bool IsProtected(EntityUid uid)
    {
        return IsMaskProtected(uid) || IsHeadProtected(uid);
    }

    private bool IsMaskProtected(EntityUid uid)
    {
        if (!_inventory.TryGetSlotEntity(uid, MaskSlot, out var mask) || mask == null)
            return false;

        return HasComp<BreathToolComponent>(mask.Value)
            && (!TryComp<MaskComponent>(mask.Value, out var maskComp) || !maskComp.IsToggled);
    }

    private bool IsHeadProtected(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, HeadSlot, out var head)
            && head != null
            && HasComp<BreathToolComponent>(head.Value);
    }

    private bool HasWaterVapor(EntityUid uid, TransformComponent xform)
    {
        var mixture = _atmos.GetContainingMixture((uid, xform), ignoreExposed: true);
        return mixture != null && mixture.GetMoles(Gas.WaterVapor) > MinWaterVaporMoles;
    }
}
