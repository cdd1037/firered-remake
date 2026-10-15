using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Optional authenticated RockTunnel assets and source event text. Application commands own battle state and RNG.</summary>
public partial class CampaignView
{
    private void LoadRockTunnelPresentation()
    {
        if (!_catalog.RockTunnelEnabled) return;
        var source = _catalog.RockTunnelPokemon ?? throw new InvalidDataException("RockTunnel Pokemon presentation is missing.");
        Image Decode(byte[] bytes, int width, int height)
        {
            var image = new Image();
            if (image.LoadPngFromBuffer(bytes) != Error.Ok) { image.Dispose(); throw new InvalidDataException("Invalid RockTunnel PNG."); }
            image.Convert(Image.Format.Rgba8);
            if (image.GetWidth() != width || image.GetHeight() != height)
            { image.Dispose(); throw new InvalidDataException("RockTunnel source pixels differ."); }
            return image;
        }
        foreach (var sprite in source.Sprites)
        {
            using var image = Decode(source.ReadSpriteTexture(new(sprite.SpeciesId), sprite.Side, sprite.PaletteVariant == "shiny"), 64, 64);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var footprint in source.Footprints)
        {
            using var image = Decode(source.ReadFootprintTexture(footprint.SourceSpeciesId), 16, 16);
            _route22Footprints.Add(footprint.SourceSpeciesId, ImageTexture.CreateFromImage(image));
        }
        foreach (var icon in source.Icons)
        {
            using var image = Decode(source.ReadIconTexture(new(icon.SpeciesId)), 32, 32);
            var rgba = image.GetData();
            for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                for (int c = 0; c < 3; c++) { int value = rgba[i + c] >> 3; rgba[i + c] = (byte)((value << 3) | (value >> 2)); }
            using var display = Image.CreateFromData(32, 32, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), ImageTexture.CreateFromImage(display));
        }
        var assets=RockTunnelPresentationAssets.Load(_root);
        using(var image=NativeImages.LoadPng(assets["pokemaniac-front.png"], 64, 64))_forestTrainerPortraits.Add("pokemaniac-front",ImageTexture.CreateFromImage(image));
        using(var image=NativeImages.LoadPng(assets["indoor-streaks.png"], 256, 80))_fieldFlashStreaks=ImageTexture.CreateFromImage(image);
        foreach(var icon in PretRockTunnelMachines.LoadProject(_root).Icons.Concat(PretRockTunnelItems.LoadProject(_root).Icons))
        {
            
            if(!(_earlyItemIcons.TryGetValue(icon.Item,out var existing)))_earlyItemIcons.Add(icon.Item,ItemTexture("data/items/rocktunnel", icon));
        }
    }
    private Texture2D? _fieldFlashStreaks;
    private string? _fieldFlashWait,_fieldFlashPhase;
    private double _fieldFlashFrames;
    private bool _fieldFlashCry;
    private int _fieldFlashMaskRadius=-1;
    private readonly int[] _fieldFlashLeft=new int[160],_fieldFlashRight=new int[160];
    private int FieldFlashFrame=>(int)Math.Floor(_fieldFlashFrames+1e-7);
    private int FieldFlashPlayerFrame=>FieldFlashFrame<16?FieldFlashFrame/4:4;
    private int FieldFlashRadius=>_session.State.FlashEffect is {Phase:FieldFlashSourcePresentation.Illuminate} flash?FieldFlashSourcePresentation.RadiusAtFrame(flash.InitialLevel,FieldFlashFrame):FieldFlashSourcePresentation.Radius(_session.State.FlashLevel);
    private void SynchronizeFieldFlashPresentation(CampaignState after)
    {
        var flash=after.FlashEffect;
        if(flash?.WaitId!=_fieldFlashWait||flash?.Phase!=_fieldFlashPhase){_fieldFlashWait=flash?.WaitId;_fieldFlashPhase=flash?.Phase;_fieldFlashFrames=0;_fieldFlashCry=false;}
        if(flash is not null){_held=null;_repeat=0;}
    }
    private bool ProcessFieldFlashPresentation(double delta)
    {
        if(_session.State.FlashEffect is not {} flash)return false;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        int duration=flash.Phase==FieldFlashSourcePresentation.ShowMon?FieldFlashSourcePresentation.ShowMonCueFrames:FieldFlashSourcePresentation.IlluminateCueFrames(flash.InitialLevel);
        _fieldFlashFrames=Math.Min(duration,_fieldFlashFrames+delta*60);
        if(flash.Phase==FieldFlashSourcePresentation.ShowMon&&!_fieldFlashCry&&FieldFlashFrame>=FieldFlashSourcePresentation.PlayerFrameDurations.Sum()+FieldFlashSourcePresentation.IndoorBannerOpenFrames+FieldFlashSourcePresentation.MonSlideInFrames)
        {_fieldFlashCry=true;SourceCry(_session.State.Party[flash.PartyIndex].Pokemon);}
        if(!_busy&&FieldFlashFrame>=duration)Apply(new("field_flash_complete",Text:flash.WaitId));
        _held=null;_repeat=0;return true;
    }
    private void UseFieldPartyFlash()
    {
        bool accepted = Apply(new("field_flash",PartyIndex:_fieldPartyCursor));
        if(accepted){ResetFieldMenu();return;}
        _fieldMenuMode="party";_fieldMenuNotice=_status=="field_flash_badge_required"?"This can't be used until a new\nBADGE is obtained.":"Can't use that here.";
    }
    // Original SetFlashScanlineEffectWindowBoundaries integer raster and right-exclusive GBA window.
    private void DrawFieldFlashMask()
    {
        if(!_catalog.RockTunnelEnabled||_session.State.FlashLevel==0)return;
        int radius=FieldFlashRadius;
        if(radius!=_fieldFlashMaskRadius)
        {
            _fieldFlashMaskRadius=radius;Array.Clear(_fieldFlashLeft);Array.Clear(_fieldFlashRight);
            void Set(int y,int left,int right){if(y is >=0 and <160){_fieldFlashLeft[y]=Math.Clamp(left,0,255);_fieldFlashRight[y]=Math.Clamp(right,0,255);}}
            int xy=radius,error=radius,yx=0;
            while(xy>=yx){Set(80-yx,120-xy,120+xy);Set(80+yx,120-xy,120+xy);Set(80-xy,120-yx,120+yx);Set(80+xy,120-yx,120+yx);error-=(yx*2)-1;yx++;if(error<0){error+=2*(xy-1);xy--;}}
        }
        for(int y=0;y<160;y++){int left=Math.Min(240,_fieldFlashLeft[y]),right=Math.Min(240,_fieldFlashRight[y]);if(left>0)DrawRect(new(0,y,left,1),Colors.Black);if(right<240)DrawRect(new(right,y,240-right,1),Colors.Black);}
    }
    private void DrawFieldFlashBanner()
    {
        if(_session.State.FlashEffect is not {Phase:FieldFlashSourcePresentation.ShowMon} flash)return;
        DrawFieldIndoorShowMonBanner(FieldFlashFrame,flash.PartyIndex);
    }
    private void DrawFieldIndoorShowMonBanner(int frame,int partyIndex)
    {
        int age=frame-FieldFlashSourcePresentation.PlayerFrameDurations.Sum();if(age<0)return;
        int open=FieldFlashSourcePresentation.IndoorBannerOpenFrames,middle=FieldFlashSourcePresentation.MonSlideInFrames+FieldFlashSourcePresentation.MonHoldFrames+FieldFlashSourcePresentation.MonSlideOutFrames,closing=age-open-middle;
        Span<bool> visible=stackalloc bool[32];
        for(int t=0;t<Math.Min(16,age+1);t++){int column=(32-t*2)&31;visible[column]=true;visible[(column+1)&31]=true;}
        for(int t=0;t<Math.Min(16,closing+1);t++){int column=(-(open+middle+t+1)*2)&31;visible[column]=false;visible[(column+1)&31]=false;}
        int scroll=-(age+1)*16;
        for(int x=0;x<240;){int sourceX=((x+scroll)%256+256)%256,span=Math.Min(240-x,8-sourceX%8);if(visible[sourceX/8])DrawTextureRectRegion(_fieldFlashStreaks!,new(x,40,span,80),new(sourceX,0,span,80));x+=span;}
        int slide=age-open;if(slide<0||slide>=middle)return;
        int exit=slide-FieldFlashSourcePresentation.MonSlideInFrames-FieldFlashSourcePresentation.MonHoldFrames;
        int xMon=slide<FieldFlashSourcePresentation.MonSlideInFrames?Math.Max(120,320-slide*20):exit<0?120:120-exit*20;
        var mon=_session.State.Party[partyIndex].Pokemon;DrawTexture(_battleArt[(PresentedRenderSpecies(mon),"front",mon.IsShiny)].Texture,new(xMon-32,48));
    }
    private ForestMessage? RockTunnelWildMessage(WildBattleEvent current)=>!_catalog.RockTunnelEnabled?null:current switch
    {
        WildBattleProtectAttemptEvent=>new(""),
        WildBattleMtMoonVolatileEvent {Volatile:"protect",Reason:"move"} p=>new("sText_PkmnProtectedItself2",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(p.Side))),
        WildBattleMtMoonVolatileEvent {Volatile:"endure",Reason:"move"} p=>new("sText_PkmnBracedItself",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(p.Side))),
        WildBattleMtMoonVolatileEvent {Volatile:"endure",Reason:"endured"} e=>new("sText_PkmnEnduredHit",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(e.Side))),
        WildBattleMoveFailedEvent {Reason:"protected"} p=>new("sText_PkmnProtectedItself",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(p.Target))),
        WildBattleMoveFailedEvent {Reason:"protect-failed" or "already-reflecting" or "already-sandstorm"}=>new("sText_ButItFailed"),
        WildBattleMtMoonVolatileEvent {Volatile:"reflect",Reason:"move"} r=>new("sText_PkmnRaisedDef",("B_ATK_PREFIX2",r.Side==WildBattleSide.Player?"Ally":"Foe"),("B_CURRENT_MOVE","REFLECT")),
        WildBattleMtMoonVolatileEvent {Volatile:"reflect",Reason:"expired"} r=>new("sText_PkmnsXWoreOff",("B_ATK_PREFIX1",r.Side==WildBattleSide.Player?"Ally":"Foe"),("B_BUFF1","REFLECT")),
        WildBattleMtMoonVolatileEvent {Volatile:"sandstorm",Reason:"move"}=>new("sText_SandstormBrewed"),
        WildBattleMtMoonVolatileEvent {Volatile:"sandstorm",Reason:"countdown"}=>new("sText_SandstormRages"),
        WildBattleMtMoonVolatileEvent {Volatile:"sandstorm",Reason:"expired"}=>new("sText_SandstormSubsided"),
        WildBattleWeatherDamageEvent {Weather:"sandstorm"} w=>new("sText_PkmnBuffetedBySandstorm",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(w.Side))),
        WildBattleMtMoonVolatileEvent {Volatile:"identified"} i=>new("sText_PkmnIdentified",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(i.Side==WildBattleSide.Player?WildBattleSide.Opponent:WildBattleSide.Player)),("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(i.Side))),
        WildBattleHeldItemSurvivalEvent i=>new("sText_PkmnHungOnWithX",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(i.Target)),("B_LAST_ITEM",RockTunnelHeldItemName(i.Item))),
        WildBattleHeldItemKnockedOffEvent i=>new("sText_PkmnKnockedOff",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(i.Actor)),("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(i.Target)),("B_LAST_ITEM",RockTunnelHeldItemName(i.Item))),
        WildBattleHeldItemStolenEvent i=>new("sText_PkmnStoleItem",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(i.Actor)),("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(i.Target)),("B_LAST_ITEM",RockTunnelHeldItemName(i.Item))),
        _=>null
    };
    private bool RockTunnelSilentEvent(CampaignWildBattle b)=>WildCurrentEvent(b) is {} e&&RockTunnelWildMessage(e) is {Symbol.Length:0};
    private string RockTunnelHeldItemName(string symbol)=>symbol=="ITEM_FOCUS_BAND"?"FOCUS BAND":symbol=="ITEM_HARD_STONE"?"HARD STONE":_catalog.EarlyShop?.Items.FirstOrDefault(i=>i.SourceItemSymbol==symbol)?.Name??VermilionHeldItemName(symbol)??MtMoonHeldItemName(symbol);
}
