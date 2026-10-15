using Game.Content;
using Game.Domain.Capture;
using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private void LoadCeladonApproachPresentation()
    {
        if(!_catalog.CeladonApproachEnabled)return;
        var source=_catalog.CeladonApproachPokemon??throw new InvalidDataException("Missing CeladonApproach Pokemon.");
        Image Decode(byte[] bytes,int width,int height)
        {
            var image=new Image();
            if(image.LoadPngFromBuffer(bytes)!=Error.Ok){image.Dispose();throw new InvalidDataException("Invalid CeladonApproach PNG.");}
            image.Convert(Image.Format.Rgba8);
            if(image.GetWidth()!=width||image.GetHeight()!=height)
            {image.Dispose();throw new InvalidDataException("CeladonApproach source pixels differ.");}
            return image;
        }
        foreach(var sprite in source.Sprites)
        {
            using var image=Decode(source.ReadSpriteTexture(new(sprite.SpeciesId),sprite.Side,sprite.PaletteVariant=="shiny"),64,64);
            _battleArt.Add((sprite.SourceSpeciesId,sprite.Side,sprite.PaletteVariant=="shiny"),new(ImageTexture.CreateFromImage(image),new(sprite.SingleBattleTopLeft.X,sprite.SingleBattleTopLeft.Y)));
        }
        foreach(var footprint in source.Footprints)
        {
            using var image=Decode(source.ReadFootprintTexture(footprint.SourceSpeciesId),16,16);
            _route22Footprints.Add(footprint.SourceSpeciesId,ImageTexture.CreateFromImage(image));
        }
        foreach(var icon in source.Icons)
        {
            using var image=Decode(source.ReadIconTexture(new(icon.SpeciesId)),32,32);
            var rgba=image.GetData();
            for(int i=0;i<rgba.Length;i+=4)if(rgba[i+3]!=0)for(int c=0;c<3;c++){int value=rgba[i+c]>>3;rgba[i+c]=(byte)((value<<3)|(value>>2));}
            using var display=Image.CreateFromData(32,32,false,Image.Format.Rgba8,rgba);
            _storageIcons.Add(new(icon.SpeciesId),ImageTexture.CreateFromImage(display));
        }
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
