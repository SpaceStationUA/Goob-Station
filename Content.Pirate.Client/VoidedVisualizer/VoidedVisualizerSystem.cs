// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Graphics;
using Content.Pirate.Shared.Voidwalker;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.VoidedVisualizer;

public sealed class VoidedVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string PostShaderId = "pirate-voidwalker";

    private readonly ProtoId<ShaderPrototype> _shaderId = "VoidedShader";
    private ShaderPrototype? _shaderProto;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidedVisualsComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VoidedVisualsComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentStartup(EntityUid uid, VoidedVisualsComponent component, ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(PostShaderId, (_shaderProto ??= _protoMan.Index(_shaderId)).InstanceUnique())
            {
                Before = ContentPostShaderIds.BeforeOutlines,
            });
    }

    private void OnComponentShutdown(EntityUid uid, VoidedVisualsComponent component, ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.RemovePostShader((uid, sprite), PostShaderId);
    }
}
