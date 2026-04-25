// SPDX-FileCopyrightText: 2026 CrazeTheDragon crazedragon@duck.com
// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.Audio;

namespace Content.Shared._Pirate.AI;

[RegisterComponent]
public sealed partial class AiRedPhoneComponent : Component
{
    [DataField("sentMessage")]
    public string SentMessage { get; private set; } = "prayer-popup-notify-centcom-sent";
    [DataField("notificationPrefix")]
    public string NotificationPrefix { get; private set; } = "prayer-chat-notify-centcom";
    [DataField("notificationSound")]
    public SoundSpecifier? NotificationSound { get; private set; }
}