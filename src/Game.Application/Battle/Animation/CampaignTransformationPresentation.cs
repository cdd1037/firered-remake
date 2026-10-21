using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;
public sealed partial class CampaignSession
{
    private static PokemonInstance BattleDisplayPokemon(WildBattleCombatant mon)=>WildBattleTransformationRules.PersistentPokemon(mon) with{Moves=mon.Pokemon.Moves};
    private static CampaignWildAppearance? BattleAppearance(WildBattleCombatant mon)=>mon.Transform is {} t ? new(t.CopiedPokemon.Species,t.CopiedPokemon.Personality,t.CopiedPokemon.OriginalTrainerId):null;
}
