using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All thirteen initial Rocket Hideout singles teams, with exact source NPC construction.</summary>
public static class RocketHideoutTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post, string? PostScript, string? Music);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_7")] = new("CeladonCity_GameCorner_EventScript_RocketGrunt", "CeladonCity_GameCorner_Text_GruntIntro", "CeladonCity_GameCorner_Text_GruntDefeat", "CeladonCity_GameCorner_Text_GruntPostBattle", "CeladonCity_GameCorner_Text_DefeatedGrunt", null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_8")] = new("RocketHideout_B1F_EventScript_Grunt1", "RocketHideout_B1F_Text_Grunt1Intro", "RocketHideout_B1F_Text_Grunt1Defeat", "RocketHideout_B1F_Text_Grunt1PostBattle", null, null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_9")] = new("RocketHideout_B1F_EventScript_Grunt2", "RocketHideout_B1F_Text_Grunt2Intro", "RocketHideout_B1F_Text_Grunt2Defeat", "RocketHideout_B1F_Text_Grunt2PostBattle", null, null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_10")] = new("RocketHideout_B1F_EventScript_Grunt3", "RocketHideout_B1F_Text_Grunt3Intro", "RocketHideout_B1F_Text_Grunt3Defeat", "RocketHideout_B1F_Text_Grunt3PostBattle", null, null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_11")] = new("RocketHideout_B1F_EventScript_Grunt4", "RocketHideout_B1F_Text_Grunt4Intro", "RocketHideout_B1F_Text_Grunt4Defeat", "RocketHideout_B1F_Text_Grunt4PostBattle", null, null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_12")] = new("RocketHideout_B1F_EventScript_Grunt5", "RocketHideout_B1F_Text_Grunt5Intro", "RocketHideout_B1F_Text_Grunt5Defeat", "RocketHideout_B1F_Text_Grunt5PostBattle", "RocketHideout_B1F_EventScript_DefeatedGrunt5", null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_13")] = new("RocketHideout_B2F_EventScript_Grunt", "RocketHideout_B2F_Text_GruntIntro", "RocketHideout_B2F_Text_GruntDefeat", "RocketHideout_B2F_Text_GruntPostBattle", null, null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_14")] = new("RocketHideout_B3F_EventScript_Grunt1", "RocketHideout_B3F_Text_Grunt1Intro", "RocketHideout_B3F_Text_Grunt1Defeat", "RocketHideout_B3F_Text_Grunt1PostBattle", null, null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_15")] = new("RocketHideout_B3F_EventScript_Grunt2", "RocketHideout_B3F_Text_Grunt2Intro", "RocketHideout_B3F_Text_Grunt2Defeat", "RocketHideout_B3F_Text_Grunt2PostBattle", null, null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_16")] = new("RocketHideout_B4F_EventScript_Grunt2", "RocketHideout_B4F_Text_Grunt2Intro", "RocketHideout_B4F_Text_Grunt2Defeat", "RocketHideout_B4F_Text_Grunt2PostBattle", "RocketHideout_B4F_EventScript_DefeatedGrunt2", null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_17")] = new("RocketHideout_B4F_EventScript_Grunt3", "RocketHideout_B4F_Text_Grunt3Intro", "RocketHideout_B4F_Text_Grunt3Defeat", "RocketHideout_B4F_Text_Grunt3PostBattle", "RocketHideout_B4F_EventScript_DefeatedGrunt3", null),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_18")] = new("RocketHideout_B4F_EventScript_Grunt1", "RocketHideout_B4F_Text_Grunt1Intro", "RocketHideout_B4F_Text_Grunt1Defeat", "RocketHideout_B4F_Text_Grunt1PostBattle", "RocketHideout_B4F_EventScript_DefeatedGrunt1", null),
        [new("pret:TRAINER_BOSS_GIOVANNI")] = new("RocketHideout_B4F_EventScript_Giovanni", "", "RocketHideout_B4F_Text_GiovanniDefeat", "RocketHideout_B4F_Text_GiovanniPostBattle", null, null),
    }.ToImmutableDictionary();
    public static TrainerId Giovanni { get; } = new("pret:TRAINER_BOSS_GIOVANNI");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(RocketHideoutTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("RocketHideout requires the exact ninety-nine-species catalog and one initial source RocketHideout team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(RocketHideoutTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !BattleBindings.TryGetValue(b.Trainer,out var e) || b.Mode!=(b.Trainer==Giovanni ? 3 : e.PostScript is null ? 0 : 2) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(b.Trainer==Giovanni ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != e.PostScript || b.MusicOption != e.Music)
            throw new NotSupportedException("RocketHideout initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
