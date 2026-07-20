// SPDX-License-Identifier: MIT

using Content.Server.DeviceNetwork.Components;
using Content.Shared._Pirate.ZLevels.Core.Components; // Pirate: multiz
using Content.Shared.DeviceNetwork.Events;
using JetBrains.Annotations;

namespace Content.Server.DeviceNetwork.Systems
{
    [UsedImplicitly]
    public sealed class WiredNetworkSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<WiredNetworkComponent, BeforePacketSentEvent>(OnBeforePacketSent);
        }

        /// <summary>
        /// Checks if both devices are on the same grid
        /// </summary>
        private void OnBeforePacketSent(EntityUid uid, WiredNetworkComponent component, BeforePacketSentEvent args)
        {
            var receiverGrid = Transform(uid).GridUid; // Pirate: multiz
            var senderGrid = args.SenderTransform.GridUid; // Pirate: multiz

            if (receiverGrid != senderGrid && !AreZLinkedGrids(senderGrid, receiverGrid)) // Pirate: multiz
            {
                args.Cancel();
            }
        }

        #region Pirate: multiz
        private bool AreZLinkedGrids(EntityUid? senderGrid, EntityUid? receiverGrid)
        {
            if (senderGrid == null || receiverGrid == null)
                return false;

            return TryComp<CEZLinkedGridComponent>(senderGrid.Value, out var senderLinked)
                   && TryComp<CEZLinkedGridComponent>(receiverGrid.Value, out var receiverLinked)
                   && senderLinked.ZNetwork.IsValid()
                   && senderLinked.ZNetwork == receiverLinked.ZNetwork;
        }
        #endregion

        //Things to do in a future PR:
        //Abstract out the connection between the apcExtensionCable and the apcPowerReceiver
        //Traverse the power cables using path traversal
        //Cache an optimized representation of the traversed path (Probably just cache Devices)
    }
}