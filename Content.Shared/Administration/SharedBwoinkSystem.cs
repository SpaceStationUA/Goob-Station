// SPDX-FileCopyrightText: 2021 20kdc <asdd2808@gmail.com>
// SPDX-FileCopyrightText: 2021 Paul Ritter <ritter.paul1@googlemail.com>
// SPDX-FileCopyrightText: 2022 E F R <602406+Efruit@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr.@gmail.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <jmaster9999@gmail.com>
// SPDX-FileCopyrightText: 2022 Visne <39844191+Visne@users.noreply.github.com>
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
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    public abstract class SharedBwoinkSystem : EntitySystem
    {
        // System users
        public static NetUserId SystemUserId { get; } = new NetUserId(Guid.Empty);

        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<BwoinkTextMessage>(OnBwoinkTextMessage);

            // Group chat events
            SubscribeNetworkEvent<BwoinkCreateGroupMessage>(OnBwoinkCreateGroup);
            SubscribeNetworkEvent<BwoinkAddToGroupMessage>(OnBwoinkAddToGroup);
            SubscribeNetworkEvent<BwoinkRemoveFromGroupMessage>(OnBwoinkRemoveFromGroup);
            SubscribeNetworkEvent<BwoinkDeleteGroupMessage>(OnBwoinkDeleteGroup);
            SubscribeNetworkEvent<BwoinkRenameGroupMessage>(OnBwoinkRenameGroup); // Pirate Changes
            SubscribeNetworkEvent<BwoinkGroupTextMessage>(OnBwoinkGroupTextMessage);
            SubscribeNetworkEvent<BwoinkGroupUpdateMessage>(OnBwoinkGroupUpdate);
            SubscribeNetworkEvent<BwoinkGroupListMessage>(OnBwoinkGroupList);
        }

        protected virtual void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected virtual void OnBwoinkCreateGroup(BwoinkCreateGroupMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected virtual void OnBwoinkAddToGroup(BwoinkAddToGroupMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected virtual void OnBwoinkRemoveFromGroup(BwoinkRemoveFromGroupMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected virtual void OnBwoinkDeleteGroup(BwoinkDeleteGroupMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        // Pirate Changes Start Here
        protected virtual void OnBwoinkRenameGroup(BwoinkRenameGroupMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }
        // Pirate Changes End Here

        protected virtual void OnBwoinkGroupTextMessage(BwoinkGroupTextMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected virtual void OnBwoinkGroupUpdate(BwoinkGroupUpdateMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected virtual void OnBwoinkGroupList(BwoinkGroupListMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected virtual void OnBwoinkMutePlayer(BwoinkMutePlayerMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected virtual void OnBwoinkUnmutePlayer(BwoinkUnmutePlayerMessage message, EntitySessionEventArgs eventArgs)
        {
            // Specific side code in target.
        }

        protected void LogBwoink(BwoinkTextMessage message)
        {
        }

        [Serializable, NetSerializable]
        public sealed class BwoinkTextMessage : EntityEventArgs
        {
            public DateTime SentAt { get; }

            public NetUserId UserId { get; }

            // This is ignored from the client.
            // It's checked by the client when receiving a message from the server for bwoink noises.
            // This could be a boolean "Incoming", but that would require making a second instance.
            public NetUserId TrueSender { get; }
            public string Text { get; }

            public bool PlaySound { get; }

            public readonly bool AdminOnly;

            public BwoinkTextMessage(NetUserId userId, NetUserId trueSender, string text, DateTime? sentAt = default, bool playSound = true, bool adminOnly = false)
            {
                SentAt = sentAt ?? DateTime.Now;
                UserId = userId;
                TrueSender = trueSender;
                Text = text;
                PlaySound = playSound;
                AdminOnly = adminOnly;
            }
        }
    }

    /// <summary>
    ///     Sent by the server to notify all clients when the webhook url is sent.
    ///     The webhook url itself is not and should not be sent.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkDiscordRelayUpdated : EntityEventArgs
    {
        public bool DiscordRelayEnabled { get; }

        public BwoinkDiscordRelayUpdated(bool enabled)
        {
            DiscordRelayEnabled = enabled;
        }
    }

    /// <summary>
    ///     Sent by the client to notify the server when it begins or stops typing.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkClientTypingUpdated : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public bool Typing { get; }

        public BwoinkClientTypingUpdated(NetUserId channel, bool typing)
        {
            Channel = channel;
            Typing = typing;
        }
    }

    /// <summary>
    ///     Sent by server to notify admins when a player begins or stops typing.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkPlayerTypingUpdated : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public string PlayerName { get; }
        public bool Typing { get; }

        public BwoinkPlayerTypingUpdated(NetUserId channel, string playerName, bool typing)
        {
            Channel = channel;
            PlayerName = playerName;
            Typing = typing;
        }
    }

    // Group Chat Messages

    /// <summary>
    ///     Sent by admin to create a new group chat.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkCreateGroupMessage : EntityEventArgs
    {
        public Guid GroupId { get; }
        public string GroupName { get; }
        public List<NetUserId> InitialMembers { get; }

        public BwoinkCreateGroupMessage(Guid groupId, string groupName, List<NetUserId> initialMembers)
        {
            GroupId = groupId;
            GroupName = groupName;
            InitialMembers = initialMembers;
        }
    }

    /// <summary>
    ///     Sent by admin to add a player to a group chat.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkAddToGroupMessage : EntityEventArgs
    {
        public Guid GroupId { get; }
        public NetUserId UserId { get; }

        public BwoinkAddToGroupMessage(Guid groupId, NetUserId userId)
        {
            GroupId = groupId;
            UserId = userId;
        }
    }

    /// <summary>
    ///     Sent by admin to remove a player from a group chat.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkRemoveFromGroupMessage : EntityEventArgs
    {
        public Guid GroupId { get; }
        public NetUserId UserId { get; }

        public BwoinkRemoveFromGroupMessage(Guid groupId, NetUserId userId)
        {
            GroupId = groupId;
            UserId = userId;
        }
    }

    /// <summary>
    ///     Sent by admin to delete a group chat entirely.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkDeleteGroupMessage : EntityEventArgs
    {
        public Guid GroupId { get; }

        public BwoinkDeleteGroupMessage(Guid groupId)
        {
            GroupId = groupId;
        }
    }

    // Pirate Changes Start Here - Group rename message
    /// <summary>
    ///     Sent by admin to rename a group chat.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkRenameGroupMessage : EntityEventArgs
    {
        public Guid GroupId { get; }
        public string NewName { get; }

        public BwoinkRenameGroupMessage(Guid groupId, string newName)
        {
            GroupId = groupId;
            NewName = newName;
        }
    }
    // Pirate Changes End Here

    /// <summary>
    ///     Sent to send a message to a group chat.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkGroupTextMessage : EntityEventArgs
    {
        public DateTime SentAt { get; }
        public Guid GroupId { get; }
        public NetUserId SenderId { get; }
        public string Text { get; }
        public bool PlaySound { get; }

        public BwoinkGroupTextMessage(Guid groupId, NetUserId senderId, string text, DateTime? sentAt = default, bool playSound = true)
        {
            SentAt = sentAt ?? DateTime.Now;
            GroupId = groupId;
            SenderId = senderId;
            Text = text;
            PlaySound = playSound;
        }
    }

    /// <summary>
    ///     Sent by server to notify clients about group chat updates.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkGroupUpdateMessage : EntityEventArgs
    {
        public Guid GroupId { get; }
        public string GroupName { get; }
        public List<NetUserId> Members { get; }
        public bool IsDeleted { get; }

        public BwoinkGroupUpdateMessage(Guid groupId, string groupName, List<NetUserId> members, bool isDeleted = false)
        {
            GroupId = groupId;
            GroupName = groupName;
            Members = members;
            IsDeleted = isDeleted;
        }
    }

    /// <summary>
    ///     Sent by server to notify clients about all existing group chats they have access to.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkGroupListMessage : EntityEventArgs
    {
        public List<BwoinkGroupInfo> Groups { get; }

        public BwoinkGroupListMessage(List<BwoinkGroupInfo> groups)
        {
            Groups = groups;
        }
    }

    /// <summary>
    ///     Sent by client to mute a player in group chats.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkMutePlayerMessage : EntityEventArgs
    {
        public NetUserId UserId { get; }
        public TimeSpan MuteDuration { get; }

        public BwoinkMutePlayerMessage(NetUserId userId, TimeSpan muteDuration)
        {
            UserId = userId;
            MuteDuration = muteDuration;
        }
    }

    /// <summary>
    ///     Sent by client to unmute a player in group chats.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkUnmutePlayerMessage : EntityEventArgs
    {
        public NetUserId UserId { get; }

        public BwoinkUnmutePlayerMessage(NetUserId userId)
        {
            UserId = userId;
        }
    }

    /// <summary>
    ///     Information about a group chat.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BwoinkGroupInfo
    {
        public Guid GroupId { get; }
        public string GroupName { get; }
        public List<NetUserId> Members { get; }
        public DateTime CreatedAt { get; }

        public BwoinkGroupInfo(Guid groupId, string groupName, List<NetUserId> members, DateTime createdAt)
        {
            GroupId = groupId;
            GroupName = groupName;
            Members = members;
            CreatedAt = createdAt;
        }
    }
}
