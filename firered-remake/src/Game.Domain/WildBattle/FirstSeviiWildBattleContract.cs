using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.WildBattle;

public enum FirstSeviiEncounterKind { Land, Surf, OldRod }
public sealed record LostelleHypnoReturn(bool ContinueRescue, bool WhiteOut, bool PreserveCapturedPokemon);
/// <summary>The authenticated original Lostelle setwildbattle HYPNO30 entry, separate from Snorlax/Tower.</summary>
public static class FirstSeviiHypnoBattleContract
{
    public const string ScriptEntry="ThreeIsland_BerryForest_EventScript_Lostelle";
    public const string SourcePath="data/maps/ThreeIsland_BerryForest/scripts.inc";
    public const string SourceSha256="e716d5252078b670652939fe883fc8b6db45af0273e4183c82ee3939985a9342";
    public const int SourceLine=10, Level=30, SourceBattleTypeFlags=1<<17;
    public static PokemonCreationResult Create(CinnabarOwnedPokemonCatalog owned,uint playerTrainerId,RngState rng)
    {
        if(owned is null||!ReferenceEquals(CinnabarOwnedPokemonCatalog.For(owned.Catalog),owned))
            throw new ArgumentException("first_sevii_hypno_owned_proof");
        return PokemonFactory.Create(owned.Catalog,new(new("pret:SPECIES_HYPNO"),Level,playerTrainerId,
            IvPolicy.Random,PersonalityPolicy.Random),rng);
    }
    public static bool SourceInitialInstance(PokemonInstance mon) => mon.SourceSpeciesId==97 && mon.Species.Value=="pret:SPECIES_HYPNO" &&
        mon.Level==Level && mon.Experience==27000 && mon.Friendship==70 && mon.Evs==SixStats.Zero && mon.CurrentHp==mon.Stats.MaxHp &&
        mon.Moves.Select(m=>(m.Move.Value,m.CurrentPp,m.MaximumPp,m.PpUps)).SequenceEqual(new[] {
            ("pret:MOVE_CONFUSION",25,25,0),("pret:MOVE_HEADBUTT",15,15,0),
            ("pret:MOVE_POISON_GAS",40,40,0),("pret:MOVE_MEDITATE",40,40,0)});
    public static LostelleHypnoReturn Resolve(WildBattleOutcome outcome) => outcome switch {
        WildBattleOutcome.Won or WildBattleOutcome.Escaped=>new(true,false,false),
        WildBattleOutcome.Caught=>new(true,false,true),
        WildBattleOutcome.Lost or WildBattleOutcome.Draw=>new(false,true,false),
        _=>throw new ArgumentException("first_sevii_hypno_nonterminal_outcome",nameof(outcome))};
}
