using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record BillTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles)
{
    /// <summary>Additional source call sites; trainer definitions may belong to another catalog.</summary>
    public ImmutableArray<CeruleanTrainerBattleDefinition> AdditionalOccurrences { get; init; } = [];
}

/// <summary>Exact nineteen initial Bill-route teams, no rematches. Reuses original NPC construction.</summary>
public static class BillTrainerPartyFactory
{
    public static TrainerId Rocket { get; } = new("pret:TRAINER_TEAM_ROCKET_GRUNT_6");

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_BUG_CATCHER_CALE")] = new("Route24_EventScript_Cale", "Route24_Text_CaleIntro", "Route24_Text_CaleDefeat", "Route24_Text_CalePostBattle"),
        [new("pret:TRAINER_CAMPER_ETHAN")] = new("Route24_EventScript_Ethan", "Route24_Text_EthanIntro", "Route24_Text_EthanDefeat", "Route24_Text_EthanPostBattle"),
        [new("pret:TRAINER_CAMPER_FLINT")] = new("Route25_EventScript_Flint", "Route25_Text_FlintIntro", "Route25_Text_FlintDefeat", "Route25_Text_FlintPostBattle"),
        [new("pret:TRAINER_CAMPER_SHANE")] = new("Route24_EventScript_Shane", "Route24_Text_ShaneIntro", "Route24_Text_ShaneDefeat", "Route24_Text_ShanePostBattle"),
        [new("pret:TRAINER_HIKER_FRANKLIN")] = new("Route25_EventScript_Franklin", "Route25_Text_FranklinIntro", "Route25_Text_FranklinDefeat", "Route25_Text_FranklinPostBattle"),
        [new("pret:TRAINER_HIKER_NOB")] = new("Route25_EventScript_Nob", "Route25_Text_NobIntro", "Route25_Text_NobDefeat", "Route25_Text_NobPostBattle"),
        [new("pret:TRAINER_HIKER_WAYNE")] = new("Route25_EventScript_Wayne", "Route25_Text_WayneIntro", "Route25_Text_WayneDefeat", "Route25_Text_WaynePostBattle"),
        [new("pret:TRAINER_LASS_ALI")] = new("Route24_EventScript_Ali", "Route24_Text_AliIntro", "Route24_Text_AliDefeat", "Route24_Text_AliPostBattle"),
        [new("pret:TRAINER_LASS_HALEY")] = new("Route25_EventScript_Haley", "Route25_Text_HaleyIntro", "Route25_Text_HaleyDefeat", "Route25_Text_HaleyPostBattle"),
        [new("pret:TRAINER_LASS_RELI")] = new("Route24_EventScript_Reli", "Route24_Text_ReliIntro", "Route24_Text_ReliDefeat", "Route24_Text_ReliPostBattle"),
        [new("pret:TRAINER_PICNICKER_KELSEY")] = new("Route25_EventScript_Kelsey", "Route25_Text_KelseyIntro", "Route25_Text_KelseyDefeat", "Route25_Text_KelseyPostBattle"),
        [new("pret:TRAINER_RIVAL_CERULEAN_BULBASAUR")] = new("CeruleanCity_EventScript_RivalBulbasaur", "", "CeruleanCity_Text_RivalDefeat", ""),
        [new("pret:TRAINER_RIVAL_CERULEAN_CHARMANDER")] = new("CeruleanCity_EventScript_RivalCharmander", "", "CeruleanCity_Text_RivalDefeat", ""),
        [new("pret:TRAINER_RIVAL_CERULEAN_SQUIRTLE")] = new("CeruleanCity_EventScript_RivalSquirtle", "", "CeruleanCity_Text_RivalDefeat", ""),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_6")] = new("Route24_EventScript_BattleRocket", "", "Route24_Text_RocketDefeat", "Route24_Text_YoudBecomeTopRocketLeader"),
        [new("pret:TRAINER_YOUNGSTER_CHAD")] = new("Route25_EventScript_Chad", "Route25_Text_ChadIntro", "Route25_Text_ChadDefeat", "Route25_Text_ChadPostBattle"),
        [new("pret:TRAINER_YOUNGSTER_DAN")] = new("Route25_EventScript_Dan", "Route25_Text_DanIntro", "Route25_Text_DanDefeat", "Route25_Text_DanPostBattle"),
        [new("pret:TRAINER_YOUNGSTER_JOEY")] = new("Route25_EventScript_Joey", "Route25_Text_JoeyIntro", "Route25_Text_JoeyDefeat", "Route25_Text_JoeyPostBattle"),
        [new("pret:TRAINER_YOUNGSTER_TIMMY")] = new("Route24_EventScript_Timmy", "Route24_Text_TimmyIntro", "Route24_Text_TimmyDefeat", "Route24_Text_TimmyPostBattle"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerId ForPlayerStarter(SpeciesId starter) => CeruleanTrainerPartyFactory.ForPlayerStarter(starter);
    public static TrainerPartyCreationResult Create(BillTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("Bill requires the exact sixty-eight-species catalog and one initial team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        var created=TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        return trainer.PartyFlags==0 ? created : created with { Party=created.Party.Select((m,i)=>m with { Pokemon=m.Pokemon with {
            Moves=trainer.Party[i].CustomMoves.TakeWhile(x=>x.Value!="pret:MOVE_NONE").Select(x=>new PokemonMoveSlot(x,pokemon.Moves[x].Pp,pokemon.Moves[x].Pp)).ToImmutableArray()
        }}).ToImmutableArray() };
    }
    public static void ValidateCatalog(BillTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !BattleBindings.TryGetValue(b.Trainer,out var e) || b.Mode!=(e.Intro==""?3:0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!=(e.Intro==""?"EventScript_DoNoIntroTrainerBattle":"EventScript_TryDoNormalTrainerBattle") ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript is not null || b.MusicOption is not null)
            throw new NotSupportedException("Bill initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
