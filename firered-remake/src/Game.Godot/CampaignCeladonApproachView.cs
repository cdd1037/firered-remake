using System.Security.Cryptography;
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
        if(_battleArt.Count!=372||_storageIcons.Count!=93||_route22Footprints.Count!=64)throw new InvalidDataException("CeladonApproach must extend93 species.");
        Image Decode(byte[] bytes,int width,int height,string sha)
        {
            var image=new Image();
            if(image.LoadPngFromBuffer(bytes)!=Error.Ok){image.Dispose();throw new InvalidDataException("Invalid CeladonApproach PNG.");}
            image.Convert(Image.Format.Rgba8);
            if(image.GetWidth()!=width||image.GetHeight()!=height||Convert.ToHexStringLower(SHA256.HashData(image.GetData()))!=sha)
            {image.Dispose();throw new InvalidDataException("CeladonApproach source pixels differ.");}
            return image;
        }
        foreach(var sprite in source.Sprites)
        {
            using var image=Decode(source.ReadSpriteTexture(new(sprite.SpeciesId),sprite.Side,sprite.PaletteVariant=="shiny"),64,64,sprite.RgbaSha256);
            _battleArt.Add((sprite.SourceSpeciesId,sprite.Side,sprite.PaletteVariant=="shiny"),new(ImageTexture.CreateFromImage(image),new(sprite.SingleBattleTopLeft.X,sprite.SingleBattleTopLeft.Y)));
        }
        foreach(var footprint in source.Footprints)
        {
            using var image=Decode(source.ReadFootprintTexture(footprint.SourceSpeciesId),16,16,footprint.RgbaSha256);
            _route22Footprints.Add(footprint.SourceSpeciesId,ImageTexture.CreateFromImage(image));
        }
        foreach(var icon in source.Icons)
        {
            using var image=Decode(source.ReadIconTexture(new(icon.SpeciesId)),32,32,icon.RgbaSha256);
            var rgba=image.GetData();
            for(int i=0;i<rgba.Length;i+=4)if(rgba[i+3]!=0)for(int c=0;c<3;c++){int value=rgba[i+c]>>3;rgba[i+c]=(byte)((value<<3)|(value>>2));}
            using var display=Image.CreateFromData(32,32,false,Image.Format.Rgba8,rgba);
            _storageIcons.Add(new(icon.SpeciesId),ImageTexture.CreateFromImage(display));
        }
        var art=CeladonApproachPresentationAssets.Load(_root);
        using(var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,art["biker-front.rgba"]))
            _forestTrainerPortraits.Add("biker-front",ImageTexture.CreateFromImage(image));
        foreach(var icon in PretCeladonApproachItems.LoadProject(_root).Icons)
        {
            using var image=Image.CreateFromData(icon.Width,icon.Height,false,Image.Format.Rgba8,PretPewterMachines.IconRgba(icon));
            if(_earlyItemIcons.TryGetValue(icon.Item,out var existing))
            {
                using var current=existing.GetImage();current.Convert(Image.Format.Rgba8);
                if(!current.GetData().SequenceEqual(image.GetData()))throw new InvalidDataException("Historical item icon changed.");
            }
            else _earlyItemIcons.Add(icon.Item,ImageTexture.CreateFromImage(image));
        }
        if(_battleArt.Count!=376||_storageIcons.Count!=94||_route22Footprints.Count!=65)throw new InvalidDataException("CeladonApproach presentation inventory differs.");
    }

    private static string CaptureBallName(CaptureBall ball)=>ball switch
    {
        CaptureBall.Poke=>"POKé BALL", CaptureBall.Great=>"GREAT BALL",
        CaptureBall.Net=>"NET BALL", CaptureBall.Nest=>"NEST BALL",
        _=>throw new InvalidDataException("Unsupported visible capture ball: "+ball)
    };
}
