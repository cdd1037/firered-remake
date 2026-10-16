using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>Seven original Fuchsia Gym teams, with exact source NPC construction. Creation does not admit battle effects.</summary>
public static class FuchsiaGymTrainerPartyFactory
{

    internal sealed record BattleBinding(string Entry, string Intro, string Defeat, string Post, string? PostScript, string? Music);
    private static readonly ImmutableDictionary<TrainerId, BattleBinding> BattleBindings = new Dictionary<TrainerId, BattleBinding>
    {
        [new("pret:TRAINER_JUGGLER_KAYDEN")] = new("FuchsiaCity_Gym_EventScript_Kayden", "FuchsiaCity_Gym_Text_KaydenIntro", "FuchsiaCity_Gym_Text_KaydenDefeat", "FuchsiaCity_Gym_Text_KaydenPostBattle", null, null),
        [new("pret:TRAINER_JUGGLER_SHAWN")] = new("FuchsiaCity_Gym_EventScript_Shawn", "FuchsiaCity_Gym_Text_ShawnIntro", "FuchsiaCity_Gym_Text_ShawnDefeat", "FuchsiaCity_Gym_Text_ShawnPostBattle", null, null),
        [new("pret:TRAINER_JUGGLER_KIRK")] = new("FuchsiaCity_Gym_EventScript_Kirk", "FuchsiaCity_Gym_Text_KirkIntro", "FuchsiaCity_Gym_Text_KirkDefeat", "FuchsiaCity_Gym_Text_KirkPostBattle", null, null),
        [new("pret:TRAINER_TAMER_EDGAR")] = new("FuchsiaCity_Gym_EventScript_Edgar", "FuchsiaCity_Gym_Text_EdgarIntro", "FuchsiaCity_Gym_Text_EdgarDefeat", "FuchsiaCity_Gym_Text_EdgarPostBattle", null, null),
        [new("pret:TRAINER_TAMER_PHIL")] = new("FuchsiaCity_Gym_EventScript_Phil", "FuchsiaCity_Gym_Text_PhilIntro", "FuchsiaCity_Gym_Text_PhilDefeat", "FuchsiaCity_Gym_Text_PhilPostBattle", null, null),
        [new("pret:TRAINER_JUGGLER_NATE")] = new("FuchsiaCity_Gym_EventScript_Nate", "FuchsiaCity_Gym_Text_NateIntro", "FuchsiaCity_Gym_Text_NateDefeat", "FuchsiaCity_Gym_Text_NatePostBattle", null, null),
        [new("pret:TRAINER_LEADER_KOGA")] = new("FuchsiaCity_Gym_EventScript_Koga", "FuchsiaCity_Gym_Text_KogaIntro", "FuchsiaCity_Gym_Text_KogaDefeat", "FuchsiaCity_Gym_Text_KogaPostBattle", "FuchsiaCity_Gym_EventScript_DefeatedKoga", "NO_MUSIC"),
    }.ToImmutableDictionary();
    public static TrainerId Koga { get; } = new("pret:TRAINER_LEADER_KOGA");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = BattleBindings.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray();
    public static TrainerPartyCreationResult Create(FuchsiaGymTrainerCatalog trainers, PokemonCatalog pokemon, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(id, out var trainer))
            throw new NotSupportedException("FuchsiaGym requires the exact107-species catalog and one initial source FuchsiaGym team.");
        if (trainer.Party.Any(member => !pokemon.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("trainer_party_species");
        var created = TrainerPartyFactory.CreateValidated(trainer,pokemon,rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member,i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m=>m.Value!="pret:MOVE_NONE")
                .Select(m=>new PokemonMoveSlot(m,pokemon.Moves[m].Pp,pokemon.Moves[m].Pp)).ToImmutableArray() } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(FuchsiaGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        if(b is null || !BattleBindings.TryGetValue(b.Trainer,out var e) || b.Mode!=(b.Trainer==Koga ? 1 : 0) || b.RivalFlags!=0 || b.BattleTypeFlags!=8 ||
            b.ScriptEntry!=e.Entry || b.IntroText!=e.Intro || b.DefeatText!=e.Defeat || b.PostBattleText!=e.Post || b.VictoryText!="" ||
            b.EntryScript!="EventScript_TryDoNormalTrainerBattle" ||
            b.Callback!="CB2_EndTrainerBattle" || b.PostBattleScript != e.PostScript || b.MusicOption != e.Music)
            throw new NotSupportedException("FuchsiaGym initial battle source differs.");
    }
    private static bool Encode(string text,ImmutableArray<int> bytes) => !bytes.IsDefault && bytes.SequenceEqual(Route3TrainerPartyFactory.Encode(text));
}
