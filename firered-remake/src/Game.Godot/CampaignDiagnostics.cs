/// <summary>Presentation-only diagnostics; explicit evidence capture retains the full-state/hash format.</summary>
internal sealed class CampaignDiagnostics(string? evidencePath, string? verbose)
{
    public bool Verbose { get; } = !string.IsNullOrEmpty(evidencePath) || verbose == "1";

    // Keep full-state serialization AND hashing inside detail; neither belongs in
    // summary or a lazy property that JsonSerializer could evaluate implicitly.
    public object Payload(object summary, Func<object> detail) => Verbose ? detail() : summary;
}
