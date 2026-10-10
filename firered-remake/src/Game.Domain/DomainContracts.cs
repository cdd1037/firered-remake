namespace Game.Domain;

public enum TransitionStatus { Accepted, Rejected, Fault }
public sealed record Diagnostic(string Code, string Message);

public static class RuleVersions
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    // An explicitly incomplete, versioned rules harness. Never load full FireRed content under this profile.
    public const string SingleBattleV0 = "fr-single-v0";
    public const string GridWorldV0 = "fr-grid-v0";
}
