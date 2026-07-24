// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Supermatter.Monitor;
using Robust.Shared.Audio;

namespace Content.Pirate.Server.Supermatter;

[RegisterComponent]
public sealed partial class PirateSupermatterSoundComponent : Component
{
    [DataField]
    public SoundSpecifier CalmLoopSound = new SoundPathSpecifier("/Audio/_Goobstation/Supermatter/calm.ogg");

    [DataField]
    public SoundSpecifier DelamLoopSound = new SoundPathSpecifier("/Audio/_Goobstation/Supermatter/delamming.ogg");

    [DataField]
    public SoundSpecifier CalmAccent = new SoundCollectionSpecifier("PirateSupermatterAccentNormal");

    [DataField]
    public SoundSpecifier DelamAccent = new SoundCollectionSpecifier("PirateSupermatterAccentDelam");

    [DataField]
    public SoundSpecifier DistortSound = new SoundPathSpecifier("/Audio/_Pirate/Supermatter/charge.ogg");

    [DataField]
    public SoundSpecifier EmergencyStatusSound =
        new SoundPathSpecifier("/Audio/_Pirate/Supermatter/status/engine_alert1.ogg");

    [DataField]
    public SoundSpecifier DelamStatusSound =
        new SoundPathSpecifier("/Audio/_Pirate/Supermatter/status/ohfuck.ogg");

    [DataField]
    public string WarningSpeechSound = "PirateSupermatterWarning";

    [DataField]
    public string DangerSpeechSound = "PirateSupermatterDanger";

    [DataField]
    public string EmergencySpeechSound = "PirateSupermatterEmergency";

    [DataField]
    public string DelamSpeechSound = "PirateSupermatterDelaminating";

    [DataField]
    public string[] LightningPrototypes =
    [
        "PirateSupermatterLightning",
        "PirateSupermatterLightningCharged",
        "PirateSupermatterLightningSupercharged",
        "PirateSupermatterLightningHypercharged",
    ];

    [DataField]
    public float DangerPoint = 300f;

    [DataField]
    public float AccentMinCooldown = 2f;

    [DataField]
    public float SoundRange = 20f;

    [DataField]
    public float InitialVolume = -5f;

    [DataField]
    public TimeSpan SoundUpdateInterval = TimeSpan.FromSeconds(1);

    public TimeSpan NextSoundUpdate;
    public TimeSpan AccentLastTime;
    public SupermatterStatusType? LastStatus;
    public bool DistortPlayed;
}
