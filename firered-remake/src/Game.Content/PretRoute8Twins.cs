using System.Text;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Separate, authenticated initial Twins definition. Existing ordinary trainer catalogs stay bounded.</summary>
public static class PretRoute8Twins
{
    public const string GeneratedPath = "content/generated/route8-twins/trainer.json";
    public const string GeneratedSha256 = "d512ad56aeb5851c26df61cb59dde314ac0e6801dd51828e6bf0f43283083c2b";
    public static SourceTwinsTrainerDefinition Load(string projectRoot) => Load(projectRoot, projectRoot);
    public static SourceTwinsTrainerDefinition Load(string definitionRoot, string sourceRoot)
    {
        // The original 23-file inventory already includes this trainer, its source party,
        // both interactions, original charmap, generation and encounter-music functions.
        var source = PretCeladonApproachSource.Load(sourceRoot, "celadon-approach-trainers",
            PretCeladonApproachTrainers.Profile, PretCeladonApproachTrainers.LockSha256, 23);
        var bytes = PretCeladonApproachSource.Read(definitionRoot, GeneratedPath, GeneratedSha256, 3187);
        var definition = ContentJson.ParseStrict<SourceTwinsTrainerDefinition>(Encoding.UTF8.GetString(bytes), ContentJson.CreateOptions());
        if (SourceTwinsTrainerEncounter.ValidateDefinition(definition) is { } error) throw new InvalidDataException(error);
        void Check(TrainerSourceRef s) => source.Reference(s.Path, s.Sha256, s.StartLine, s.EndLine, s.Symbol);
        Check(definition.Trainer.Source); Check(definition.Trainer.MoneySource);
        foreach (var p in definition.Trainer.Party) { Check(p.Source); Check(p.SpeciesNameSource); }
        foreach (var b in definition.Interactions) Check(b.Source);
        return definition;
    }
}
