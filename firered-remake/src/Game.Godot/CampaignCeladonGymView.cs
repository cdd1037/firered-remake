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
        if(!_catalog.CeladonGymEnabled)return;
        var source=_catalog.CeladonGymPokemon??throw new InvalidDataException("Missing CeladonGym Pokemon.");
        Image Decode(byte[] bytes,int width,int height)
        {
            var image=new Image();
            if(image.LoadPngFromBuffer(bytes)!=Error.Ok){image.Dispose();throw new InvalidDataException("Invalid CeladonGym PNG.");}
            image.Convert(Image.Format.Rgba8);
            if(image.GetWidth()!=width||image.GetHeight()!=height)
            {image.Dispose();throw new InvalidDataException("CeladonGym source pixels differ.");}
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
        var art=CeladonGymPresentationAssets.Load(_root);
        foreach(var id in new[]{"beauty-front","cooltrainer-f-front","erika-front"})
        {
            using var image=NativeImages.LoadPng(art[id+".png"], 64, 64);
            _forestTrainerPortraits.Add(id,ImageTexture.CreateFromImage(image));
        }
        foreach(var icon in PretCeladonGymItems.LoadProject(_root).Icons)
        {
            
            if(!(_earlyItemIcons.TryGetValue(icon.Item,out var existing)))_earlyItemIcons.Add(icon.Item,ItemTexture("celadon-gym-items", icon));
        }
    }

    private ForestMessage? CeladonGymWildMessage(WildBattleEvent current)=>!_catalog.CeladonGymEnabled?null:current switch
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
