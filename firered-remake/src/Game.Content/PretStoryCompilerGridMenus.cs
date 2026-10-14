using System.Text.RegularExpressions;
namespace Game.Content;

public static partial class PretStoryCompiler
{
    private static bool IsEmptySourceMapHeader(PretSourceScriptFile file, string label, string[] body,
        IReadOnlyDictionary<string, string>? authenticatedMapScriptHashes)
    {
        // The mounting package authenticates this inventory against its pinned Git/source lock.
        // A matching self-declared SourceRef hash alone is insufficient for new data-table admission.
        if (!IsAuthenticatedMapScriptSource(file, authenticatedMapScriptHashes) ||
            body.Length != 1 || body[0] != ".byte 0") return false;
        var map = Regex.Match(file.Path, @"^data/maps/([A-Za-z0-9_]+)/scripts\.inc$");
        return map.Success && label == map.Groups[1].Value + "_MapScripts";
    }

    private static bool IsAuthenticatedMapScriptSource(PretSourceScriptFile file,
        IReadOnlyDictionary<string, string>? authenticatedMapScriptHashes) =>
        file.SourceRef.Repository == "pret/pokefirered" && file.SourceRef.Commit == PretWorldMap.SourceCommit &&
        authenticatedMapScriptHashes is not null && authenticatedMapScriptHashes.TryGetValue(file.Path, out var hash) &&
        hash == file.SourceRef.Sha256 && Regex.IsMatch(file.Path, @"^data/maps/[A-Za-z0-9_]+/scripts\.inc$");
}
