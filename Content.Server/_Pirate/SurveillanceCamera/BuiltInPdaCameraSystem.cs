// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._Pirate.SurveillanceCamera;

public sealed class BuiltInPdaCameraSystem : EntitySystem
{
    public const string CameraContainerId = "built-in-pda-camera";

    [Dependency] private readonly ContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BuiltInPdaCameraComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BuiltInPdaCameraComponent> ent, ref MapInitEvent args)
    {
        var container = _containers.EnsureContainer<Container>(ent, CameraContainerId);
        if (container.ContainedEntities.Count != 0)
            return;

        var camera = Spawn(ent.Comp.CameraPrototype, Transform(ent).Coordinates);
        if (!_containers.Insert(camera, container))
            QueueDel(camera);
    }
}
