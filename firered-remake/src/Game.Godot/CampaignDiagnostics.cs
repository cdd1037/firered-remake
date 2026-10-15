/// <summary>Presentation-only diagnostics; evidence capture retains the established full-state format.</summary>
internal sealed class CampaignDiagnostics(string? evidencePath, string? verbose)
{
    public bool Verbose { get; } = !string.IsNullOrEmpty(evidencePath) || verbose == "1";

    // Do not construct or serialize full-state payloads in ordinary player sessions.
    public object Payload(object summary, Func<object> detail) => Verbose ? detail() : summary;
}
