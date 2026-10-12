using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private string? ValidateSilphProgress(CampaignState state)
    {
        int rival=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_SILPH_CO_7F");
        int boss=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_SILPH_CO_11F");
        bool flag(string key)=>state.Script.Flags.GetValueOrDefault(key);
        if(!_content.SilphEnabled)return rival!=0||boss!=0||flag("FLAG_HIDE_SILPH_ROCKETS")||flag("FLAG_HIDE_SILPH_RIVAL")||flag("FLAG_HIDE_SAFFRON_ROCKETS")?"save_silph_progress_profile_missing":null;
        if(rival is <0 or >1||boss is <0 or >1)return "save_silph_scene_range";
        if(Game.Domain.Items.OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],Game.Domain.Items.SilphShopCatalog.CardKeyId)
            !=(flag("FLAG_HIDE_SILPH_CO_5F_CARD_KEY")?1:0))return "save_silph_card_key_receipt";
        var history=state.ForestTrainerHistory??[];
        var rivals=history.Where(h=>IsSilphRival(new(h.TrainerId))&&h.Outcome==WildBattleOutcome.Won).ToArray();
        string expected="pret:TRAINER_RIVAL_SILPH_"+(state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON") switch
        {2=>"SQUIRTLE",1=>"BULBASAUR",0=>"CHARMANDER",_=>"INVALID"});
        if(rivals.Length!=rival||rivals.Any(h=>h.TrainerId!=expected)||flag("FLAG_HIDE_SILPH_RIVAL")!=(rival==1))return "save_silph_rival_history";
        bool won=boss==1;
        if(history.Count(h=>h.TrainerId=="pret:TRAINER_BOSS_GIOVANNI_2"&&h.Outcome==WildBattleOutcome.Won)!=boss||
            flag("FLAG_HIDE_SILPH_ROCKETS")!=won||flag("FLAG_HIDE_SAFFRON_ROCKETS")!=won||flag("FLAG_HIDE_SAFFRON_CIVILIANS")==won)
            return "save_silph_liberation_history";
        // The president script itself is unconditional; source collision/trigger geometry
        // makes the completed Giovanni encounter a prerequisite for reaching his room.
        if(flag("FLAG_GOT_MASTER_BALL_FROM_SILPH")&&!won)return "save_silph_master_ball_before_liberation";
        return null;
    }
}
