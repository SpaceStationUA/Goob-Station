// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics; // Pirate: multiz
using Content.Server.DeviceNetwork.Components;
using Content.Shared._Pirate.ZLevels.Core.Components; // Pirate: multiz
using Content.Shared.DeviceNetwork.Events;
using JetBrains.Annotations;

namespace Content.Server.DeviceNetwork.Systems
{
    [UsedImplicitly]
    public sealed class WirelessNetworkSystem : EntitySystem
    {
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<WirelessNetworkComponent, BeforePacketSentEvent>(OnBeforePacketSent);
        }

        /// <summary>
        /// Gets the position of both the sending and receiving entity and checks if the receiver is in range of the sender.
        /// </summary>
        private void OnBeforePacketSent(EntityUid uid, WirelessNetworkComponent component, BeforePacketSentEvent args)
        {
            var ownPosition = args.SenderPosition;
            var xform = Transform(uid);

            // not a wireless to wireless connection, just let it happen
            if (!TryComp<WirelessNetworkComponent>(args.Sender, out var sendingComponent))
                return;

            #region Pirate: multiz
            if (args.SenderTransform.MapID == xform.MapID)
            {
                if ((ownPosition - _transformSystem.GetWorldPosition(xform)).Length() > sendingComponent.Range)
                    args.Cancel();
                return;
            }

            if (!TryGetZStackDistance(args.SenderTransform, ownPosition, xform, out var stackedDistance)
                || stackedDistance > sendingComponent.Range)
            {
                args.Cancel();
            }
            #endregion
        }

        #region Pirate: multiz
        private bool TryGetZStackDistance(TransformComponent senderXform, Vector2 senderWorldPos, TransformComponent receiverXform, out float distance)
        {
            distance = 0f;

            if (senderXform.GridUid is not { } senderGrid || receiverXform.GridUid is not { } receiverGrid)
                return false;

            if (!TryComp<CEZLinkedGridComponent>(senderGrid, out var senderLinked)
                || !TryComp<CEZLinkedGridComponent>(receiverGrid, out var receiverLinked)
                || !senderLinked.ZNetwork.IsValid()
                || senderLinked.ZNetwork != receiverLinked.ZNetwork)
                return false;

            // Compare grid-local positions for linked decks on separate maps.
            var senderLocal = Vector2.Transform(senderWorldPos, _transformSystem.GetInvWorldMatrix(senderGrid));
            var receiverLocal = Vector2.Transform(_transformSystem.GetWorldPosition(receiverXform), _transformSystem.GetInvWorldMatrix(receiverGrid));
            distance = (senderLocal - receiverLocal).Length();
            return true;
        }
        #endregion
    }
}