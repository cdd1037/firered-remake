using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record SsAnneTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>All sixteen ordinary S.S. Anne teams and three rival variants, with exact source NPC construction.</summary>
public static class SsAnneTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post)
    {
        public bool Rival => Entry.StartsWith("SSAnne_2F_Corridor_EventScript_Rival", StringComparison.Ordinal);
    }
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_YOUNGSTER_TYLER")] = new("SSAnne_1F_Room2_EventScript_Tyler", "SSAnne_1F_Room2_Text_TylerIntro", "SSAnne_1F_Room2_Text_TylerDefeat", "SSAnne_1F_Room2_Text_TylerPostBattle"),
        [new("pret:TRAINER_LASS_ANN")] = new("SSAnne_1F_Room2_EventScript_Ann", "SSAnne_1F_Room2_Text_AnnIntro", "SSAnne_1F_Room2_Text_AnnDefeat", "SSAnne_1F_Room2_Text_AnnPostBattle"),
        [new("pret:TRAINER_LASS_DAWN")] = new("SSAnne_2F_Room4_EventScript_Dawn", "SSAnne_2F_Room4_Text_DawnIntro", "SSAnne_2F_Room4_Text_DawnDefeat", "SSAnne_2F_Room4_Text_DawnPostBattle"),
        [new("pret:TRAINER_SAILOR_EDMOND")] = new("SSAnne_Deck_EventScript_Edmond", "SSAnne_Deck_Text_EdmondIntro", "SSAnne_Deck_Text_EdmondDefeat", "SSAnne_Deck_Text_EdmondPostBattle"),
        [new("pret:TRAINER_SAILOR_TREVOR")] = new("SSAnne_Deck_EventScript_Trevor", "SSAnne_Deck_Text_TrevorIntro", "SSAnne_Deck_Text_TrevorDefeat", "SSAnne_Deck_Text_TrevorPostBattle"),
        [new("pret:TRAINER_SAILOR_LEONARD")] = new("SSAnne_B1F_Room4_EventScript_Leonard", "SSAnne_B1F_Room4_Text_LeonardIntro", "SSAnne_B1F_Room4_Text_LeonardDefeat", "SSAnne_B1F_Room4_Text_LeonardPostBattle"),
        [new("pret:TRAINER_SAILOR_DUNCAN")] = new("SSAnne_B1F_Room4_EventScript_Duncan", "SSAnne_B1F_Room4_Text_DuncanIntro", "SSAnne_B1F_Room4_Text_DuncanDefeat", "SSAnne_B1F_Room4_Text_DuncanPostBattle"),
        [new("pret:TRAINER_SAILOR_HUEY")] = new("SSAnne_B1F_Room2_EventScript_Huey", "SSAnne_B1F_Room2_Text_HueyIntro", "SSAnne_B1F_Room2_Text_HueyDefeat", "SSAnne_B1F_Room2_Text_HueyPostBattle"),
        [new("pret:TRAINER_SAILOR_DYLAN")] = new("SSAnne_B1F_Room3_EventScript_Dylan", "SSAnne_B1F_Room3_Text_DylanIntro", "SSAnne_B1F_Room3_Text_DylanDefeat", "SSAnne_B1F_Room3_Text_DylanPostBattle"),
        [new("pret:TRAINER_SAILOR_PHILLIP")] = new("SSAnne_B1F_Room1_EventScript_Phillip", "SSAnne_B1F_Room1_Text_PhillipIntro", "SSAnne_B1F_Room1_Text_PhillipDefeat", "SSAnne_B1F_Room1_Text_PhillipPostBattle"),
        [new("pret:TRAINER_FISHERMAN_DALE")] = new("SSAnne_2F_Room2_EventScript_Dale", "SSAnne_2F_Room2_Text_DaleIntro", "SSAnne_2F_Room2_Text_DaleDefeat", "SSAnne_2F_Room2_Text_DalePostBattle"),
        [new("pret:TRAINER_FISHERMAN_BARNY")] = new("SSAnne_B1F_Room1_EventScript_Barny", "SSAnne_B1F_Room1_Text_BarnyIntro", "SSAnne_B1F_Room1_Text_BarnyDefeat", "SSAnne_B1F_Room1_Text_BarnyPostBattle"),
        [new("pret:TRAINER_GENTLEMAN_THOMAS")] = new("SSAnne_1F_Room7_EventScript_Thomas", "SSAnne_1F_Room7_Text_ThomasIntro", "SSAnne_1F_Room7_Text_ThomasDefeat", "SSAnne_1F_Room7_Text_ThomasPostBattle"),
        [new("pret:TRAINER_GENTLEMAN_ARTHUR")] = new("SSAnne_1F_Room5_EventScript_Arthur", "SSAnne_1F_Room5_Text_ArthurIntro", "SSAnne_1F_Room5_Text_ArthurDefeat", "SSAnne_1F_Room5_Text_ArthurPostBattle"),
        [new("pret:TRAINER_RIVAL_SS_ANNE_SQUIRTLE")] = new("SSAnne_2F_Corridor_EventScript_RivalSquirtle", "SSAnne_2F_Corridor_Text_RivalIntro", "SSAnne_2F_Corridor_Text_RivalDefeat", "SSAnne_2F_Corridor_Text_RivalPostBattle"),
        [new("pret:TRAINER_RIVAL_SS_ANNE_BULBASAUR")] = new("SSAnne_2F_Corridor_EventScript_RivalBulbasaur", "SSAnne_2F_Corridor_Text_RivalIntro", "SSAnne_2F_Corridor_Text_RivalDefeat", "SSAnne_2F_Corridor_Text_RivalPostBattle"),
        [new("pret:TRAINER_RIVAL_SS_ANNE_CHARMANDER")] = new("SSAnne_2F_Corridor_EventScript_RivalCharmander", "SSAnne_2F_Corridor_Text_RivalIntro", "SSAnne_2F_Corridor_Text_RivalDefeat", "SSAnne_2F_Corridor_Text_RivalPostBattle"),
        [new("pret:TRAINER_GENTLEMAN_BROOKS")] = new("SSAnne_2F_Room2_EventScript_Brooks", "SSAnne_2F_Room2_Text_BrooksIntro", "SSAnne_2F_Room2_Text_BrooksDefeat", "SSAnne_2F_Room2_Text_BrooksPostBattle"),
        [new("pret:TRAINER_GENTLEMAN_LAMAR")] = new("SSAnne_2F_Room4_EventScript_Lamar", "SSAnne_2F_Room4_Text_LamarIntro", "SSAnne_2F_Room4_Text_LamarDefeat", "SSAnne_2F_Room4_Text_LamarPostBattle"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerId ForPlayerStarter(SpeciesId starter) => starter.Value switch
    {
        "pret:SPECIES_BULBASAUR" => new("pret:TRAINER_RIVAL_SS_ANNE_SQUIRTLE"),
        "pret:SPECIES_CHARMANDER" => new("pret:TRAINER_RIVAL_SS_ANNE_BULBASAUR"),
        "pret:SPECIES_SQUIRTLE" => new("pret:TRAINER_RIVAL_SS_ANNE_CHARMANDER"),
        _ => throw new NotSupportedException("Unknown original starter.")
    };
    public static TrainerPartyCreationResult Create(SsAnneTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("SsAnne requires the exact seventy-three-species catalog and one source ship team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        // The ship rival also uses the default-move party format. AI flags are independent.
        return TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
    }
    public static void ValidateCatalog(SsAnneTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !BattleBindings.TryGetValue(b.Trainer,out var e) || b.Mode!=(e.Rival?3:0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(e.Rival?"EventScript_DoNoIntroTrainerBattle":"EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null)
            throw new NotSupportedException("SsAnne initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
