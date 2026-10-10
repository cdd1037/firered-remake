using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;
public sealed partial class CampaignSession
{
    private static PokemonInstance BattleDisplayPokemon(WildBattleCombatant mon)=>WildBattleTransformationRules.PersistentPokemon(mon) with{Moves=mon.Pokemon.Moves};
    private static CampaignWildAppearance? BattleAppearance(WildBattleCombatant mon)=>mon.Transform is {} t ? new(t.CopiedPokemon.Species,t.CopiedPokemon.Personality,t.CopiedPokemon.OriginalTrainerId):null;
    private static CampaignWildBattle ApplyTransformDisplay(CampaignWildBattle b,WildBattleTransformedEvent changed)
    {
        var appearance=new CampaignWildAppearance(changed.AppearanceSpecies,changed.AppearancePersonality,changed.AppearanceOriginalTrainerId);
        return changed.Actor==WildBattleSide.Player
            ?b with{DisplayPlayer=b.DisplayPlayer with{Moves=changed.BattleView.Moves},DisplayPlayerAppearance=appearance}
            :b with{DisplayOpponent=b.DisplayOpponent with{Moves=changed.BattleView.Moves},DisplayOpponentAppearance=appearance};
    }
}
