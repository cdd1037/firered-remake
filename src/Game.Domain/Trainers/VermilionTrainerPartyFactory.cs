using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

public sealed record VermilionTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);

/// <summary>Exact six initial Vermilion-route teams, no rematches. Reuses original NPC construction.</summary>
public static class VermilionTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_BUG_CATCHER_KEIGO")] = new("Route6_EventScript_Keigo", "Route6_Text_KeigoIntro", "Route6_Text_KeigoDefeat", "Route6_Text_KeigoPostBattle"),
        [new("pret:TRAINER_CAMPER_RICKY")] = new("Route6_EventScript_Ricky", "Route6_Text_RickyIntro", "Route6_Text_RickyDefeat", "Route6_Text_RickyPostBattle"),
        [new("pret:TRAINER_PICNICKER_NANCY")] = new("Route6_EventScript_Nancy", "Route6_Text_NancyIntro", "Route6_Text_NancyDefeat", "Route6_Text_NancyPostBattle"),
        [new("pret:TRAINER_BUG_CATCHER_ELIJAH")] = new("Route6_EventScript_Elijah", "Route6_Text_ElijahIntro", "Route6_Text_ElijahDefeat", "Route6_Text_ElijahPostBattle"),
        [new("pret:TRAINER_PICNICKER_ISABELLE")] = new("Route6_EventScript_Isabelle", "Route6_Text_IsabelleIntro", "Route6_Text_IsabelleDefeat", "Route6_Text_IsabellePostBattle"),
        [new("pret:TRAINER_CAMPER_JEFF")] = new("Route6_EventScript_Jeff", "Route6_Text_JeffIntro", "Route6_Text_JeffDefeat", "Route6_Text_JeffPostBattle"),
    }.ToImmutableDictionary();
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(VermilionTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("Vermilion requires the exact seventy-species catalog and one initial team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        return TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
    }
    public static void ValidateCatalog(VermilionTrainerCatalog catalog)
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
            throw new NotSupportedException("Vermilion initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
