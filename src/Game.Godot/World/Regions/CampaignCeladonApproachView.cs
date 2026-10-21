using Game.Content;
using Game.Domain.Capture;
using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private void LoadCeladonApproachPresentation()
    {
        var source=_catalog.CeladonApproachPokemon??throw new InvalidDataException("Missing CeladonApproach Pokemon.");

        RegisterPokemonSprites(source);

        RegisterPokemonIcons(source);
        var art=CeladonApproachPresentationAssets.Load(_root);
        using(var image=NativeImages.LoadPng(art["biker-front.png"], 64, 64))
            _forestTrainerPortraits.Add("biker-front",ImageTexture.CreateFromImage(image));
        foreach(var icon in PretCeladonApproachItems.LoadProject(_root).Icons)
        {
            
            if(!(_earlyItemIcons.TryGetValue(icon.Item,out var existing)))_earlyItemIcons.Add(icon.Item,ItemTexture("data/items/celadon-approach", icon));
        }
    }

    private string CaptureBallName(CaptureBall ball)=>_catalog.DisplayNames.ItemName(ball switch
    {
        CaptureBall.Poke=>"ITEM_POKE_BALL", CaptureBall.Great=>"ITEM_GREAT_BALL",
        CaptureBall.Ultra=>"ITEM_ULTRA_BALL", CaptureBall.Master=>"ITEM_MASTER_BALL",
        CaptureBall.Net=>"ITEM_NET_BALL", CaptureBall.Nest=>"ITEM_NEST_BALL",
        CaptureBall.Luxury=>"ITEM_LUXURY_BALL",
        _=>throw new InvalidDataException("Unsupported visible capture ball: "+ball)
    });
}
