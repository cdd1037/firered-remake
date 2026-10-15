using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All eleven initial Route8 singles teams, with exact source NPC construction.</summary>
public static class CeladonApproachTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_LASS_JULIA")] = new("Route8_EventScript_Julia", "Route8_Text_JuliaIntro", "Route8_Text_JuliaDefeat", "Route8_Text_JuliaPostBattle"),
        [new("pret:TRAINER_GAMER_RICH")] = new("Route8_EventScript_Rich", "Route8_Text_RichIntro", "Route8_Text_RichDefeat", "Route8_Text_RichPostBattle"),
        [new("pret:TRAINER_SUPER_NERD_GLENN")] = new("Route8_EventScript_Glenn", "Route8_Text_GlennIntro", "Route8_Text_GlennDefeat", "Route8_Text_GlennPostBattle"),
        [new("pret:TRAINER_LASS_PAIGE")] = new("Route8_EventScript_Paige", "Route8_Text_PaigeIntro", "Route8_Text_PaigeDefeat", "Route8_Text_PaigePostBattle"),
        [new("pret:TRAINER_SUPER_NERD_LESLIE")] = new("Route8_EventScript_Leslie", "Route8_Text_LeslieIntro", "Route8_Text_LeslieDefeat", "Route8_Text_LesliePostBattle"),
        [new("pret:TRAINER_LASS_ANDREA")] = new("Route8_EventScript_Andrea", "Route8_Text_AndreaIntro", "Route8_Text_AndreaDefeat", "Route8_Text_AndreaPostBattle"),
        [new("pret:TRAINER_LASS_MEGAN")] = new("Route8_EventScript_Megan", "Route8_Text_MeganIntro", "Route8_Text_MeganDefeat", "Route8_Text_MeganPostBattle"),
        [new("pret:TRAINER_GAMER_STAN")] = new("Route8_EventScript_Stan", "Route8_Text_StanIntro", "Route8_Text_StanDefeat", "Route8_Text_StanPostBattle"),
        [new("pret:TRAINER_SUPER_NERD_AIDAN")] = new("Route8_EventScript_Aidan", "Route8_Text_AidanIntro", "Route8_Text_AidanDefeat", "Route8_Text_AidanPostBattle"),
        [new("pret:TRAINER_BIKER_RICARDO")] = new("Route8_EventScript_Ricardo", "Route8_Text_RicardoIntro", "Route8_Text_RicardoDefeat", "Route8_Text_RicardoPostBattle"),
        [new("pret:TRAINER_BIKER_JAREN")] = new("Route8_EventScript_Jaren", "Route8_Text_JarenIntro", "Route8_Text_JarenDefeat", "Route8_Text_JarenPostBattle"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(CeladonApproachTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("CeladonApproach requires the exact ninety-four-species catalog and one initial source CeladonApproach team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(CeladonApproachTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !BattleBindings.TryGetValue(b.Trainer,out var e) || b.Mode!=0 || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!="EventScript_TryDoNormalTrainerBattle" ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null)
            throw new NotSupportedException("CeladonApproach initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
