using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Content;

namespace Game.Tooling;

/// <summary>Explicit offline v2-to-v4 and v3-to-v4 routes. Runtime loading remains strict.</summary>
internal static class CurrentCheckpointConversion
{
    internal const string RetiredPokemonRules = "firered-current-campaign-v2";
    internal const string LegacyTowerRules = "firered-current-campaign-v3";
    internal const string BoundedTowerRules = "firered-current-campaign-v4";
    internal const string SupportedContent = "firered-current-content-v4";

    public static int Run(string root, string sourcePath, string destinationPath)
    {
        string input = Path.GetFullPath(sourcePath), output = Path.GetFullPath(destinationPath);
        if (string.Equals(input, output, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("checkpoint_conversion_requires_new_destination");
        byte[] original = File.ReadAllBytes(input);
        string sourceRules = ReadRulesVersion(original);
        if (sourceRules is not (RetiredPokemonRules or LegacyTowerRules))
            throw new InvalidDataException("checkpoint_conversion_unsupported_route: expected campaign-v2 or campaign-v3 to campaign-v4");
        var catalog = PretCampaignCatalog.LoadCurrent(root);
        if (catalog.ContentVersion != SupportedContent)
            throw new InvalidDataException("checkpoint_conversion_content_unsupported");
        var prior = SaveCodec.Decode<JsonElement>(original, sourceRules, SupportedContent, input);
        if (prior.State.ValueKind != JsonValueKind.Object) throw new InvalidDataException("save_payload_invalid");
        var payload = JsonNode.Parse(prior.State.GetRawText())!.AsObject();
        bool retiredPokemon = sourceRules == RetiredPokemonRules;
        if (retiredPokemon)
        {
            payload.Remove("ReleasedPokemon");
            if (payload["HallOfFame"] is JsonObject hall) hall.Remove("RibbonRecipients");
        }
        var source = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(payload, prior.Generation, sourceRules, SupportedContent),
            sourceRules, SupportedContent).State;
        var validator = CampaignSession.CreateCurrent(catalog);
        // v2 first removes retired Pokemon proofs, then validates the full v3 Tower
        // history. The separate Tower step validates every receipt before discarding it.
        var interim = retiredPokemon ? validator.ConvertRetiredPokemonCheckpoint(source) : source;
        var current = validator.ConvertLegacyTrainerTowerCheckpoint(interim);
        var preserved = current with { TrainerTowerHistory = source.TrainerTowerHistory };
        if (retiredPokemon)
            preserved = preserved with { Exploration = source.Exploration, EvolutionHistory = source.EvolutionHistory,
                PrizePokemon = source.PrizePokemon, DayCareEggs = source.DayCareEggs };
        if (CanonicalJson.Hash(preserved) != CanonicalJson.Hash(source) ||
            retiredPokemon && current.Exploration is {} now && source.Exploration is {} before &&
            CanonicalJson.Hash(now with { Captured = before.Captured, Battles = before.Battles }) != CanonicalJson.Hash(before))
            throw new InvalidDataException("checkpoint_conversion_changed_gameplay_or_source");

        // Edit only the declared payload fields. In particular the v3 route must
        // not erase unrelated/unknown optional data through typed reserialization.
        if (retiredPokemon)
        {
            if (payload["Exploration"] is JsonObject exploration && current.Exploration is {} compacted)
            {
                RetainPayloadRows(exploration, "Captured", "InstanceId", JsonSerializer.SerializeToNode(compacted.Captured));
                RetainPayloadRows(exploration, "Battles", "Id", JsonSerializer.SerializeToNode(compacted.Battles));
            }
            RetainPayloadRows(payload, "EvolutionHistory", "InstanceId", JsonSerializer.SerializeToNode(current.EvolutionHistory));
            RetainPayloadRows(payload, "PrizePokemon", "InstanceId", JsonSerializer.SerializeToNode(current.PrizePokemon));
            RetainPayloadRows(payload, "DayCareEggs", "InstanceId", JsonSerializer.SerializeToNode(current.DayCareEggs));
        }
        if (payload["TrainerTowerHistory"] is JsonArray history && history.Count > 1)
            payload["TrainerTowerHistory"] = new JsonArray(history[^1]!.DeepClone());
        byte[] converted = SaveCodec.Encode(payload, prior.Generation, BoundedTowerRules, SupportedContent);
        var verified = SaveCodec.Decode<CampaignState>(converted, BoundedTowerRules, SupportedContent,
            validate: validator.ValidatePlayerSave);
        if (CanonicalJson.Hash(verified.State) != CanonicalJson.Hash(current) || verified.Generation != prior.Generation ||
            !File.ReadAllBytes(input).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("checkpoint_conversion_changed_gameplay_or_source");
        NewCheckpointFile.Publish(output, converted);
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, input, output,
            generation = verified.Generation, sourceRules, rules = BoundedTowerRules,
            sourcePreserved = true, retiredEntitiesRemoved = retiredPokemon,
            retainedCaptures = current.Exploration?.Captured?.Length,
            retainedWildSettlements = current.Exploration?.Battles.Length,
            discardedTowerReceipts = (source.TrainerTowerHistory?.Length ?? 0) - (current.TrainerTowerHistory?.Length ?? 0),
            scope = retiredPokemon
                ? "Explicit v2-to-v4 chain: retired Pokemon proof compaction, then validated Tower receipt compaction. No gameplay advancement or inferred hatch count."
                : "Explicit v3-to-v4 Tower receipt compaction. No other gameplay changes or inferred hatch count."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    // The v2 step only removes rows. Retained raw rows keep all their admitted
    // fields, including optional nested data ignored by typed deserialization.
    private static void RetainPayloadRows(JsonObject payload, string property, string identity, JsonNode? compacted)
    {
        if (payload[property] is not JsonArray rows || compacted is not JsonArray retained) return;
        var identities = retained.Select(row => row![identity]!.ToJsonString()).ToHashSet(StringComparer.Ordinal);
        for (int i = rows.Count - 1; i >= 0; i--)
            if (!identities.Contains(rows[i]![identity]!.ToJsonString())) rows.RemoveAt(i);
    }

    // Select only a declared route here; SaveCodec subsequently authenticates the
    // complete envelope, exact version/content, checksum and payload for that route.
    internal static string ReadRulesVersion(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("RulesVersion", out var rules) || rules.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("save_envelope_invalid");
            return rules.GetString()!;
        }
        catch (JsonException error) { throw new InvalidDataException("save_decode_invalid: JsonException", error); }
    }
}

internal static class NewCheckpointFile
{
    public static void Publish(string destination, ReadOnlySpan<byte> bytes)
    {
        string staged = Path.Combine(Path.GetDirectoryName(destination)!,
            ".firered-checkpoint-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            File.Move(staged, destination, overwrite: false);
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }
}
