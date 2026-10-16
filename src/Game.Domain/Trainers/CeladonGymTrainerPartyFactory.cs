using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All eight initial Route8 singles teams, with exact source NPC construction.</summary>
public static class CeladonGymTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post, string? PostScript, string? Music);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_LASS_KAY")] = new("CeladonCity_Gym_EventScript_Kay", "CeladonCity_Gym_Text_KayIntro", "CeladonCity_Gym_Text_KayDefeat", "CeladonCity_Gym_Text_KayPostBattle", null, null),
        [new("pret:TRAINER_BEAUTY_BRIDGET")] = new("CeladonCity_Gym_EventScript_Bridget", "CeladonCity_Gym_Text_BridgetIntro", "CeladonCity_Gym_Text_BridgetDefeat", "CeladonCity_Gym_Text_BridgetPostBattle", null, null),
        [new("pret:TRAINER_PICNICKER_TINA")] = new("CeladonCity_Gym_EventScript_Tina", "CeladonCity_Gym_Text_TinaIntro", "CeladonCity_Gym_Text_TinaDefeat", "CeladonCity_Gym_Text_TinaPostBattle", null, null),
        [new("pret:TRAINER_BEAUTY_TAMIA")] = new("CeladonCity_Gym_EventScript_Tamia", "CeladonCity_Gym_Text_TamiaIntro", "CeladonCity_Gym_Text_TamiaDefeat", "CeladonCity_Gym_Text_TamiaPostBattle", null, null),
        [new("pret:TRAINER_BEAUTY_LORI")] = new("CeladonCity_Gym_EventScript_Lori", "CeladonCity_Gym_Text_LoriIntro", "CeladonCity_Gym_Text_LoriDefeat", "CeladonCity_Gym_Text_LoriPostBattle", null, null),
        [new("pret:TRAINER_LASS_LISA")] = new("CeladonCity_Gym_EventScript_Lisa", "CeladonCity_Gym_Text_LisaIntro", "CeladonCity_Gym_Text_LisaDefeat", "CeladonCity_Gym_Text_LisaPostBattle", null, null),
        [new("pret:TRAINER_COOLTRAINER_MARY")] = new("CeladonCity_Gym_EventScript_Mary", "CeladonCity_Gym_Text_MaryIntro", "CeladonCity_Gym_Text_MaryDefeat", "CeladonCity_Gym_Text_MaryPostBattle", null, null),
        [new("pret:TRAINER_LEADER_ERIKA")] = new("CeladonCity_Gym_EventScript_Erika", "CeladonCity_Gym_Text_ErikaIntro", "CeladonCity_Gym_Text_ErikaDefeat", "CeladonCity_Gym_Text_ErikaPostBattle", "CeladonCity_Gym_EventScript_DefeatedErika", "NO_MUSIC"),
    }.ToImmutableDictionary();
    public static TrainerId Erika { get; } = new("pret:TRAINER_LEADER_ERIKA");
    public static TrainerId Mary { get; } = new("pret:TRAINER_COOLTRAINER_MARY");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(CeladonGymTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("CeladonGym requires the exact ninety-eight-species catalog and one initial source CeladonGym team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(CeladonGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !BattleBindings.TryGetValue(b.Trainer,out var e) || b.Mode!=(b.Trainer==Erika ? 1 : 0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!="EventScript_TryDoNormalTrainerBattle" ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != e.PostScript || b.MusicOption != e.Music)
            throw new NotSupportedException("CeladonGym initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
