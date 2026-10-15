using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All nineteen initial Pokemon Tower singles teams, with exact source NPC construction.</summary>
public static class PokemonTowerTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post, string? PostScript, string? Music);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_RIVAL_POKEMON_TOWER_SQUIRTLE")] = new("PokemonTower_2F_EventScript_RivalSquirtle", "PokemonTower_2F_Text_RivalIntro", "PokemonTower_2F_Text_RivalDefeat", "PokemonTower_2F_Text_RivalPostBattle", null, null),
        [new("pret:TRAINER_RIVAL_POKEMON_TOWER_BULBASAUR")] = new("PokemonTower_2F_EventScript_RivalBulbasaur", "PokemonTower_2F_Text_RivalIntro", "PokemonTower_2F_Text_RivalDefeat", "PokemonTower_2F_Text_RivalPostBattle", null, null),
        [new("pret:TRAINER_RIVAL_POKEMON_TOWER_CHARMANDER")] = new("PokemonTower_2F_EventScript_RivalCharmander", "PokemonTower_2F_Text_RivalIntro", "PokemonTower_2F_Text_RivalDefeat", "PokemonTower_2F_Text_RivalPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_PATRICIA")] = new("PokemonTower_3F_EventScript_Patricia", "PokemonTower_3F_Text_PatriciaIntro", "PokemonTower_3F_Text_PatriciaDefeat", "PokemonTower_3F_Text_PatriciaPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_CARLY")] = new("PokemonTower_3F_EventScript_Carly", "PokemonTower_3F_Text_CarlyIntro", "PokemonTower_3F_Text_CarlyDefeat", "PokemonTower_3F_Text_CarlyPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_HOPE")] = new("PokemonTower_3F_EventScript_Hope", "PokemonTower_3F_Text_HopeIntro", "PokemonTower_3F_Text_HopeDefeat", "PokemonTower_3F_Text_HopePostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_PAULA")] = new("PokemonTower_4F_EventScript_Paula", "PokemonTower_4F_Text_PaulaIntro", "PokemonTower_4F_Text_PaulaDefeat", "PokemonTower_4F_Text_PaulaPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_LAUREL")] = new("PokemonTower_4F_EventScript_Laurel", "PokemonTower_4F_Text_LaurelIntro", "PokemonTower_4F_Text_LaurelDefeat", "PokemonTower_4F_Text_LaurelPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_JODY")] = new("PokemonTower_4F_EventScript_Jody", "PokemonTower_4F_Text_JodyIntro", "PokemonTower_4F_Text_JodyDefeat", "PokemonTower_4F_Text_JodyPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_TAMMY")] = new("PokemonTower_5F_EventScript_Tammy", "PokemonTower_5F_Text_TammyIntro", "PokemonTower_5F_Text_TammyDefeat", "PokemonTower_5F_Text_TammyPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_RUTH")] = new("PokemonTower_5F_EventScript_Ruth", "PokemonTower_5F_Text_RuthIntro", "PokemonTower_5F_Text_RuthDefeat", "PokemonTower_5F_Text_RuthPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_KARINA")] = new("PokemonTower_5F_EventScript_Karina", "PokemonTower_5F_Text_KarinaIntro", "PokemonTower_5F_Text_KarinaDefeat", "PokemonTower_5F_Text_KarinaPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_JANAE")] = new("PokemonTower_5F_EventScript_Janae", "PokemonTower_5F_Text_JanaeIntro", "PokemonTower_5F_Text_JanaeDefeat", "PokemonTower_5F_Text_JanaePostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_ANGELICA")] = new("PokemonTower_6F_EventScript_Angelica", "PokemonTower_6F_Text_AngelicaIntro", "PokemonTower_6F_Text_AngelicaDefeat", "PokemonTower_6F_Text_AngelicaPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_EMILIA")] = new("PokemonTower_6F_EventScript_Emilia", "PokemonTower_6F_Text_EmiliaIntro", "PokemonTower_6F_Text_EmiliaDefeat", "PokemonTower_6F_Text_EmiliaPostBattle", null, null),
        [new("pret:TRAINER_CHANNELER_JENNIFER")] = new("PokemonTower_6F_EventScript_Jennifer", "PokemonTower_6F_Text_JenniferIntro", "PokemonTower_6F_Text_JenniferDefeat", "PokemonTower_6F_Text_JenniferPostBattle", null, null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_19")] = new("PokemonTower_7F_EventScript_Grunt1", "PokemonTower_7F_Text_Grunt1Intro", "PokemonTower_7F_Text_Grunt1Defeat", "PokemonTower_7F_Text_Grunt1PostBattle", "PokemonTower_7F_EventScript_DefeatedGrunt1", null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_20")] = new("PokemonTower_7F_EventScript_Grunt2", "PokemonTower_7F_Text_Grunt2Intro", "PokemonTower_7F_Text_Grunt2Defeat", "PokemonTower_7F_Text_Grunt2PostBattle", "PokemonTower_7F_EventScript_DefeatedGrunt2", null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_21")] = new("PokemonTower_7F_EventScript_Grunt3", "PokemonTower_7F_Text_Grunt3Intro", "PokemonTower_7F_Text_Grunt3Defeat", "PokemonTower_7F_Text_Grunt3PostBattle", "PokemonTower_7F_EventScript_DefeatedGrunt3", null),
    }.ToImmutableDictionary();
    public static TrainerId RivalForStarter(int sourceStarterValue) => new("pret:TRAINER_RIVAL_POKEMON_TOWER_" + (sourceStarterValue switch { 0 => "CHARMANDER", 1 => "BULBASAUR", 2 => "SQUIRTLE", _ => throw new ArgumentOutOfRangeException(nameof(sourceStarterValue)) }));
    public static bool IsRival(TrainerId id) => id.Value is "pret:TRAINER_RIVAL_POKEMON_TOWER_SQUIRTLE" or "pret:TRAINER_RIVAL_POKEMON_TOWER_BULBASAUR" or "pret:TRAINER_RIVAL_POKEMON_TOWER_CHARMANDER";
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(PokemonTowerTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("PokemonTower requires the exact one-hundred-and-one-species catalog and one initial source PokemonTower team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(PokemonTowerTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !BattleBindings.TryGetValue(b.Trainer,out var e) || b.Mode!=(IsRival(b.Trainer) ? 3 : e.PostScript is null ? 0 : 2) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(IsRival(b.Trainer) ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != e.PostScript || b.MusicOption != e.Music)
            throw new NotSupportedException("PokemonTower initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
