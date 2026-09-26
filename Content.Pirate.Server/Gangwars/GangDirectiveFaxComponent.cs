using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Gangwars;

/// <summary>Faxes Central Command's gang-response directive to command and security shortly after the rule starts.</summary>
[RegisterComponent, Access(typeof(GangDirectiveFaxSystem))]
public sealed partial class GangDirectiveFaxComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan SendAt;

    [DataField]
    public bool Sent;

    /// <summary>The paper prototype supplies the directive content and stamps.</summary>
    [DataField]
    public EntProtoId Paper = "PaperCentcomGangDirective";

    /// <summary>Case-sensitive fax-name substrings identifying command and security machines.</summary>
    [DataField]
    public List<string> FaxKeywords = new()
    {
        "Captain",
        "captain",
        "Капітан",
        "капітан",
        "HoS",
        "Head of Security",
        "Head of security",
        "Security",
        "Безпек",
        "Warden",
        "Наглядач",
        "Brig",
        "Бриг",
        "Bridge",
        "bridge",
        "Місток",
    };
}
