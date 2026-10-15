using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>Executable story IR and display strings, produced only by the import tool.</summary>
public sealed record PretStoryRuntime(
    int SchemaVersion,
    PretStoryCompilation Compilation,
    ImmutableDictionary<string, string> TextBank,
    ImmutableSortedDictionary<int, string> StarterSpeciesNames,
    string NicknameCharacters,
    ImmutableSortedDictionary<string, string> MoveNames)
{
    public static PretStoryRuntime Load(string projectRoot)
    {
        var result = JsonSerializer.Deserialize<PretStoryRuntime>(File.ReadAllBytes(
            Path.Combine(projectRoot, "content/generated/story/runtime.json")))
            ?? throw new InvalidDataException("Story runtime resource is empty.");
        if (result.SchemaVersion != 1 || result.Compilation.Programs.Count == 0 ||
            result.TextBank.Count == 0 || result.NicknameCharacters.Length < 60)
            throw new InvalidDataException("Story runtime resource schema or content is invalid.");
        return result;
    }
}
