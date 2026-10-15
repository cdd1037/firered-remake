using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Content;

namespace Game.Tooling;

/// <summary>Explicit offline checkpoint conversion into the current composition.
/// Runtime loaders remain strict; this tool never advances gameplay or replaces its input.</summary>
internal static class CurrentCheckpointConversion
{
    public static int Run(string root, string sourcePath, string destinationPath)
    {
        string input = Path.GetFullPath(sourcePath), output = Path.GetFullPath(destinationPath);
        if (input == output || File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("checkpoint_conversion_requires_new_destination");

        byte[] original = File.ReadAllBytes(input);
        using var sourceDocument = JsonDocument.Parse(original);
        string sourceRules = sourceDocument.RootElement.GetProperty("RulesVersion").GetString()!;
        string sourceContent = sourceDocument.RootElement.GetProperty("ContentVersion").GetString()!;
        if (sourceRules != CampaignSession.CelioRulesVersion && sourceRules != CurrentGameProfile.RulesVersion)
            throw new InvalidDataException("checkpoint_source_rules_unsupported");
        if (string.IsNullOrWhiteSpace(sourceContent))
            throw new InvalidDataException("checkpoint_source_content_invalid");
        byte[] admitted = original;
        if (!sourceDocument.RootElement.GetProperty("Payload").TryGetProperty("Coins", out _))
        {
            // This is the explicit pre-wallet rooftop format, not a repair path for
            // current saves. First validate the untouched envelope/checksum.
            if (sourceRules != CurrentGameProfile.RulesVersion ||
                sourceContent != "1549c2e7484d83969c34ff75550efa3faff4ef197dbd00d1ca543c02f742fa90")
                throw new InvalidDataException("checkpoint_missing_coins_not_pre_wallet_format");
            var checkedSource = SaveCodec.Decode<JsonElement>(original, sourceRules, sourceContent, input);
            var payload = JsonNode.Parse(checkedSource.State.GetRawText())!.AsObject();
            payload.Add("Coins", 0);
            admitted = SaveCodec.Encode(payload, checkedSource.Generation, sourceRules, sourceContent);
        }
        var removedProvenance = new List<string>();
        if (sourceContent == "firered-current-content-v3")
        {
            // Verify the original envelope before the one declared schema transformation.
            var checkedSource = SaveCodec.Decode<JsonElement>(admitted, sourceRules, sourceContent, input);
            var payload = JsonNode.Parse(checkedSource.State.GetRawText())!;
            RemoveTradeFingerprint(payload, "Payload", removedProvenance);
            admitted = SaveCodec.Encode(payload, checkedSource.Generation, sourceRules, sourceContent);
        }
        var catalog = PretCampaignCatalog.LoadCurrent(root);
        if (sourceContent == catalog.ContentVersion && sourceRules == CurrentGameProfile.RulesVersion)
            throw new InvalidDataException("checkpoint_already_current");
        var validator = CampaignSession.CreateCurrent(catalog);
        var prior = SaveCodec.Decode<CampaignState>(admitted, sourceRules,
            sourceContent, input, validator.ValidatePlayerSave);
        var current = CampaignSession.RestoreCurrent(catalog, prior.State, prior.Generation);
        byte[] converted = SaveCodec.Encode(current.State, current.Revision,
            CurrentGameProfile.RulesVersion, catalog.ContentVersion);
        var verified = SaveCodec.Decode<CampaignState>(converted, CurrentGameProfile.RulesVersion,
            catalog.ContentVersion, validate: current.ValidatePlayerSave);
        string payloadHash = CanonicalJson.Hash(prior.State);
        if (verified.Generation != prior.Generation || CanonicalJson.Hash(verified.State) != payloadHash ||
            current.State.World.Rng != prior.State.World.Rng ||
            !File.ReadAllBytes(input).AsSpan().SequenceEqual(original))
            throw new InvalidDataException("checkpoint_conversion_changed_payload_or_source");

        // Missing fields introduced after the archived checkpoint may acquire neutral defaults.
        // Every original value must survive; no old history, party or inventory may disappear.
        using var targetDocument = JsonDocument.Parse(converted);
        var addedDefaults = new List<string>();
        using var admittedDocument = JsonDocument.Parse(admitted);
        RequirePreserved(admittedDocument.RootElement.GetProperty("Payload"),
            targetDocument.RootElement.GetProperty("Payload"), "Payload", addedDefaults);
        NewCheckpointFile.Publish(output, converted);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            passed = true, input, output, generation = verified.Generation, payloadHash,
            sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(original)),
            outputSha256 = Convert.ToHexStringLower(SHA256.HashData(converted)),
            sourceRules, targetRules = CurrentGameProfile.RulesVersion,
            sourceContent, content = catalog.ContentVersion, sourcePreserved = true,
            originalPayloadHash = sourceDocument.RootElement.GetProperty("PayloadHash").GetString(),
            originalGameplayValuesPreserved = true, removedProvenance, addedNeutralDefaults = addedDefaults,
            scope = "Explicit offline checkpoint conversion; no automatic old-save acceptance or gameplay advancement."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static void RemoveTradeFingerprint(JsonNode node, string path, List<string> removed)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj.ToArray())
            {
                if (pair.Key == "NpcTradeOrigin" && pair.Value is JsonObject origin && origin.ContainsKey("CatalogFingerprint"))
                {
                    if (origin.Count != 3 || !origin.ContainsKey("TradeId") || !origin.ContainsKey("ReceivedLevel") ||
                        origin["CatalogFingerprint"] is not JsonValue fingerprint ||
                        !fingerprint.TryGetValue<string>(out var value) || value.Length != 64 || !value.All(Uri.IsHexDigit))
                        throw new InvalidDataException("checkpoint_trade_origin_shape: " + path);
                    origin.Remove("CatalogFingerprint");
                    removed.Add(path + ".NpcTradeOrigin.CatalogFingerprint");
                }
                if (pair.Value is not null) RemoveTradeFingerprint(pair.Value, path + "." + pair.Key, removed);
            }
        }
        else if (node is JsonArray array)
            for (int index = 0; index < array.Count; index++)
                if (array[index] is {} item) RemoveTradeFingerprint(item, path + "[" + index + "]", removed);
    }

    private static void RequirePreserved(JsonElement before, JsonElement after, string path, List<string> additions)
    {
        if (before.ValueKind != after.ValueKind)
            throw new InvalidDataException("checkpoint_payload_changed: " + path);
        if (before.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in before.EnumerateObject())
            {
                if (!after.TryGetProperty(property.Name, out var value))
                    throw new InvalidDataException("checkpoint_payload_removed: " + path + "." + property.Name);
                RequirePreserved(property.Value, value, path + "." + property.Name, additions);
            }
            foreach (var property in after.EnumerateObject())
                if (!before.TryGetProperty(property.Name, out _))
                {
                    bool neutral = property.Value.ValueKind switch
                    {
                        JsonValueKind.Null or JsonValueKind.False => true,
                        JsonValueKind.Array => property.Value.GetArrayLength() == 0,
                        JsonValueKind.Object => !property.Value.EnumerateObject().Any(),
                        JsonValueKind.Number => property.Value.TryGetInt64(out long number) && number == 0,
                        _ => false
                    };
                    if (!neutral) throw new InvalidDataException("checkpoint_non_neutral_default: " + path + "." + property.Name);
                    additions.Add(path + "." + property.Name);
                }
        }
        else if (before.ValueKind == JsonValueKind.Array)
        {
            if (before.GetArrayLength() != after.GetArrayLength())
                throw new InvalidDataException("checkpoint_payload_changed: " + path);
            for (int index = 0; index < before.GetArrayLength(); index++)
                RequirePreserved(before[index], after[index], path + "[" + index + "]", additions);
        }
        else if (CanonicalJson.Hash(before) != CanonicalJson.Hash(after))
            throw new InvalidDataException("checkpoint_payload_changed: " + path);
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
