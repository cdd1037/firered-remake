using System.Collections.Immutable;
using System.Text;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretEarlyRepelSource(string Path);
public sealed record PretEarlyRepelText(string Symbol, string Text, string Path, int Line);
public sealed record PretEarlyRepelDocument(int SchemaVersion, string SourceCommit, string Profile,
    int RepelVariable, string ItemSymbol, int Duration, int BattleUsage, string BattleUseFunction,
    string FieldUseFunction, ImmutableArray<PretEarlyRepelSource> SourceFiles,
    ImmutableArray<PretEarlyRepelText> Texts);

/// <summary>Three original texts and REPEL metadata; no art/audio or runtime reference-repository access.</summary>
public static class PretEarlyRepel
{

    public static PretEarlyRepelDocument Load(string json)
    {
        var data = ContentJson.ParseStrict<PretEarlyRepelDocument>(json, ContentJson.CreateOptions());
        if (data.SchemaVersion != 1 || data.RepelVariable != 0x4020 ||
            data.ItemSymbol != "ITEM_REPEL" || data.Duration != EarlyRepelRules.Duration || data.BattleUsage != 0 ||
            data.BattleUseFunction != "NULL" || data.FieldUseFunction != "FieldUseFunc_Repel" || data.Texts.Length != 3 ||
            !data.Texts.Select(x => x.Symbol).SequenceEqual(new[] { "gText_PlayerUsedVar2","gText_RepelEffectsLingered","Text_RepelWoreOff" }))
            throw ContentJson.Error("early_repel_profile", "$", "Incomplete Repel source document.");
        return data;
    }
}
