using System;
using System.Threading;
using Content.Shared._Pirate.Furniture.Tables.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Furniture.Tables;

public sealed class RouletteSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RouletteComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<RouletteComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, RouletteComponent component, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (component.State == RouletteState.Result)
        {
            var color = GetResultColor(component.Result);
            args.PushMarkup(Loc.GetString("roulette-examine-result", ("number", component.Result), ("color", color)));
        }
        else if (component.State == RouletteState.Rolling)
        {
            args.PushMarkup(Loc.GetString("roulette-examine-rolling"));
        }
    }

    private void OnActivate(EntityUid uid, RouletteComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (component.State == RouletteState.Rolling)
        {
            _popup.PopupEntity(Loc.GetString("roulette-already-rolling"), uid, args.User);
            return;
        }

        args.Handled = true;
        component.CancellationTokenSource?.Cancel();
        component.CancellationTokenSource = new CancellationTokenSource();
        component.Result = _random.Next(0, 37);
        component.State = RouletteState.Rolling;
        _appearance.SetData(uid, RouletteVisuals.State, RouletteState.Rolling);
        Dirty(uid, component);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Pirate/Furniture/roulette-wheel-throw.ogg"), uid,
            new AudioParams { Volume = -8f, MaxDistance = 7f, RolloffFactor = 2f, ReferenceDistance = 0.5f });

        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(9), () =>
        {
            if (!TerminatingOrDeleted(uid))
                SetResult(uid, component);
        }, component.CancellationTokenSource.Token);
    }

    private void SetResult(EntityUid uid, RouletteComponent component)
    {
        component.State = RouletteState.Result;
        _appearance.SetData(uid, RouletteVisuals.State, RouletteState.Result);
        Dirty(uid, component);

        _popup.PopupEntity(
            Loc.GetString("roulette-popup-result",
                ("number", component.Result),
                ("color", GetResultColorName(component.Result))),
            uid);
    }

    private static string GetResultColor(int result)
    {
        return result switch
        {
            0 => "green",
            1 or 3 or 5 or 7 or 9 or 12 or 14 or 16 or 18 or 19 or 21 or 23 or 25 or 27 or 30 or 32 or 34 or 36 => "red",
            _ => "#BFBFBF"
        };
    }

    private string GetResultColorName(int result)
    {
        return Loc.GetString(result switch
        {
            0 => "roulette-color-green",
            1 or 3 or 5 or 7 or 9 or 12 or 14 or 16 or 18 or 19 or 21 or 23 or 25 or 27 or 30 or 32 or 34 or 36 => "roulette-color-red",
            _ => "roulette-color-black"
        });
    }
}
