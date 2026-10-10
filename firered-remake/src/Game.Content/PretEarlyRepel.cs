using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretEarlyRepelSource(string Path, string Sha256, int ByteLength, string GitBlobSha1);
public sealed record PretEarlyRepelText(string Symbol, string Text, string Path, int Line);
public sealed record PretEarlyRepelDocument(int SchemaVersion, string SourceCommit, string Profile,
    int RepelVariable, string ItemSymbol, int Duration, int BattleUsage, string BattleUseFunction,
    string FieldUseFunction, ImmutableArray<PretEarlyRepelSource> SourceFiles,
    ImmutableArray<PretEarlyRepelText> Texts);

/// <summary>Three original texts and REPEL metadata; no art/audio or runtime reference-repository access.</summary>
public static class PretEarlyRepel
{
    public const string GeneratedSha256 = "66fb7d7e31ff867462391a389431bd293f16be02fb1e82480fb428972ddc378c";
    public const string LockSha256 = "6afd036a2e2c61046d7339ef4f7f441ac78b8b00ced9e73f28b3b4bd8beb9aa8";

    public static PretEarlyRepelDocument Load(string json)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))) != GeneratedSha256)
            throw ContentJson.Error("early_repel_generated_hash", "$", "The complete reviewed Repel bytes are required.");
        var data = ContentJson.ParseStrict<PretEarlyRepelDocument>(json, ContentJson.CreateOptions());
        if (data.SchemaVersion != 1 || data.SourceCommit != EarlyRepelRules.SourceCommit ||
            data.Profile != EarlyRepelRules.RulesVersion || data.RepelVariable != 0x4020 ||
            data.ItemSymbol != "ITEM_REPEL" || data.Duration != EarlyRepelRules.Duration || data.BattleUsage != 0 ||
            data.BattleUseFunction != "NULL" || data.FieldUseFunction != "FieldUseFunc_Repel" ||
            data.SourceFiles.Length != 10 || data.Texts.Length != 3 ||
            !data.Texts.Select(x => x.Symbol).SequenceEqual(new[] { "gText_PlayerUsedVar2", "gText_RepelEffectsLingered", "Text_RepelWoreOff" }))
            throw ContentJson.Error("early_repel_profile", "$", "Incomplete Repel source document.");
        return data;
    }
}
