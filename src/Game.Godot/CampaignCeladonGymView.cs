using Game.Content;
using Game.Domain.Capture;
using Game.Domain.Items;
using Game.Application;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{
    private void LoadCeladonGymPresentation()
    {
        var source=_catalog.CeladonGymPokemon??throw new InvalidDataException("Missing CeladonGym Pokemon.");

        RegisterPokemonSprites(source);

        RegisterPokemonIcons(source);
        var art=CeladonGymPresentationAssets.Load(_root);
        foreach(var id in new[]{"beauty-front","cooltrainer-f-front","erika-front"})
        {
            using var image=NativeImages.LoadPng(art[id+".png"], 64, 64);
            _forestTrainerPortraits.Add(id,ImageTexture.CreateFromImage(image));
        }
        foreach(var icon in PretCeladonGymItems.LoadProject(_root).Icons)
        {
            
            if(!(_earlyItemIcons.TryGetValue(icon.Item,out var existing)))_earlyItemIcons.Add(icon.Item,ItemTexture("data/items/celadon-gym", icon));
        }
    }

    private ForestMessage? CeladonGymWildMessage(WildBattleEvent current)=>current switch
    {
        WildBattleRootsChangedEvent {Rooted:true} roots=>new("sText_PkmnPlantedRoots",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(roots.Side))),
        WildBattleRootsChangedEvent=>new(""),
        WildBattleIngrainHealEvent heal=>new("sText_PkmnAbsorbedNutrients",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(heal.Side))),
        WildBattleMoveFailedEvent {Reason:"already-rooted"}=>new("sText_ButItFailed"),
        WildBattleMoveFailedEvent {Reason:"rooted"} failed=>new("sText_PkmnAnchoredItself",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(failed.Target))),
        _=>null
    };
    private bool CeladonGymSilentEvent(CampaignWildBattle battle)=>WildCurrentEvent(battle) is {} e&&CeladonGymWildMessage(e) is {Symbol.Length:0};
}
