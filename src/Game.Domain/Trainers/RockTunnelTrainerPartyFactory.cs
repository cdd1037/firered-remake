using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>All thirty initial Route9, Route10 and RockTunnel teams, with exact source NPC construction.</summary>
public static class RockTunnelTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_PICNICKER_ALICIA")] = new("Route9_EventScript_Alicia", "Route9_Text_AliciaIntro", "Route9_Text_AliciaDefeat", "Route9_Text_AliciaPostBattle"),
        [new("pret:TRAINER_HIKER_JEREMY")] = new("Route9_EventScript_Jeremy", "Route9_Text_JeremyIntro", "Route9_Text_JeremyDefeat", "Route9_Text_JeremyPostBattle"),
        [new("pret:TRAINER_HIKER_ALAN")] = new("Route9_EventScript_Alan", "Route9_Text_AlanIntro", "Route9_Text_AlanDefeat", "Route9_Text_AlanPostBattle"),
        [new("pret:TRAINER_CAMPER_CHRIS")] = new("Route9_EventScript_Chris", "Route9_Text_ChrisIntro", "Route9_Text_ChrisDefeat", "Route9_Text_ChrisPostBattle"),
        [new("pret:TRAINER_BUG_CATCHER_BRENT")] = new("Route9_EventScript_Brent", "Route9_Text_BrentIntro", "Route9_Text_BrentDefeat", "Route9_Text_BrentPostBattle"),
        [new("pret:TRAINER_BUG_CATCHER_CONNER")] = new("Route9_EventScript_Conner", "Route9_Text_ConnerIntro", "Route9_Text_ConnerDefeat", "Route9_Text_ConnerPostBattle"),
        [new("pret:TRAINER_HIKER_BRICE")] = new("Route9_EventScript_Brice", "Route9_Text_BriceIntro", "Route9_Text_BriceDefeat", "Route9_Text_BricePostBattle"),
        [new("pret:TRAINER_PICNICKER_CAITLIN")] = new("Route9_EventScript_Caitlin", "Route9_Text_CaitlinIntro", "Route9_Text_CaitlinDefeat", "Route9_Text_CaitlinPostBattle"),
        [new("pret:TRAINER_CAMPER_DREW")] = new("Route9_EventScript_Drew", "Route9_Text_DrewIntro", "Route9_Text_DrewDefeat", "Route9_Text_DrewPostBattle"),
        [new("pret:TRAINER_PICNICKER_CAROL")] = new("Route10_EventScript_Carol", "Route10_Text_CarolIntro", "Route10_Text_CarolDefeat", "Route10_Text_CarolPostBattle"),
        [new("pret:TRAINER_HIKER_CLARK")] = new("Route10_EventScript_Clark", "Route10_Text_ClarkIntro", "Route10_Text_ClarkDefeat", "Route10_Text_ClarkPostBattle"),
        [new("pret:TRAINER_POKEMANIAC_HERMAN")] = new("Route10_EventScript_Herman", "Route10_Text_HermanIntro", "Route10_Text_HermanDefeat", "Route10_Text_HermanPostBattle"),
        [new("pret:TRAINER_HIKER_TRENT")] = new("Route10_EventScript_Trent", "Route10_Text_TrentIntro", "Route10_Text_TrentDefeat", "Route10_Text_TrentPostBattle"),
        [new("pret:TRAINER_POKEMANIAC_MARK")] = new("Route10_EventScript_Mark", "Route10_Text_MarkIntro", "Route10_Text_MarkDefeat", "Route10_Text_MarkPostBattle"),
        [new("pret:TRAINER_PICNICKER_HEIDI")] = new("Route10_EventScript_Heidi", "Route10_Text_HeidiIntro", "Route10_Text_HeidiDefeat", "Route10_Text_HeidiPostBattle"),
        [new("pret:TRAINER_PICNICKER_DANA")] = new("RockTunnel_1F_EventScript_Dana", "RockTunnel_1F_Text_DanaIntro", "RockTunnel_1F_Text_DanaDefeat", "RockTunnel_1F_Text_DanaPostBattle"),
        [new("pret:TRAINER_PICNICKER_ARIANA")] = new("RockTunnel_1F_EventScript_Ariana", "RockTunnel_1F_Text_ArianaIntro", "RockTunnel_1F_Text_ArianaDefeat", "RockTunnel_1F_Text_ArianaPostBattle"),
        [new("pret:TRAINER_PICNICKER_LEAH")] = new("RockTunnel_1F_EventScript_Leah", "RockTunnel_1F_Text_LeahIntro", "RockTunnel_1F_Text_LeahDefeat", "RockTunnel_1F_Text_LeahPostBattle"),
        [new("pret:TRAINER_HIKER_LUCAS")] = new("RockTunnel_1F_EventScript_Lucas", "RockTunnel_1F_Text_LucasIntro", "RockTunnel_1F_Text_LucasDefeat", "RockTunnel_1F_Text_LucasPostBattle"),
        [new("pret:TRAINER_HIKER_OLIVER")] = new("RockTunnel_1F_EventScript_Oliver", "RockTunnel_1F_Text_OliverIntro", "RockTunnel_1F_Text_OliverDefeat", "RockTunnel_1F_Text_OliverPostBattle"),
        [new("pret:TRAINER_HIKER_LENNY")] = new("RockTunnel_1F_EventScript_Lenny", "RockTunnel_1F_Text_LennyIntro", "RockTunnel_1F_Text_LennyDefeat", "RockTunnel_1F_Text_LennyPostBattle"),
        [new("pret:TRAINER_POKEMANIAC_ASHTON")] = new("RockTunnel_1F_EventScript_Ashton", "RockTunnel_1F_Text_AshtonIntro", "RockTunnel_1F_Text_AshtonDefeat", "RockTunnel_1F_Text_AshtonPostBattle"),
        [new("pret:TRAINER_POKEMANIAC_COOPER")] = new("RockTunnel_B1F_EventScript_Cooper", "RockTunnel_B1F_Text_CooperIntro", "RockTunnel_B1F_Text_CooperDefeat", "RockTunnel_B1F_Text_CooperPostBattle"),
        [new("pret:TRAINER_HIKER_DUDLEY")] = new("RockTunnel_B1F_EventScript_Dudley", "RockTunnel_B1F_Text_DudleyIntro", "RockTunnel_B1F_Text_DudleyDefeat", "RockTunnel_B1F_Text_DudleyPostBattle"),
        [new("pret:TRAINER_PICNICKER_SOFIA")] = new("RockTunnel_B1F_EventScript_Sofia", "RockTunnel_B1F_Text_SofiaIntro", "RockTunnel_B1F_Text_SofiaDefeat", "RockTunnel_B1F_Text_SofiaPostBattle"),
        [new("pret:TRAINER_HIKER_ALLEN")] = new("RockTunnel_B1F_EventScript_Allen", "RockTunnel_B1F_Text_AllenIntro", "RockTunnel_B1F_Text_AllenDefeat", "RockTunnel_B1F_Text_AllenPostBattle"),
        [new("pret:TRAINER_HIKER_ERIC")] = new("RockTunnel_B1F_EventScript_Eric", "RockTunnel_B1F_Text_EricIntro", "RockTunnel_B1F_Text_EricDefeat", "RockTunnel_B1F_Text_EricPostBattle"),
        [new("pret:TRAINER_POKEMANIAC_STEVE")] = new("RockTunnel_B1F_EventScript_Steve", "RockTunnel_B1F_Text_SteveIntro", "RockTunnel_B1F_Text_SteveDefeat", "RockTunnel_B1F_Text_StevePostBattle"),
        [new("pret:TRAINER_PICNICKER_MARTHA")] = new("RockTunnel_B1F_EventScript_Martha", "RockTunnel_B1F_Text_MarthaIntro", "RockTunnel_B1F_Text_MarthaDefeat", "RockTunnel_B1F_Text_MarthaPostBattle"),
        [new("pret:TRAINER_POKEMANIAC_WINSTON")] = new("RockTunnel_B1F_EventScript_Winston", "RockTunnel_B1F_Text_WinstonIntro", "RockTunnel_B1F_Text_WinstonDefeat", "RockTunnel_B1F_Text_WinstonPostBattle"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(RockTunnelTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("RockTunnel requires the exact ninety-three-species catalog and one initial source RockTunnel team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        // Source custom parties share the original deterministic PID/OT construction.
        return TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
    }
    public static void ValidateCatalog(RockTunnelTrainerCatalog catalog)
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
            throw new NotSupportedException("RockTunnel initial battle source differs.");
    }
}
