using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        string rules = CurrentCheckpointConversion.ReadRulesVersion(original);
        if (rules is not (CurrentCheckpointConversion.RetiredPokemonRules or CurrentCheckpointConversion.LegacyTowerRules or CurrentCheckpointConversion.BoundedTowerRules))
            throw new InvalidDataException("hatch_count_conversion_unsupported_route: expected campaign-v2, campaign-v3 or campaign-v4; rules are preserved");
        var catalog = PretCampaignCatalog.LoadCurrent(root);
        if (catalog.ContentVersion != CurrentCheckpointConversion.SupportedContent)
            throw new InvalidDataException("hatch_count_conversion_content_unsupported");
        // A v2 correction retains retired JSON properties in the output. Strip only
        // those declared v2 fields from a throwaway copy for typed semantic validation.
        LoadedSave<CampaignState> DecodeForValidation(byte[] bytes, Func<CampaignState, string?>? validate = null)
        {
            var raw = SaveCodec.Decode<JsonElement>(bytes, rules, CurrentCheckpointConversion.SupportedContent, input);
            if (raw.State.ValueKind != JsonValueKind.Object) throw new InvalidDataException("save_payload_invalid");
            var admission = JsonNode.Parse(raw.State.GetRawText())!.AsObject();
            if (rules == CurrentCheckpointConversion.RetiredPokemonRules)
            {
                admission.Remove("ReleasedPokemon");
                if (admission["HallOfFame"] is JsonObject hall) hall.Remove("RibbonRecipients");
            }
            return SaveCodec.Decode<CampaignState>(SaveCodec.Encode(admission, raw.Generation, rules,
                CurrentCheckpointConversion.SupportedContent), rules, CurrentCheckpointConversion.SupportedContent, input, validate);
        }
        var source = DecodeForValidation(original);
        if (source.State.GameStats?.ContainsKey(Stat) == true)
            throw new InvalidDataException("hatch_count_already_present");
        var stats = source.State.GameStats ?? ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);
        var current = source.State with { GameStats = stats.Add(Stat, knownCount) };
        var validator = CampaignSession.CreateCurrent(catalog);
        string? Validate(CampaignState state)
        {
            if (rules == CurrentCheckpointConversion.RetiredPokemonRules)
            {
                // Validate the explicit retired-source intermediate without publishing
                // its compaction. The correction itself remains v2 and changes only Stat.
                try { _ = validator.ConvertRetiredPokemonCheckpoint(state); return null; }
                catch (InvalidDataException failure) { return failure.Message; }
            }
            return rules == CurrentCheckpointConversion.LegacyTowerRules
                ? validator.ValidateLegacyTrainerTowerCheckpoint(state) : validator.ValidatePlayerSave(state);
        }
        if (Validate(current) is {} error)
            throw new InvalidDataException("hatch_count_conversion_invalid: " + error);
        // Preserve the source version and complete Tower history. This command only
        // adds the supplied statistic; the distinct version converter is explicit.
        var payload = JsonNode.Parse(SaveCodec.Decode<JsonElement>(original, rules,
            CurrentCheckpointConversion.SupportedContent).State.GetRawText())!.AsObject();
        var gameStats = payload["GameStats"] as JsonObject;
        if (gameStats is null) payload["GameStats"] = gameStats = new JsonObject();
        gameStats.Add(Stat, knownCount);
        byte[] converted = SaveCodec.Encode(payload, source.Generation, rules, CurrentCheckpointConversion.SupportedContent);
        var verified = DecodeForValidation(converted, Validate);
        if (verified.Generation != source.Generation || current.World.Rng != source.State.World.Rng ||
            CanonicalJson.Hash(current with { GameStats = source.State.GameStats }) != CanonicalJson.Hash(source.State) ||
            CanonicalJson.Hash(verified.State) != CanonicalJson.Hash(current) ||
            !File.ReadAllBytes(input).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("hatch_count_conversion_changed_unrelated_state");
        NewCheckpointFile.Publish(output, converted);
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, input, output, generation = verified.Generation, rules,
            knownHatchedEggs = knownCount, sourcePreserved = true,
            scope = "Explicit operator-provided lifetime total, not reconstructed history. Only the absent hatch statistic was added; rules and Tower history are unchanged."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
