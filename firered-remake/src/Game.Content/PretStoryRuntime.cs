using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Events;

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
    public const int CurrentSchemaVersion = 2;
    public static JsonSerializerOptions JsonOptions { get; } = new()
    { Converters = { new StoryProgramJsonConverter() } };

    public static PretStoryRuntime Load(string projectRoot)
    {
        var result = JsonSerializer.Deserialize<PretStoryRuntime>(File.ReadAllBytes(
            Path.Combine(projectRoot, "content/generated/data/story.json")), JsonOptions)
            ?? throw new InvalidDataException("Story runtime resource is empty.");
        if (result.SchemaVersion != CurrentSchemaVersion || result.Compilation.Programs.Count == 0 ||
            result.TextBank.Count == 0 || result.NicknameCharacters.Length < 60)
            throw new InvalidDataException("Story runtime resource schema or content is invalid.");
        foreach (var (id, menu) in result.Compilation.Menus)
        {
            if (menu is null || menu.Id != id || !result.Compilation.TryConstant(id, out int sourceId) ||
                sourceId != menu.SourceId || sourceId is < 0 or > 255 || menu.Options.IsDefaultOrEmpty ||
                menu.Options.Length > StoryMenu.CancelResult || menu.ColumnWidthTiles is < 2 or > 28 ||
                menu.Options.Any(o => o is null || string.IsNullOrWhiteSpace(o.TextSymbol) || string.IsNullOrWhiteSpace(o.Text)))
                throw new InvalidDataException("Invalid story menu definition: " + id);
        }
        foreach (var instruction in result.Compilation.Programs.Values.SelectMany(p => p.Instructions))
        {
            if (StoryMenuRules.HasDefinition(instruction, result.Compilation) && StoryMenuRules.Resolve(instruction, result.Compilation) is null)
                throw new InvalidDataException("Invalid story menu operands: " + instruction.Source);
        }
        return result;
    }
}
