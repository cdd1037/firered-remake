using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;

namespace Game.Tooling;

/// <summary>Explicit operator-supplied lifetime count. Never estimates a total from surviving Pokemon.</summary>
internal static class KnownHatchCountConversion
{
    private const string Stat = "GAME_STAT_HATCHED_EGGS";

    public static int Run(string root, string sourcePath, string destinationPath, int knownCount)
    {
        if (knownCount is < 0 or > 0xffffff)
            throw new ArgumentOutOfRangeException(nameof(knownCount), "Known lifetime hatch count must be 0..16777215.");
        string input = Path.GetFullPath(sourcePath), output = Path.GetFullPath(destinationPath);
        if (string.Equals(input, output, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("hatch_count_conversion_requires_new_destination");
        byte[] original = File.ReadAllBytes(input);
        var catalog = PretCampaignCatalog.LoadCurrent(root);
        var source = SaveCodec.Decode<CampaignState>(original, CurrentGameProfile.RulesVersion, catalog.ContentVersion, input);
        if (source.State.GameStats?.ContainsKey(Stat) == true)
            throw new InvalidDataException("hatch_count_already_present");
        var stats = source.State.GameStats ?? ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);
        var current = source.State with { GameStats = stats.Add(Stat, knownCount) };
        var validator = CampaignSession.CreateCurrent(catalog);
        if (validator.ValidatePlayerSave(current) is {} error)
            throw new InvalidDataException("hatch_count_conversion_invalid: " + error);
        byte[] converted = SaveCodec.Encode(current, source.Generation, CurrentGameProfile.RulesVersion, catalog.ContentVersion);
        var verified = SaveCodec.Decode<CampaignState>(converted, CurrentGameProfile.RulesVersion, catalog.ContentVersion,
            validate: validator.ValidatePlayerSave);
        if (verified.Generation != source.Generation || current.World.Rng != source.State.World.Rng ||
            CanonicalJson.Hash(current with { GameStats = source.State.GameStats }) != CanonicalJson.Hash(source.State) ||
            !File.ReadAllBytes(input).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("hatch_count_conversion_changed_unrelated_state");
        NewCheckpointFile.Publish(output, converted);
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, input, output, generation = verified.Generation,
            knownHatchedEggs = knownCount, sourcePreserved = true,
            scope = "Explicit operator-provided lifetime total, not reconstructed history. Only the absent hatch statistic was added."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
