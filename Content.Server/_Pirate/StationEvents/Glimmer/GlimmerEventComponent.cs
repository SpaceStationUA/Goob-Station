namespace Content.Server.Psionics.Glimmer;

[RegisterComponent]
public sealed partial class GlimmerEventComponent : Component
{
    /// <summary>
    /// Minimum glimmer value for event to be eligible.
    /// </summary>
    [DataField]
    public int MinimumGlimmer = 100;

    /// <summary>
    /// Maximum glimmer value for event to be eligible.
    /// </summary>
    [DataField]
    public int MaximumGlimmer = 1000;

    /// <summary>
    /// Lower bound used when subtracting glimmer after the event.
    /// </summary>
    [DataField]
    public int GlimmerBurnLower = 25;

    /// <summary>
    /// Upper bound used when subtracting glimmer after the event.
    /// </summary>
    [DataField]
    public int GlimmerBurnUpper = 70;

    [DataField("report")]
    public string SophicReport = "glimmer-event-report-generic";
}
