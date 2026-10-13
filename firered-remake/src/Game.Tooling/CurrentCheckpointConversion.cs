using System.Security.Cryptography;
using System.Text.Json;
using Game.Application;
using Game.Content;

namespace Game.Tooling;

/// <summary>Offline, explicit conversion of the backed-up Celio milestone.
/// This is not a loader fallback or a historical campaign upgrade chain.</summary>
internal static class CurrentCheckpointConversion
{
    public static int Run(string root, string sourcePath, string destinationPath)
    {
        string input = Path.GetFullPath(sourcePath), output = Path.GetFullPath(destinationPath);
        if (input == output || File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("checkpoint_conversion_requires_new_destination");

        var catalog = PretCampaignCatalog.LoadCurrent(root);
        var diagnostic = CampaignSession.CreateDiagnostic(catalog);
        byte[] original = File.ReadAllBytes(input);
        var prior = SaveCodec.Decode<CampaignState>(original, CampaignSession.CelioRulesVersion,
            catalog.ContentVersion, input, diagnostic.ValidatePlayerSave);
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

        NewCheckpointFile.Publish(output, converted);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            passed = true, input, output, generation = verified.Generation, payloadHash,
            sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(original)),
            outputSha256 = Convert.ToHexStringLower(SHA256.HashData(converted)),
            sourceRules = CampaignSession.CelioRulesVersion,
            targetRules = CurrentGameProfile.RulesVersion,
            content = catalog.ContentVersion, sourcePreserved = true, gameplayPayloadPreserved = true,
            scope = "Explicit offline Celio milestone conversion; no automatic old-save acceptance."
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
