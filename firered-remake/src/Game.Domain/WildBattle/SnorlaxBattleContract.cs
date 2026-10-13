using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.WildBattle;

/// <summary>Scripted ordinary wild flags. Application authenticates route script, actor and callback.</summary>
public static class SnorlaxBattleContract
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const int SourceBattleTypeFlags = 1 << 17;
    public const int CreationDrawCount = 4;
    public const int AiSetupDrawCount = 4;
    public const int HeldItemDrawCount = 1;
    public static string WildHeldItem(int roll) => roll is >= 0 and < 100 ? "ITEM_CHESTO_BERRY" : throw new ArgumentOutOfRangeException(nameof(roll));
    public static PokemonCreationResult Create(PokemonCatalog catalog, uint originalTrainerId, RngState rng, bool fuchsiaRules = false)
    {
        PokemonFactory.ValidateCatalog(catalog);
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) != (fuchsiaRules ? 107 : 102)) throw new NotSupportedException("snorlax_catalog_required");
        return PokemonFactory.Create(catalog, new(new("pret:SPECIES_SNORLAX"),30,originalTrainerId,IvPolicy.Random,PersonalityPolicy.Random),rng);
    }
    public static bool SourceInitialInstance(PokemonInstance mon) => mon.SourceSpeciesId == 143 && mon.Level == 30 &&
        mon.Experience == 33750 && mon.Evs == SixStats.Zero && mon.CurrentHp == mon.Stats.MaxHp && mon.Friendship == 70 &&
        mon.Moves.Select(x => (x.Move.Value,x.CurrentPp,x.MaximumPp,x.PpUps)).SequenceEqual(new[] {
            ("pret:MOVE_HEADBUTT",15,15,0),("pret:MOVE_YAWN",10,10,0),("pret:MOVE_REST",10,10,0),("pret:MOVE_SNORE",15,15,0) });
}
