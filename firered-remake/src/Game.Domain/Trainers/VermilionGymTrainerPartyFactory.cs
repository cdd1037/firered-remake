using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record VermilionGymTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>All four initial Vermilion Gym teams, with exact source NPC construction.</summary>
public static class VermilionGymTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post)
    {
        public bool Rival => Entry.StartsWith("SSAnne_2F_Corridor_EventScript_Rival", StringComparison.Ordinal);
    }
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_SAILOR_DWAYNE")] = new("VermilionCity_Gym_EventScript_Dwayne", "VermilionCity_Gym_Text_DwayneIntro", "VermilionCity_Gym_Text_DwayneDefeat", "VermilionCity_Gym_Text_DwaynePostBattle"),
        [new("pret:TRAINER_ENGINEER_BAILY")] = new("VermilionCity_Gym_EventScript_Baily", "VermilionCity_Gym_Text_BailyIntro", "VermilionCity_Gym_Text_BailyDefeat", "VermilionCity_Gym_Text_BailyPostBattle"),
        [new("pret:TRAINER_GENTLEMAN_TUCKER")] = new("VermilionCity_Gym_EventScript_Tucker", "VermilionCity_Gym_Text_TuckerIntro", "VermilionCity_Gym_Text_TuckerDefeat", "VermilionCity_Gym_Text_TuckerPostBattle"),
        [new("pret:TRAINER_LEADER_LT_SURGE")] = new("VermilionCity_Gym_EventScript_LtSurge", "VermilionCity_Gym_Text_LtSurgeIntro", "VermilionCity_Gym_Text_LtSurgeDefeat", "VermilionCity_Gym_Text_LtSurgePostBattle"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerId LtSurge { get; } = new("pret:TRAINER_LEADER_LT_SURGE");
    public static TrainerPartyCreationResult Create(VermilionGymTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("VermilionGym requires the exact seventy-three-species catalog and one source Gym team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        // Source custom-move parties and natural-move parties share exact NPC construction.
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(VermilionGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !BattleBindings.TryGetValue(b.Trainer,out var e) || b.Mode!=(e.Rival?3:0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(e.Rival?"EventScript_DoNoIntroTrainerBattle":"EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != (b.Trainer == LtSurge ? "VermilionCity_Gym_EventScript_DefeatedLtSurge" : b.Trainer.Value == "pret:TRAINER_GENTLEMAN_TUCKER" ? "VermilionCity_Gym_EventScript_DefeatedTucker" : null) || b.MusicOption != (b.Trainer == LtSurge ? "NO_MUSIC" : null))
            throw new NotSupportedException("VermilionGym initial battle source differs.");
    }
}
