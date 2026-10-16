using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record Route11TrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>All ten initial Route11 teams, with exact source NPC construction.</summary>
public static class Route11TrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_YOUNGSTER_EDDIE")] = new("Route11_EventScript_Eddie", "Route11_Text_EddieIntro", "Route11_Text_EddieDefeat", "Route11_Text_EddiePostBattle"),
        [new("pret:TRAINER_GAMER_HUGO")] = new("Route11_EventScript_Hugo", "Route11_Text_HugoIntro", "Route11_Text_HugoDefeat", "Route11_Text_HugoPostBattle"),
        [new("pret:TRAINER_YOUNGSTER_DILLON")] = new("Route11_EventScript_Dillon", "Route11_Text_DillonIntro", "Route11_Text_DillonDefeat", "Route11_Text_DillonPostBattle"),
        [new("pret:TRAINER_YOUNGSTER_DAVE")] = new("Route11_EventScript_Dave", "Route11_Text_DaveIntro", "Route11_Text_DaveDefeat", "Route11_Text_DavePostBattle"),
        [new("pret:TRAINER_GAMER_JASPER")] = new("Route11_EventScript_Jasper", "Route11_Text_JasperIntro", "Route11_Text_JasperDefeat", "Route11_Text_JasperPostBattle"),
        [new("pret:TRAINER_GAMER_DARIAN")] = new("Route11_EventScript_Darian", "Route11_Text_DarianIntro", "Route11_Text_DarianDefeat", "Route11_Text_DarianPostBattle"),
        [new("pret:TRAINER_ENGINEER_BRAXTON")] = new("Route11_EventScript_Braxton", "Route11_Text_BraxtonIntro", "Route11_Text_BraxtonDefeat", "Route11_Text_BraxtonPostBattle"),
        [new("pret:TRAINER_YOUNGSTER_YASU")] = new("Route11_EventScript_Yasu", "Route11_Text_YasuIntro", "Route11_Text_YasuDefeat", "Route11_Text_YasuPostBattle"),
        [new("pret:TRAINER_GAMER_DIRK")] = new("Route11_EventScript_Dirk", "Route11_Text_DirkIntro", "Route11_Text_DirkDefeat", "Route11_Text_DirkPostBattle"),
        [new("pret:TRAINER_ENGINEER_BERNIE")] = new("Route11_EventScript_Bernie", "Route11_Text_BernieIntro", "Route11_Text_BernieDefeat", "Route11_Text_BerniePostBattle"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(Route11TrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("Route11 requires the exact seventy-nine-species catalog and one initial source Route11 team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        // These initial route parties use source default moves; no custom or rematch party is admitted.
        return TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
    }
    public static void ValidateCatalog(Route11TrainerCatalog catalog)
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
            throw new NotSupportedException("Route11 initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
