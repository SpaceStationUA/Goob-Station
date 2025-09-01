// SPDX-FileCopyrightText: 2021 Paul Ritter <ritter.paul1@googlemail.com>
// SPDX-FileCopyrightText: 2021 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2022 20kdc <asdd2808@gmail.com>
// SPDX-FileCopyrightText: 2022 E F R <602406+Efruit@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr.@gmail.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <jmaster9999@gmail.com>
// SPDX-FileCopyrightText: 2022 Moony <moonheart08@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Paul <ritter.paul1@googlemail.com>
// SPDX-FileCopyrightText: 2022 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 wrexbe <wrexbe@protonmail.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 dffdff2423 <57052305+dffdff2423@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Pieter-Jan Briers <pieterjan.briers@gmail.com>
// SPDX-FileCopyrightText: 2025 Winkarst <74284083+Winkarst-cpu@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.Administration.Systems
{
    [UsedImplicitly]
    public sealed class BwoinkSystem : SharedBwoinkSystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;

        public event EventHandler<BwoinkTextMessage>? OnBwoinkTextMessageRecieved;
        private (TimeSpan Timestamp, bool Typing) _lastTypingUpdateSent;

        // Pirate Changes Start Here - Group chat events
        public event EventHandler<BwoinkGroupTextMessage>? OnBwoinkGroupTextMessageReceived;
        public event EventHandler<BwoinkGroupUpdateMessage>? OnBwoinkGroupUpdateReceived;
        public event EventHandler<BwoinkGroupListMessage>? OnBwoinkGroupListReceived;
        // Pirate Changes End Here

        protected override void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        {
            OnBwoinkTextMessageRecieved?.Invoke(this, message);
        }

        public void Send(NetUserId channelId, string text, bool playSound, bool adminOnly)
        {
            // Reuse the channel ID as the 'true sender'.
            // Server will ignore this and if someone makes it not ignore this (which is bad, allows impersonation!!!), that will help.
            RaiseNetworkEvent(new BwoinkTextMessage(channelId, channelId, text, playSound: playSound, adminOnly: adminOnly));
            SendInputTextUpdated(channelId, false);
        }

        public void SendInputTextUpdated(NetUserId channel, bool typing)
        {
            if (_lastTypingUpdateSent.Typing == typing &&
                _lastTypingUpdateSent.Timestamp + TimeSpan.FromSeconds(1) > _timing.RealTime)
            {
                return;
            }

            _lastTypingUpdateSent = (_timing.RealTime, typing);
            RaiseNetworkEvent(new BwoinkClientTypingUpdated(channel, typing));
        }

        // Pirate Changes Start Here - Group chat methods
        protected override void OnBwoinkGroupTextMessage(BwoinkGroupTextMessage message, EntitySessionEventArgs eventArgs)
        {
            OnBwoinkGroupTextMessageReceived?.Invoke(this, message);
        }

        protected override void OnBwoinkGroupUpdate(BwoinkGroupUpdateMessage message, EntitySessionEventArgs eventArgs)
        {
            OnBwoinkGroupUpdateReceived?.Invoke(this, message);
        }

        protected override void OnBwoinkGroupList(BwoinkGroupListMessage message, EntitySessionEventArgs eventArgs)
        {
            OnBwoinkGroupListReceived?.Invoke(this, message);
        }

        public void CreateGroup(string groupName, List<NetUserId> initialMembers)
        {
            var groupId = Guid.NewGuid();
            RaiseNetworkEvent(new BwoinkCreateGroupMessage(groupId, groupName, initialMembers));
        }

        public void AddToGroup(Guid groupId, NetUserId userId)
        {
            RaiseNetworkEvent(new BwoinkAddToGroupMessage(groupId, userId));
        }

        public void RemoveFromGroup(Guid groupId, NetUserId userId)
        {
            RaiseNetworkEvent(new BwoinkRemoveFromGroupMessage(groupId, userId));
        }

        public void DeleteGroup(Guid groupId)
        {
            RaiseNetworkEvent(new BwoinkDeleteGroupMessage(groupId));
        }

        // Pirate Changes Start Here - Group rename method
        public void RenameGroup(Guid groupId, string newName)
        {
            RaiseNetworkEvent(new BwoinkRenameGroupMessage(groupId, newName));
        }
        // Pirate Changes End Here

        public void SendGroupMessage(Guid groupId, string text, bool playSound = true)
        {
            // Server will set the correct sender ID
            RaiseNetworkEvent(new BwoinkGroupTextMessage(groupId, default, text, playSound: playSound));
        }

        public void MutePlayer(NetUserId userId, TimeSpan duration)
        {
            RaiseNetworkEvent(new BwoinkMutePlayerMessage(userId, duration));
        }

        public void UnmutePlayer(NetUserId userId)
        {
            RaiseNetworkEvent(new BwoinkUnmutePlayerMessage(userId));
        }
        // Pirate Changes End Here
    }
}
