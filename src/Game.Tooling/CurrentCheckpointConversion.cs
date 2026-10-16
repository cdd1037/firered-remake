using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Content;

namespace Game.Tooling;

/// <summary>One explicit offline v2-to-v3 conversion. Runtime loading remains strict.</summary>
internal static class CurrentCheckpointConversion
{
    public static int Run(string root, string sourcePath, string destinationPath)
    {
        string input = Path.GetFullPath(sourcePath), output = Path.GetFullPath(destinationPath);
        if (input == output || File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("checkpoint_conversion_requires_new_destination");
        byte[] original = File.ReadAllBytes(input);
        var catalog = PretCampaignCatalog.LoadCurrent(root);
        // The prior checkpoint must already have the current content format. This is
        // not an upgrade chain and does not repair older rules or content schemas.
        var prior = SaveCodec.Decode<JsonElement>(original, "firered-current-campaign-v2",
            catalog.ContentVersion, input);
        var payload = JsonNode.Parse(prior.State.GetRawText())!.AsObject();
        payload.Remove("ReleasedPokemon");
        if (payload["HallOfFame"] is JsonObject hall) hall.Remove("RibbonRecipients");
        var admitted = SaveCodec.Encode(payload, prior.Generation, CurrentGameProfile.RulesVersion, catalog.ContentVersion);
        var source = SaveCodec.Decode<CampaignState>(admitted, CurrentGameProfile.RulesVersion, catalog.ContentVersion).State;
        var validator = CampaignSession.CreateCurrent(catalog);
        var current = validator.ConvertRetiredPokemonCheckpoint(source);
        byte[] converted = SaveCodec.Encode(current, prior.Generation, CurrentGameProfile.RulesVersion, catalog.ContentVersion);
        var verified = SaveCodec.Decode<CampaignState>(converted, CurrentGameProfile.RulesVersion,
            catalog.ContentVersion, validate: validator.ValidatePlayerSave);
        // Only the declared current-source lists and unreferenced wild settlements may
        // change. Every other gameplay value, including allocator/RNG/stats/dex, stays exact.
        var preserved = current with { Exploration = source.Exploration, EvolutionHistory = source.EvolutionHistory,
            PrizePokemon = source.PrizePokemon, DayCareEggs = source.DayCareEggs };
        if (CanonicalJson.Hash(preserved) != CanonicalJson.Hash(source) ||
            current.Exploration is {} now && source.Exploration is {} before &&
            CanonicalJson.Hash(now with { Captured = before.Captured, Battles = before.Battles }) != CanonicalJson.Hash(before) ||
            verified.Generation != prior.Generation || !File.ReadAllBytes(input).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("checkpoint_conversion_changed_gameplay_or_source");
        NewCheckpointFile.Publish(output, converted);
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, input, output,
            generation = verified.Generation, rules = CurrentGameProfile.RulesVersion,
            sourcePreserved = true, retiredEntitiesRemoved = true,
            retainedCaptures = current.Exploration?.Captured?.Length,
            retainedWildSettlements = current.Exploration?.Battles.Length,
            scope = "Explicit v2-to-v3 conversion. No automatic old-save acceptance or gameplay advancement."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
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
