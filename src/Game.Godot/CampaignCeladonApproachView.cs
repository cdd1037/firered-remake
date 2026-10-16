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

    private static string CaptureBallName(CaptureBall ball)=>ball switch
    {
        CaptureBall.Poke=>"POKé BALL", CaptureBall.Great=>"GREAT BALL",
        CaptureBall.Ultra=>"ULTRA BALL", CaptureBall.Master=>"MASTER BALL",
        CaptureBall.Net=>"NET BALL", CaptureBall.Nest=>"NEST BALL",
        CaptureBall.Luxury=>"LUXURY BALL",
        _=>throw new InvalidDataException("Unsupported visible capture ball: "+ball)
    };
}
