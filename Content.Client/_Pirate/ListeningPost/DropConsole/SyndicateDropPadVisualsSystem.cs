// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.ListeningPost.DropConsole;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Pirate.ListeningPost.DropConsole;

public sealed class SyndicateDropPadVisualsSystem : EntitySystem
{
    private const string IdleKey = "syndicate-drop-pad-idle";
    private const string BeamKey = "syndicate-drop-pad-beam";

    private static readonly Animation IdleAnimation = new()
    {
        Length = TimeSpan.FromSeconds(0.6),
        AnimationTracks =
        {
            new AnimationTrackSpriteFlick
            {
                LayerKey = SyndicateDropPadLayers.Beam,
                KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId("lpad-idle"), 0f) },
            },
        },
    };

    private static readonly Animation BeamAnimation = new()
    {
        Length = TimeSpan.FromSeconds(1),
        AnimationTracks =
        {
            new AnimationTrackSpriteFlick
            {
                LayerKey = SyndicateDropPadLayers.Beam,
                KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId("lpad-beam"), 0f) },
            },
        },
    };

    [Dependency] private readonly AnimationPlayerSystem _player = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SyndicateDropPadComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<SyndicateDropPadComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnAppearanceChange(Entity<SyndicateDropPadComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateSprite(ent, args.Sprite);
    }

    private void OnAnimationCompleted(Entity<SyndicateDropPadComponent> ent, ref AnimationCompletedEvent args)
    {
        UpdateSprite(ent);
    }

    private void UpdateSprite(Entity<SyndicateDropPadComponent> ent, SpriteComponent? sprite = null)
    {
        if (!Resolve(ent.Owner, ref sprite))
            return;

        if (!TryComp<AnimationPlayerComponent>(ent, out var player))
            return;

        _appearance.TryGetData<SyndicateDropPadState>(ent, SyndicateDropPadVisuals.State, out var state);

        switch (state)
        {
            case SyndicateDropPadState.Sending:
                _player.Stop((ent.Owner, player), IdleKey);

                if (!_player.HasRunningAnimation(ent.Owner, BeamKey))
                    _player.Play((ent.Owner, player), BeamAnimation, BeamKey);

                break;

            case SyndicateDropPadState.Unpowered:
                _sprite.LayerSetVisible((ent.Owner, sprite), SyndicateDropPadLayers.Beam, false);
                _player.Stop((ent.Owner, player), BeamKey);
                _player.Stop((ent.Owner, player), IdleKey);
                break;

            default:
                _sprite.LayerSetVisible((ent.Owner, sprite), SyndicateDropPadLayers.Beam, true);

                if (_player.HasRunningAnimation(ent.Owner, IdleKey) ||
                    _player.HasRunningAnimation(ent.Owner, BeamKey))
                {
                    return;
                }

                _player.Play((ent.Owner, player), IdleAnimation, IdleKey);
                break;
        }
    }
}
