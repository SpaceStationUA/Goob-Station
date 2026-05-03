using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Silicons.IPC;
using Content.Server.Humanoid;
using Robust.Shared.Prototypes;
using System.Linq;
using System;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;

namespace Content.Server.Silicons.IPC;

public sealed class ScreenSaverSystem : SharedScreenSaverSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    private List<MarkingPrototype>? _ipcFaceMarkings = null;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScreenSaverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ScreenSaverComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<SelectScreenSaverMessage>(OnSelectScreen);
        SubscribeLocalEvent<BodyPartComponent, DamageChangedEvent>(OnBodyPartDamage);
    }

    private void PopulateCache()
    {
        _ipcFaceMarkings = MarkingManager.Markings.Values
            .Where(m => m.MarkingCategory == MarkingCategories.Face &&
                        m.SpeciesRestrictions != null &&
                        m.SpeciesRestrictions.Contains("IPC"))
            .ToList();
    }

    private void OnMapInit(EntityUid uid, ScreenSaverComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ref component.ActionEntity, component.ActionId);
        UpdateVisuals(uid, component);
        Dirty(uid, component);
    }

    private void OnShutdown(EntityUid uid, ScreenSaverComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionEntity);
    }

    private void OnSelectScreen(SelectScreenSaverMessage msg, EntitySessionEventArgs args)
    {
        var uid = args.SenderSession.AttachedEntity;
        if (uid == null) return;

        if (!TryComp<ScreenSaverComponent>(uid, out var component))
            return;

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        if (!MarkingManager.Markings.TryGetValue(msg.MarkingId, out var markingPrototype))
            return;
        
        if (markingPrototype.MarkingCategory != MarkingCategories.Face)
            return;
         
        if (markingPrototype.SpeciesRestrictions != null && !markingPrototype.SpeciesRestrictions.Contains("IPC"))
            return;

        ReplaceScreenMarking(uid.Value, msg.MarkingId, component, humanoid, true);
    }

    private void ReplaceScreenMarking(EntityUid uid, string newMarkingId, ScreenSaverComponent screenSaver, HumanoidAppearanceComponent humanoid, bool sound = false)
    {
        var color = Color.White;
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Face, out var existing))
        {
            var lastMarking = existing.LastOrDefault();
            if (lastMarking != null && lastMarking.MarkingColors.Count > 0)
            {
                color = lastMarking.MarkingColors[0];
            }

            var toRemove = new List<Marking>(existing);
            foreach (var m in toRemove)
            {
                RemoveMarking(uid, m.MarkingId);
            }
        }

        Humanoid.AddMarking(uid, newMarkingId, color);

        screenSaver.CurrentScreen = newMarkingId;
        if (sound) {
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Pirate/Machines/terminal_prompt.ogg"), uid);
        }
        UpdateVisuals(uid, screenSaver);
        Dirty(uid, screenSaver);
    }

    private void RemoveMarking(EntityUid uid, string marking, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid)
            || !MarkingManager.Markings.TryGetValue(marking, out var prototype))
        {
            return;
        }

        humanoid.MarkingSet.Remove(prototype.MarkingCategory, marking);

        if (sync)
            Dirty(uid, humanoid);
    }
    
    private void OnBodyPartDamage(EntityUid uid, BodyPartComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || component.PartType != BodyPartType.Head || component.Body is not { } body)
            return;

        if (!TryComp<ScreenSaverComponent>(body, out var screenSaver)
            || !TryComp<HumanoidAppearanceComponent>(body, out var humanoid)
            || !_mobState.IsAlive(body))
            return;

        if (_ipcFaceMarkings == null)
            PopulateCache();

        if (_ipcFaceMarkings!.Count == 0)
            return;

        var randomMarking = _random.Pick(_ipcFaceMarkings);
        ReplaceScreenMarking(body, randomMarking.ID, screenSaver, humanoid);
    }
}
