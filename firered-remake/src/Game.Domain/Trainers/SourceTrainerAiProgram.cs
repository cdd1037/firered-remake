using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Game.Domain.Trainers;

/// <summary>Immutable authenticated original data. The private generated pack is rebuilt by
/// the source importer and embedded at build time; no filesystem or network lookup occurs.</summary>
internal static class SourceTrainerAiProgram
{
    internal const string PackSha256 = "6eb1b994e573c0c39e09899611bee68c03be5feef991adad0e31e32704ca72b0";
    internal static readonly byte[] Code;
    internal static readonly int AI_CheckBadMove;
    internal static readonly int AI_CheckViability;
    internal static readonly int AI_TryToFaint;
    internal static readonly ImmutableDictionary<string, uint> Constants;
    internal static readonly ImmutableDictionary<string, (int Id, uint Effect)> Items;

    static SourceTrainerAiProgram()
    {
        using var stream = typeof(SourceTrainerAiProgram).Assembly.GetManifestResourceStream("Game.Domain.SourceTrainerAiProgram.json")
            ?? throw new InvalidOperationException("source_trainer_ai_program_missing");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes);
        byte[] payload = bytes.ToArray();
        if (!Convert.ToHexString(SHA256.HashData(payload)).Equals(PackSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("source_trainer_ai_program_hash");
        using var json = JsonDocument.Parse(payload); var root = json.RootElement;
        if (root.GetProperty("sourceCommit").GetString() != SourceTrainerAi.SourceCommit)
            throw new InvalidOperationException("source_trainer_ai_program_source");
        Code = root.GetProperty("program").EnumerateArray().Select(x => x.GetByte()).ToArray();
        var entries = root.GetProperty("entries");
        AI_CheckBadMove = entries.GetProperty("AI_CheckBadMove").GetInt32();
        AI_CheckViability = entries.GetProperty("AI_CheckViability").GetInt32();
        AI_TryToFaint = entries.GetProperty("AI_TryToFaint").GetInt32();
        Constants = root.GetProperty("constants").EnumerateObject().ToImmutableDictionary(x => x.Name, x => x.Value.GetUInt32());
        Items = root.GetProperty("items").EnumerateObject().ToImmutableDictionary(x => x.Name,
            x => (x.Value.GetProperty("id").GetInt32(), x.Value.GetProperty("effect").GetUInt32()));
    }
}
