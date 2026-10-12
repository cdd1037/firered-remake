using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;
public sealed record SilphTrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1MoveName> MoveNames,
    ImmutableArray<string> Limitations);
public sealed record SilphTrainerCatalogContent(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers, ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);
public static class PretSilphTrainers
{
    public const string Profile = "firered-silph-trainers-v1";
    public const string GeneratedSha256 = "19803a8ac324dff408161d1b9ce25ba49be3dc5561f7679f075196d27b5ec59f";
    public const int TrainerCount = 34, PartyMemberCount = 101;
    public static SilphTrainerDocument LoadDocument(string root)
    {
        var source = SilphSourcePackage.Catalogs(root); var d = source.Parse<SilphTrainerDocument>("trainers.json");
        SilphSourcePackage.Need(d.SchemaVersion == 1 && d.Profile == Profile && d.SourceCommit == PokemonFactory.SourceCommit && !d.CompleteSourceRoster &&
            d.Trainers.Length == TrainerCount && d.Battles.Length == TrainerCount && d.Trainers.Select(t => t.Id).Distinct().Count() == TrainerCount &&
            d.Trainers.Sum(t => t.Party.Length) == PartyMemberCount && d.ResolvedMoves.GetArrayLength() == TrainerCount && !d.Limitations.IsDefaultOrEmpty,
            "Silph trainer scope differs.");
        for (int i = 0; i < d.Trainers.Length; i++)
        {
            var t = d.Trainers[i]; var b = d.Battles[i]; var resolved = d.ResolvedMoves[i];
            source.Reference(t.Source); source.Reference(t.MoneySource); source.Reference(b.Source);
            SilphSourcePackage.Need(t.Id == b.Trainer && !t.DoubleBattle && t.Items.SequenceEqual(["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"]) &&
                resolved.GetProperty("trainer").GetString() == t.Id.Value[5..] && resolved.GetProperty("party").GetArrayLength() == t.Party.Length &&
                b.BattleTypeFlags == 8 && b.Callback == "CB2_EndTrainerBattle", "Silph trainer source contract differs.");
            for (int j = 0; j < t.Party.Length; j++)
            {
                var p = t.Party[j]; var r = resolved.GetProperty("party")[j]; source.Reference(p.Source); source.Reference(p.SpeciesNameSource);
                source.Reference(SafariSource.Parse<TrainerSourceRef>(System.Text.Encoding.UTF8.GetBytes(r.GetProperty("learnsetSource").GetRawText())));
                SilphSourcePackage.Need(p.Species.Value == "pret:" + r.GetProperty("species").GetString() && p.Level == r.GetProperty("level").GetInt32() &&
                    p.HeldItem == "ITEM_NONE" && (t.PartyFlags == 0 ? p.CustomMoves.IsEmpty : p.CustomMoves.Select(m => m.Value[5..]).SequenceEqual(r.GetProperty("moves").EnumerateArray().Select(x => x.GetString()))),
                    "Silph trainer party source differs.");
            }
        }
        foreach (var m in d.Moves) { source.Reference(m.Source); SilphSourcePackage.Need(!m.BattleEffectImplemented, "Metadata cannot enable battle effects."); }
        foreach (var m in d.MoveNames) source.Reference(m.Source);
        return d;
    }
    public static SilphTrainerCatalogContent Load(string root)
    { var d = LoadDocument(root); return new(d.SourceCommit, d.Profile, false, d.Trainers.ToImmutableDictionary(t => t.Id), d.Battles.ToImmutableDictionary(b => b.Trainer)); }
}
