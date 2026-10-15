using System.Text;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Separate, authenticated initial Twins definition. Existing ordinary trainer catalogs stay bounded.</summary>
public static class PretRoute8Twins
{
    public const string GeneratedPath = "content/generated/route8-twins/trainer.json";
    public static SourceTwinsTrainerDefinition Load(string projectRoot) => Load(projectRoot, projectRoot);
    public static SourceTwinsTrainerDefinition Load(string definitionRoot, string sourceRoot)
    {
        var bytes = PretCeladonApproachSource.Read(definitionRoot,GeneratedPath);
        var definition = ContentJson.ParseStrict<SourceTwinsTrainerDefinition>(Encoding.UTF8.GetString(bytes), ContentJson.CreateOptions());
        if (SourceTwinsTrainerEncounter.ValidateDefinition(definition) is { } error) throw new InvalidDataException(error);
        return definition;
    }
}
