// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Items.Systems;
using Content.Pirate.Shared.Caduceus;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Pirate.Client.Caduceus;

/// <summary>
///     Keeps the Caduceus' sprite in sync with its current weapon form: world icon state,
///     in-hand RSI and sprite scale. The in-hand layers are supplied fresh on every
///     <see cref="GetInhandVisualsEvent"/> so the currently held form is always shown.
/// </summary>
public sealed partial class CaduceusVisualsSystem : VisualizerSystem<CaduceusComponent>
{
    private static readonly ResPath InactiveRsi = new("_Pirate/Objects/Weapons/Melee/Caduceus/vial_inactive.rsi");

    [Dependency] private readonly ItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CaduceusComponent, GetInhandVisualsEvent>(OnGetInhandVisuals,
            after: [typeof(ItemSystem)]);
    }

    protected override void OnAppearanceChange(EntityUid uid, CaduceusComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        var entity = new Entity<SpriteComponent?, AppearanceComponent>(uid, sprite, args.Component);
        if (!AppearanceSystem.TryGetData<CaduceusForm>(entity, CaduceusVisuals.Form, out var form, entity))
            return;

        CaduceusFormEntry? entry = null;
        if (form != CaduceusForm.Inactive)
            component.Forms.TryGetValue(form, out entry);

        var iconState = entry?.IconState ?? "inactive";
        if (SpriteSystem.LayerMapTryGet((uid, sprite), CaduceusVisualLayers.Icon, out var iconLayer, false))
        {
            SpriteSystem.LayerSetRsiState((uid, sprite), iconLayer, iconState);
            SpriteSystem.LayerSetVisible((uid, sprite), iconLayer, true);
        }

        var scale = entry?.Scale ?? 1f;
        SpriteSystem.SetScale((uid, sprite), new Vector2(scale, scale));

        // The holder's in-hand layers must be rebuilt for the new form.
        _item.VisualsChanged(uid);
    }

    private void OnGetInhandVisuals(Entity<CaduceusComponent> ent, ref GetInhandVisualsEvent args)
    {
        // Read the effective form from the appearance data (same source as OnAppearanceChange) so the
        // in-hand sprite always matches the world sprite, regardless of component state ordering.
        CaduceusForm form;
        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            if (!AppearanceSystem.TryGetData(ent, CaduceusVisuals.Form, out form, appearance))
                form = CaduceusForm.Inactive;
        }
        else
        {
            form = ent.Comp.Active ? ent.Comp.CurrentForm : CaduceusForm.Inactive;
        }

        CaduceusFormEntry? entry = null;
        if (form != CaduceusForm.Inactive)
            ent.Comp.Forms.TryGetValue(form, out entry);

        var rsi = entry?.InhandRsi ?? InactiveRsi;
        var state = args.Location == HandLocation.Left ? "inhand-left" : "inhand-right";

        var layer = new PrototypeLayerData
        {
            RsiPath = rsi.ToString(),
            State = state,
        };

        args.Layers.Add(($"caduceus-{args.Location.ToString().ToLowerInvariant()}", layer));
    }
}
