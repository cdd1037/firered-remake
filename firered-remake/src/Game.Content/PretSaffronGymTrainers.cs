using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;
public sealed record SaffronGymTrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1MoveName> MoveNames,
    ImmutableArray<string> Limitations);
public sealed record SaffronGymTrainerCatalogContent(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers, ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);
public static class PretSaffronGymTrainers
{
    public const string Profile = "firered-saffron-gym-trainers-v1";
    public const int TrainerCount = 8,PartyMemberCount = 20;
    public static SaffronGymTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var source = SaffronGymSource.Catalogs(root); var d = source.Parse<SaffronGymTrainerDocument>("trainers.json", canonical);
        SilphSourcePackage.Need(d.SchemaVersion == 1 && !d.CompleteSourceRoster &&
            d.Trainers.Length == TrainerCount && d.Battles.Length == TrainerCount && d.Trainers.Select(t => t.Id).Distinct().Count() == TrainerCount &&
            d.Trainers.Sum(t => t.Party.Length) == PartyMemberCount && d.ResolvedMoves.GetArrayLength() == TrainerCount && !d.Limitations.IsDefaultOrEmpty,
            "Saffron Gym trainer scope differs.");
        for (int i = 0; i < d.Trainers.Length; i++)
        {
            var t = d.Trainers[i]; var b = d.Battles[i]; var resolved = d.ResolvedMoves[i];
            SilphSourcePackage.Need(t.Id == b.Trainer && !t.DoubleBattle && t.Items.SequenceEqual(t.Id.Value == "pret:TRAINER_LEADER_SABRINA" ? ["ITEM_HYPER_POTION","ITEM_HYPER_POTION","ITEM_FULL_HEAL","ITEM_NONE"] : ["ITEM_NONE","ITEM_NONE","ITEM_NONE","ITEM_NONE"]) &&
                resolved.GetProperty("trainer").GetString() == t.Id.Value[5..] && resolved.GetProperty("party").GetArrayLength() == t.Party.Length &&
                b.BattleTypeFlags == 8 && b.Callback == "CB2_EndTrainerBattle", "Saffron Gym trainer source contract differs.");
            for (int j = 0; j < t.Party.Length; j++)
            {
                var p = t.Party[j]; var r = resolved.GetProperty("party")[j];                 SilphSourcePackage.Need(p.Species.Value == "pret:" + r.GetProperty("species").GetString() && p.Level == r.GetProperty("level").GetInt32() &&
                    p.HeldItem == "ITEM_NONE" && (t.PartyFlags == 0 ? p.CustomMoves.IsEmpty : p.CustomMoves.Select(m => m.Value[5..]).SequenceEqual(r.GetProperty("moves").EnumerateArray().Select(x => x.GetString()))),
                    "Saffron Gym trainer party source differs.");
            }
        }
        var leader = d.Trainers.Single(t => t.Id.Value == "pret:TRAINER_LEADER_SABRINA");
        var leaderBattle = d.Battles.Single(b => b.Trainer == leader.Id);
        SilphSourcePackage.Need(leader.AiFlags == 7 && leader.PartyFlags == 1 && leader.Party.Select(p => (p.SourceSpeciesId,p.Level)).SequenceEqual([(64,38),(122,37),(49,38),(65,43)]) &&
            leader.Party.All(p => p.SourceIv == 0) && leaderBattle.Mode == 1 && leaderBattle.PostBattleScript == "SaffronCity_Gym_EventScript_DefeatedSabrina" && leaderBattle.MusicOption == "NO_MUSIC" &&
            d.Trainers.Where(t => t.Id != leader.Id).All(t => t.AiFlags == 1 && t.PartyFlags == 0 && t.Party.All(p => p.SourceIv == (t.TrainerClass == "TRAINER_CLASS_PSYCHIC" ? 50 : 0))) && d.Moves.Length == 26,
            "Saffron Gym leader, subordinate or move scope differs.");
        foreach (var m in d.Moves) { SilphSourcePackage.Need(!m.BattleEffectImplemented, "Metadata cannot enable battle effects."); }
        return d;
    }
    public static SaffronGymTrainerCatalogContent Load(PretCanonicalPokemonCatalog canonical, string root)
    { var d = LoadDocument(canonical, root); return new(d.SourceCommit, d.Profile, false, d.Trainers.ToImmutableDictionary(t => t.Id), d.Battles.ToImmutableDictionary(b => b.Trainer)); }
}
