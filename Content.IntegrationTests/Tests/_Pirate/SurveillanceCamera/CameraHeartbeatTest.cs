// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Goobstation.Common.SurveillanceCamera;
using Content.IntegrationTests.Tests.DeviceNetwork;
using Content.Server.SurveillanceCamera;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Pirate.SurveillanceCamera;

[TestFixture]
public sealed class CameraHeartbeatTest
{
    [Test]
    public async Task SendsOneHeartbeatPerCameraPerSecondAndExpiresSilentCameras()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var receiver = server.System<DeviceNetworkTestSystem>();
        EntityUid router = default;
        EntityUid monitor = default;
        var counts = new Dictionary<string, int>();

        void OnPacket(EntityUid uid, DeviceNetworkPacketEvent args)
        {
            if (uid == router &&
                args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? command) &&
                command == SurveillanceCameraSystem.CameraHeartbeatMessage &&
                args.Data.TryGetValue(SurveillanceCameraSystem.CameraAddressData, out string? address) && address != null)
                counts[address] = counts.GetValueOrDefault(address) + 1;
        }

        await server.WaitPost(() =>
        {
            router = entities.SpawnEntity("DummyNetworkDevice", map.GridCoords);
            monitor = entities.SpawnEntity("DummyNetworkDevice", map.GridCoords);
            var camera = entities.SpawnEntity(null, map.GridCoords);
            var component = entities.EnsureComponent<SurveillanceCameraMonitorComponent>(monitor);
            var location = (entities.GetNetEntity(camera), entities.GetNetCoordinates(map.GridCoords));
#pragma warning disable RA0002 // Set up one active mobile camera and a second mobile camera in the same subnet.
            component.KnownSubnets.Add("test", entities.GetComponent<DeviceNetworkComponent>(router).Address);
            component.ActiveCamera = camera;
            component.ActiveCameraAddress = "active";
            component.KnownMobileCameras.Add("active", ("Active", location));
            component.KnownMobileCameras.Add("mobile", ("Mobile", location));
#pragma warning restore RA0002
            entities.EnsureComponent<ActiveSurveillanceCameraMonitorComponent>(monitor);
            entities.EnsureComponent<HasMobileCamerasSurveillanceCameraMonitorComponent>(monitor);
            receiver.PacketReceived += OnPacket;
        });

        try
        {
            var period = server.Timing.TickPeriod.TotalSeconds;
            await pair.RunTicksSync((int) Math.Ceiling(2.3 / period));
            await server.WaitAssertion(() =>
            {
                Assert.That(counts.GetValueOrDefault("active"), Is.EqualTo(2));
                Assert.That(counts.GetValueOrDefault("mobile"), Is.EqualTo(2));
            });

            await pair.RunTicksSync((int) Math.Ceiling(1.2 / period));
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<ActiveSurveillanceCameraMonitorComponent>(monitor), Is.False);
                Assert.That(entities.GetComponent<SurveillanceCameraMonitorComponent>(monitor).KnownMobileCameras, Is.Empty);
            });
        }
        finally
        {
            await server.WaitPost(() => receiver.PacketReceived -= OnPacket);
        }

        await pair.CleanReturnAsync();
    }
}
