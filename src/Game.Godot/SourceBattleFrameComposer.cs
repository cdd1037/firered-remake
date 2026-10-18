using System.Collections.ObjectModel;
namespace Game.Presentation.BattleAnimations;
public sealed record SourcePixelAsset(int Width,int Height,ushort[] Pixels)
{ public byte[] PaletteIndices{get;init;}=[]; public ushort[] Palette{get;init;}=[]; public int PaletteTag{get;init;}=-1; }

/// <summary>Bounded 240x112 source RGB555 field. Existing HUD/text remain later layers.</summary>
public sealed class SourceBattleFrameComposer
{
    private static ushort[]? SelectedSpritePalette(SourceAnimationFrame frame,SourceVisualSpriteFrame sprite,SourcePixelAsset asset)
    {
        if(sprite.MaterialPalette is {} retained)return retained;
        if(sprite.PaletteSlot is not int slot)return frame.SpritePalettes.GetValueOrDefault(asset.PaletteTag);
        if(slot is <0 or >15||frame.SpritePaletteSlots.Length!=16||frame.SpritePaletteSlots[slot].Length!=16)throw new InvalidDataException("Source explicit sprite palette bank is missing.");
        return frame.SpritePaletteSlots[slot];
    }

    public const int Width=240,Height=160;
    private readonly Dictionary<string,SourcePixelAsset> _assets=[];
    private readonly AnimationBattler[] _battlers;
    private readonly SourceStatMaskMaterial? _statMaterial;
    private readonly SourceMetallicMaterial? _metallicMaterial;
    private readonly SourceAnimationBackgroundSurface? _backgroundSurface;
    private readonly IReadOnlyDictionary<int,SourceBackgroundAsset> _moveBackgrounds;
    private readonly IReadOnlyDictionary<string,SourceOverlayAsset> _overlays;
    private int _backgroundRevision;
    private readonly ushort[] _objectLayer=new ushort[Width*112],_objectPixels=new ushort[Width*112];
    private readonly bool[] _objectTranslucency=new bool[Width*112];
    private readonly byte[] _objectPriorities=new byte[Width*112],_baseLayerMasks=new byte[Width*112];
    private readonly ushort[][] _battlerBackgroundLayers=[new ushort[Width*112],new ushort[Width*112]];
    public SourceBattleFrameComposer(IReadOnlyDictionary<string,SourcePixelAsset> assets,AnimationBattler[] battlers,SourceStatMaskMaterial? statMaterial=null,SourceMetallicMaterial? metallicMaterial=null,SourceAnimationBackgroundSurface? backgroundSurface=null,IReadOnlyDictionary<int,SourceBackgroundAsset>? moveBackgrounds=null,IReadOnlyDictionary<string,SourceOverlayAsset>? overlays=null)
    {
        foreach(var (id,asset) in assets)
        {
            if(asset.Width is <1 or >240||asset.Height is <1 or >512||asset.Pixels.Length!=asset.Width*asset.Height)throw new InvalidDataException("Campaign animation asset shape differs.");
            _assets.Add(id,asset);
        }
        _battlers=battlers;_statMaterial=statMaterial;_metallicMaterial=metallicMaterial;_backgroundSurface=backgroundSurface;_moveBackgrounds=moveBackgrounds??new Dictionary<int,SourceBackgroundAsset>();_overlays=overlays??new Dictionary<string,SourceOverlayAsset>();
        if(_assets["background"].Width!=240||_assets["background"].Height!=112||_assets["impact"].Width!=32||_assets["impact"].Height!=32)throw new InvalidDataException("Source field/impact shape differs.");
        foreach(var b in battlers)if(_assets[b.Asset].Width!=64||_assets[b.Asset].Height!=64)throw new InvalidDataException("Battler canvas differs.");
    }
    // GBA BG mosaic is aligned to screen origin, before BG scroll/source sampling.
    // The rendered source layer already includes scroll and transparent margins.
    private static void MosaicBackground(ushort[] pixels,int width,int height)
    {
        for(int y=0;y<112;y++)for(int x=0;x<Width;x++)
            pixels[y*Width+x]=pixels[(y-y%height)*Width+x-x%width];
    }
    public SourcePixelAsset Asset(string id)=>_assets[id];
    private SourcePixelAsset SpriteMaterial(SourceVisualSpriteFrame sprite)=>sprite.Material??_assets[sprite.Asset!];
    public byte[] ComposeField(SourceAnimationFrame frame)
    {
        var battlers=frame.Battlers.Length==0?_battlers:frame.Battlers;
        if(battlers.Length!=_battlers.Length||battlers.Select(b=>b.Slot.Value).Distinct().Count()!=battlers.Length||
            battlers.Any(b=>!_battlers.Any(original=>original.Slot==b.Slot)||!_assets.TryGetValue(b.Asset,out var asset)||asset.Width!=64||asset.Height!=64))
            throw new InvalidDataException("Source selected battler material differs.");
        if(frame.BattlerBackgroundMosaic.Length!=2||frame.BackgroundMosaicHorizontal is <0 or >15||frame.BackgroundMosaicVertical is <0 or >15)
            throw new InvalidDataException("Source BG mosaic registers differ.");
        frame.Scanline?.Validate();
        if(frame.BackgroundScrollOffsets.Length is not (0 or 2))throw new InvalidDataException("Source battler BG register shape differs.");
        frame.Window0?.Validate();frame.ObjectWindow?.Validate();frame.Brightness?.Validate();
        if(frame.ObjectWindow is {Enabled:true}&&(frame.Window0 is {Enabled:true}||frame.StatMask is {WindowEnabled:true}||frame.Metallic is {WindowEnabled:true}))
            throw new InvalidDataException("Concurrent source object-window owners are outside the admitted closure.");
        if(frame.Brightness is not null&&_backgroundSurface is null)throw new InvalidDataException("Source brightness needs indexed BG3/backdrop provenance.");
        if(frame.Brightness is not null&&(frame.BlendEnabled||frame.MoveBackground is {Darkening:>0})||
            frame.Window0 is {Enabled:true}&&(frame.StatMask is {WindowEnabled:true}||frame.Metallic is {WindowEnabled:true}))
            throw new InvalidDataException("Concurrent source window/color-effect owners are outside the admitted closure.");
        var pixels=new ushort[Width*112];Array.Fill(_baseLayerMasks,(byte)8);
        var palettes=frame.PaletteBlends.ToDictionary(p=>p.Palette);
        if(_backgroundSurface is not null)
        {
            var background=frame.MoveBackground??new(null,0,0,0);
            if(background.Revision!=_backgroundRevision)
            {
                if(background.Revision==0)_backgroundSurface.Reset();
                else if(background.Id is {} id)_backgroundSurface.Apply(_moveBackgrounds[id]);
                else _backgroundSurface.Restore();
                _backgroundRevision=background.Revision;
            }
            _backgroundSurface.Render(pixels,240,112,background.ScrollX,background.ScrollY,background.ScreenSize,background.Darkening,frame.PaletteBlends,frame.InvertedPalettes,background.FadedPalette,_baseLayerMasks,frame.BackgroundPaletteEntries);
        }
        else
        {
            if(frame.BackgroundPaletteEntries.Length>0||frame.MoveBackground is {Id:not null} or {Darkening:>0} or {ScrollX:not 0} or {ScrollY:not 0})throw new InvalidDataException("Move background requires its indexed source surface.");
        // The flattened terrain contains source BG palette banks 1..3 only. Every
        // admitted selector transforms all three identically. Fail closed if a later
        // script requests distinct banks; it then needs per-pixel bank provenance.
        var backgroundBlend=palettes.GetValueOrDefault(1);
        if(new[]{2,3}.Any(bank=>palettes.GetValueOrDefault(bank) is {} other
            ?backgroundBlend is null||other.Coefficient!=backgroundBlend.Coefficient||other.Color!=backgroundBlend.Color||other.Unfaded!=backgroundBlend.Unfaded
            :backgroundBlend is not null))throw new InvalidDataException("Terrain palette banks require source membership data.");
        Blit(pixels,_assets["background"],0,0,palettes.GetValueOrDefault(1),inverted:frame.InvertedPalettes.Contains(1));
        }
        bool SourcePresent(AnimationBattler b)=>frame.BattlerVisibility.FirstOrDefault(v=>v.Slot==b.Slot.Value)?.Present??(b.SourceVisibility?.Present??b.Visible);
        bool SourceOamVisible(AnimationBattler b)=>frame.BattlerVisibility.FirstOrDefault(v=>v.Slot==b.Slot.Value) is {} state?state.Present&&!state.OamInvisible:b.Visible&&(!frame.BackgroundSlots.Contains(b.Slot.Value)||frame.RestoredObjectSlots.Contains(b.Slot.Value));
        // BG copies retain their original 64x64 tiles. PrepareBattlerSpriteForRotScale
        // can expose a distinct affine OBJ while this copy continues scrolling.
        if(frame.BattlerBackgroundPriorities.Length!=2||frame.BattlerBackgroundPriorities.Any(p=>p is <0 or >3))throw new InvalidDataException("Battler background priorities exceed source registers.");
        foreach(var backgroundLayer in _battlerBackgroundLayers)Array.Clear(backgroundLayer);
        var layer=_objectLayer;
        foreach(var b in battlers.Where(b=>SourcePresent(b)&&frame.BackgroundSlots.Contains(b.Slot.Value)).OrderByDescending(b=>b.BackgroundRank))
        {
            Array.Clear(layer);var center=b.DisplayCenter??b.Center(frame.Doubles);var offset=frame.Offsets[b.Slot.Value];
            var copy=frame.StaticBackgroundCopies.SingleOrDefault(c=>c.Slot==b.Slot.Value);
            if(copy is not null)offset=copy.Offset;
            var palette=copy is null?palettes.GetValueOrDefault(16+b.Slot.Value):copy.Palette;
            bool grey=copy?.Greyscale??frame.GreyscaleSlots.Contains(b.Slot.Value),inverted=copy?.Inverted??frame.InvertedPalettes.Contains(16+b.Slot.Value);
            if(frame.BackgroundScrollOffsets.Length==2||ScanlineAffectsBackground(frame.Scanline,b.BackgroundRank))
            {
                if(frame.BattlerBackgroundMosaic[b.BackgroundRank-1]&&ScanlineAffectsBackground(frame.Scanline,b.BackgroundRank))
                    throw new InvalidDataException("Concurrent BG mosaic and scanline sampling is outside the admitted closure.");
                var scroll=frame.BackgroundScrollOffsets.Length==2?frame.BackgroundScrollOffsets[b.BackgroundRank-1]:new PixelPoint(32-center.X-offset.X,32-center.Y-offset.Y);
                BlitBattlerBackground(layer,_assets[b.Asset],scroll,b.BackgroundRank,frame.Scanline,palette,grey,inverted);
            }
            else Blit(layer,_assets[b.Asset],center.X+offset.X-32,center.Y+offset.Y-32,palette,grey,inverted);
            if(frame.BattlerBackgroundMosaic[b.BackgroundRank-1])
                MosaicBackground(layer,frame.BackgroundMosaicHorizontal+1,frame.BackgroundMosaicVertical+1);
            var backgroundLayer=_battlerBackgroundLayers[b.BackgroundRank-1];
            for(int i=0;i<layer.Length;i++)if((layer[i]&0x8000)!=0)backgroundLayer[i]=layer[i];
        }
        if(frame.Overlay is {} overlay)
        {
            if(!_overlays.TryGetValue(overlay.MaterialId,out var material))throw new InvalidDataException("Source BG1 overlay material missing.");
            material.Render(_battlerBackgroundLayers[0],Width,112,overlay.ScrollX,overlay.ScrollY,_backgroundSurface,frame.PaletteBlends,frame.InvertedPalettes,frame.BackgroundPaletteEntries,frame.Scanline);
            if(frame.BattlerBackgroundMosaic[0])MosaicBackground(_battlerBackgroundLayers[0],frame.BackgroundMosaicHorizontal+1,frame.BackgroundMosaicVertical+1);
        }
        if(frame.BattlerBackgroundEnabled.Length!=2)throw new InvalidDataException("Battler BG enable register shape differs.");
        for(int plane=0;plane<2;plane++)if(!frame.BattlerBackgroundEnabled[plane])Array.Clear(_battlerBackgroundLayers[plane]);
        // OBJ-window pixels form a union independent of ordinary OBJ priority.
        // They never contribute color or occlude the frontmost visible object.
        if(frame.ObjectWindow is {Enabled:true} objectWindow)
        {
            var stencil=new bool[Width*112];
            foreach(var sprite in frame.Sprites.Where(s=>s.Visible&&s.ObjectWindow&&s.Asset is not null))
            {
                if(sprite.Subsprites.Length!=0)throw new InvalidDataException("Object-window subsprite layouts are outside the admitted closure.");
                Array.Clear(layer);var material=SpriteMaterial(sprite);
                BlitSprite(layer,material,sprite,0,0,false);
                for(int i=0;i<layer.Length;i++)if((layer[i]&0x8000)!=0)stencil[i]=true;
            }
            var bg1=_battlerBackgroundLayers[0];
            for(int i=0;i<bg1.Length;i++)if(((stencil[i]?objectWindow.InsideMask:objectWindow.OutsideMask)&2)==0)bg1[i]=0;
        }
        // The PPU selects one frontmost OBJ pixel before alpha blending. Lower OBJ
        // pixels never become another OBJ's second target, including battler copies.
        var objects=_objectPixels;Array.Clear(objects);var translucent=_objectTranslucency;var objectPriority=_objectPriorities;
        if(frame.BattlerObjectPriorities.Length!=4||frame.BattlerObjectPriorities.Any(p=>p is <0 or >3))throw new InvalidDataException("Battler object priority exceeds source hardware field.");
        if(frame.Sprites.Any(s=>s.OamPriority is <0 or >3))throw new InvalidDataException("Object priority exceeds source hardware field.");
        var ordered=battlers.Where(b=>SourceOamVisible(b))
            .Select(b=>(Priority:frame.BattlerObjectPriorities[b.Slot.Value],Rank:frame.BattlerOamRanks.Length>b.Slot.Value?frame.BattlerOamRanks[b.Slot.Value]:b.Slot.Value,Fragment:0,Battler:(AnimationBattler?)b,Sprite:(SourceVisualSpriteFrame?)null))
            .Concat(frame.Sprites.Where(s=>s.Visible&&!s.ObjectWindow&&s.Asset is not null).SelectMany(ExpandSubsprites).Select(p=>(Priority:p.Frame.OamPriority,Rank:p.Frame.OamRank,Fragment:p.Index,Battler:(AnimationBattler?)null,Sprite:(SourceVisualSpriteFrame?)p.Frame)));
        foreach(var item in ordered.OrderByDescending(x=>x.Priority).ThenByDescending(x=>x.Rank).ThenByDescending(x=>x.Fragment))
        {
            Array.Clear(layer);bool blend;
            if(item.Battler is {} b)
            {
                int slot=b.Slot.Value;var center=b.DisplayCenter??b.Center(frame.Doubles);var offset=frame.ObjectOffsets.Length>slot?frame.ObjectOffsets[slot]:frame.Offsets[slot];
                var matrix=frame.BattlerAffine.Length>slot?frame.BattlerAffine[slot]:new(false,256,256);
                AffineBattler(layer,_assets[b.Asset],center.X+offset.X,center.Y+offset.Y,matrix,palettes.GetValueOrDefault(16+slot),frame.InvertedPalettes.Contains(16+slot),frame.GreyscaleSlots.Contains(slot));
                blend=frame.BlendedObjectSlots.Contains(slot);
            }
            else
            {
                var sprite=item.Sprite!;blend=sprite.Blend;var center=new PixelPoint(sprite.Position.X+sprite.Offset.X,sprite.Position.Y+sprite.Offset.Y);
                if(sprite.BattlerCopySlot>=0)
                {
                    int slot=sprite.BattlerCopySlot;var matrix=new SourceBattlerAffine(false,sprite.Scale,sprite.Scale);
                    AffineBattler(layer,SpriteMaterial(sprite),center.X,center.Y,matrix,sprite.BattlerCopyPalette??palettes.GetValueOrDefault(16+slot),sprite.BattlerCopyPalette is null&&frame.InvertedPalettes.Contains(16+slot),sprite.BattlerCopyPalette is null&&frame.GreyscaleSlots.Contains(slot));
                }
                else if(sprite.Subsprites.Length==0&&(sprite.Template is "gBasicHitSplatSpriteTemplate" or "gFlashingHitSplatSpriteTemplate"))AffineImpact(layer,SpriteMaterial(sprite),new(sprite.Position,sprite.Scale,sprite.Asset!),0,0,false);
                else BlitSprite(layer,SpriteMaterial(sprite),sprite,0,0,false,SelectedSpritePalette(frame,sprite,SpriteMaterial(sprite)));
            }
            for(int i=0;i<layer.Length;i++)if((layer[i]&0x8000)!=0){objects[i]=layer[i];translucent[i]=blend;objectPriority[i]=(byte)item.Priority;}
        }
        if((frame.BlendFirstTargets&~63)!=0||(frame.BlendSecondTargets&~63)!=0)throw new InvalidDataException("Source alpha targets exceed hardware masks.");
        if(frame.Window0 is null&&frame.Brightness is null&&frame.Overlay is null&&frame.BlendFirstTargets==0&&frame.BlendSecondTargets==63)
        {
            // The common source setalpha mode only blends semi-transparent OBJ.
            // Keep its bounded fast path while sharing the same retained BG planes.
            int p1=frame.BattlerBackgroundPriorities[0],p2=frame.BattlerBackgroundPriorities[1];
            for(int i=0;i<pixels.Length;i++)
            {
                int priority=3;
                if((_battlerBackgroundLayers[1][i]&0x8000)!=0){pixels[i]=_battlerBackgroundLayers[1][i];priority=p2;}
                if((_battlerBackgroundLayers[0][i]&0x8000)!=0&&p1<=priority){pixels[i]=_battlerBackgroundLayers[0][i];priority=p1;}
                if((objects[i]&0x8000)!=0&&objectPriority[i]<=priority)
                    pixels[i]=translucent[i]&&frame.BlendEnabled?Blend(objects[i],pixels[i],ScanlineAlpha(frame,i/Width,false),ScanlineAlpha(frame,i/Width,true)):objects[i];
            }
        }
        else for(int i=0;i<pixels.Length;i++)
        {
            // Select the two frontmost visible layers. An unselected second layer
            // blocks blending rather than allowing a search through it. OBJ wins
            // equal BG priority; lower numbered BG wins a BG/BG tie.
            ushort first=pixels[i],second=0;int firstKey=3*4+3,secondKey=int.MaxValue,firstMask=_baseLayerMasks[i],secondMask=0;
            void Insert(ushort color,int priority,int rank,int mask)
            {
                if((color&0x8000)==0)return;
                int key=priority*4+rank;
                if(key<firstKey){second=first;secondKey=firstKey;secondMask=firstMask;first=color;firstKey=key;firstMask=mask;}
                else if(key<secondKey){second=color;secondKey=key;secondMask=mask;}
            }
            Insert(_battlerBackgroundLayers[0][i],frame.BattlerBackgroundPriorities[0],1,2);
            Insert(_battlerBackgroundLayers[1][i],frame.BattlerBackgroundPriorities[1],2,4);
            Insert(objects[i],objectPriority[i],0,16);
            bool selected=(firstMask&frame.BlendFirstTargets)!=0||(firstMask==16&&translucent[i]);
            bool colorEnabled=frame.Window0?.ColorEffectsAt(i%Width,i/Width)??true;
            pixels[i]=colorEnabled&&frame.BlendEnabled&&selected&&(secondMask&frame.BlendSecondTargets)!=0
                ?Blend(first,second,ScanlineAlpha(frame,i/Width,false),ScanlineAlpha(frame,i/Width,true))
                :colorEnabled&&frame.Brightness is {} brightness&&(firstMask&frame.BlendFirstTargets)!=0
                    ?brightness.Apply(first):first;
        }
        if(frame.StatMask is {WindowEnabled:true,BackgroundLoaded:true,MaskVisible:true} mask)
        {
            if(_statMaterial is null)throw new InvalidDataException("Source stat material missing.");
            var selected=battlers.Single(b=>b.Slot.Value==mask.MaskSlot);var center=selected.DisplayCenter??selected.Center(frame.Doubles);
            var silhouette=_assets[selected.Asset];
            bool Opaque(AnimationBattler b,int x,int y)
            {var p=b.DisplayCenter??b.Center(frame.Doubles);int sx=x-p.X+32,sy=y-p.Y+32;return b.Visible&&sx>=0&&sx<64&&sy>=0&&sy<64&&(_assets[b.Asset].Pixels[sy*64+sx]&0x8000)!=0;}
            var priority=mask.PriorityPartner<0?null:battlers.Single(b=>b.Slot.Value==mask.PriorityPartner);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                int dx=center.X-32+x,dy=center.Y-32+y;
                if(dx<0||dx>=240||dy<0||dy>=112||(silhouette.Pixels[y*64+x]&0x8000)==0||priority is not null&&Opaque(priority,dx,dy))continue;
                ushort pixel=_statMaterial.Pixel(mask.ScrollX==64?1:0,mask.Palette,dx+mask.ScrollX,dy+mask.ScrollY);
                if((pixel&0x8000)!=0)pixels[dy*240+dx]=Blend(pixel,pixels[dy*240+dx],frame.AlphaA,frame.AlphaB);
            }
        }
        if(frame.Metallic is {WindowEnabled:true,BackgroundLoaded:true,MaskVisible:true} metal)
        {
            if(_metallicMaterial is null)throw new InvalidDataException("Metallic material missing.");
            var selected=battlers.Single(b=>b.Slot.Value==metal.MaskSlot);var center=selected.DisplayCenter??selected.Center(frame.Doubles);var silhouette=_assets[selected.Asset];
            var partner=metal.PriorityPartner<0?null:battlers.Single(b=>b.Slot.Value==metal.PriorityPartner);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                int dx=center.X-32+x,dy=center.Y-32+y;
                if(dx<0||dx>=240||dy<0||dy>=112||(silhouette.Pixels[y*64+x]&0x8000)==0)continue;
                if(partner is not null){var p=partner.DisplayCenter??partner.Center(frame.Doubles);int sx=dx-p.X+32,sy=dy-p.Y+32;if(sx>=0&&sx<64&&sy>=0&&sy<64&&(_assets[partner.Asset].Pixels[sy*64+sx]&0x8000)!=0)continue;}
                ushort pixel=_metallicMaterial.Pixel(dx+metal.ScrollX,dy+metal.ScrollY);
                if((pixel&0x8000)!=0)pixels[dy*240+dx]=Blend(pixel,pixels[dy*240+dx],frame.AlphaA,frame.AlphaB);
            }
        }
        var rgba=new byte[pixels.Length*4];
        for(int i=0;i<pixels.Length;i++){int p=pixels[i];rgba[i*4]=(byte)((p&31)*255/31);rgba[i*4+1]=(byte)(((p>>5)&31)*255/31);rgba[i*4+2]=(byte)(((p>>10)&31)*255/31);rgba[i*4+3]=255;}return rgba;
    }
    private static IEnumerable<(SourceVisualSpriteFrame Frame,int Index)> ExpandSubsprites(SourceVisualSpriteFrame frame)
    {
        if(frame.Subsprites.Length==0){yield return(frame,0);yield break;}
        if(frame.Affine||frame.DoubleSize||frame.BattlerCopySlot>=0||frame.Subsprites.Length>128)
            throw new InvalidDataException("Source subsprite mode is outside the non-affine OAM closure.");
        // AddSubspritesToOamBuffer uses the parent's center, then each table's
        // top-left offset. Table order is OAM order, not a new logical sprite.
        for(int i=0;i<frame.Subsprites.Length;i++)
        {
            var part=frame.Subsprites[i];
            if(part.X is <sbyte.MinValue or >sbyte.MaxValue||part.Y is <sbyte.MinValue or >sbyte.MaxValue||
                !ValidObjectShape(part.Width,part.Height)||part.TileOffset is <0 or >1023||part.Priority is <0 or >3)
                throw new InvalidDataException("Source subsprite table exceeds OAM fields.");
            int x=(frame.Position.X+frame.Offset.X+(frame.HFlip?-(part.X+part.Width):part.X))&511;
            int y=(frame.Position.Y+frame.Offset.Y+(frame.VFlip?-(part.Y+part.Height):part.Y))&255;
            if(x>255)x-=512;if(y>=160)y-=256;
            yield return(frame with{Position=new(x+part.Width/2,y+part.Height/2),Offset=default,
                Width=part.Width,Height=part.Height,Tile=(frame.Tile+part.TileOffset)&1023,
                OamPriority=part.Priority},i);
        }
    }
    private static bool ValidObjectShape(int width,int height)=>(width,height) is
        (8,8) or (16,16) or (32,32) or (64,64) or
        (16,8) or (32,8) or (32,16) or (64,32) or
        (8,16) or (8,32) or (16,32) or (32,64);
    private static bool ScanlineAffectsBackground(SourceScanlineFrame? scanline,int rank)=>scanline is not null&&scanline.Destination==(rank==1?SourceScanlineDestination.Bg1Horizontal:SourceScanlineDestination.Bg2Horizontal);
    private static int ScanlineAlpha(SourceAnimationFrame frame,int row,bool second)
    {
        if(frame.Scanline is not {Destination:SourceScanlineDestination.BlendAlpha} scanline)return second?frame.AlphaB:frame.AlphaA;
        int value=scanline.Words[row];return second?(value>>8)&31:value&31;
    }
    private static void BlitBattlerBackground(ushort[] destination,SourcePixelAsset source,PixelPoint baseline,int rank,
        SourceScanlineFrame? scanline,SourcePaletteBlend? palette,bool grey,bool inverted)
    {
        bool selected=ScanlineAffectsBackground(scanline,rank);
        for(int y=0;y<112;y++)
        {
            int scrollX=selected?scanline!.Words[y*(scanline.Word32?2:1)]:baseline.X;
            int scrollY=selected&&scanline!.Word32?scanline.Words[y*2+1]:baseline.Y;
            int sy=(y+scrollY)&255;if(sy>=source.Height)continue;
            for(int x=0;x<Width;x++)
            {
                int sx=(x+scrollX)&511;if(sx>=source.Width)continue;
                ushort color=source.Pixels[sy*source.Width+sx];if((color&0x8000)==0)continue;
                destination[y*Width+x]=SourcePaletteMath.Apply(color,palette,inverted,grey);
            }
        }
    }
    private static void Blit(ushort[] dest,SourcePixelAsset source,int x,int y,SourcePaletteBlend? palette=null,bool greyscale=false,bool inverted=false)
    {
        for(int sy=0;sy<source.Height;sy++)for(int sx=0;sx<source.Width;sx++)
        {int dx=x+sx,dy=y+sy;ushort p=source.Pixels[sy*source.Width+sx];if(dx>=0&&dx<240&&dy>=0&&dy<112&&(p&0x8000)!=0){dest[dy*240+dx]=SourcePaletteMath.Apply(p,palette,inverted,greyscale);}}
    }
    private static void AffineBattler(ushort[] dest,SourcePixelAsset source,int centerX,int centerY,SourceBattlerAffine affine,SourcePaletteBlend? palette,bool inverted=false,bool greyscale=false)
    {
        // SetSpriteRotScale supplies the inverse sampling matrix directly to the
        // GBA BIOS. Signed products use arithmetic shifts, including the off-diagonal terms.
        int half=affine.DoubleSize?64:32;
        for(int y=-half;y<half;y++)for(int x=-half;x<half;x++)
        {
            int sx=((x*affine.XScale+y*affine.MatrixB)>>8)+32,sy=((x*affine.MatrixC+y*affine.YScale)>>8)+32,dx=centerX+x,dy=centerY+y;
            if(sx<0||sx>=64||sy<0||sy>=64||dx<0||dx>=240||dy<0||dy>=112)continue;
            ushort p=source.Pixels[sy*64+sx];if((p&0x8000)==0)continue;
            dest[dy*240+dx]=SourcePaletteMath.Apply(p,palette,inverted,greyscale);
        }
    }
    private static void BlitSprite(ushort[] dest,SourcePixelAsset sheet,SourceVisualSpriteFrame sprite,int eva,int evb,bool blendEnabled,ushort[]? palette=null)
    {
        // OBJ 1D addressing uses the sprite's width to advance tile rows. The
        // generated sheet is only storage: shared graphics can mix 8x8 and 16x16
        // objects, and a frame may start halfway through a stored tile row.
        int width=sprite.Width,height=sprite.Height;
        if(width is not (8 or 16 or 32 or 64)||height is not (8 or 16 or 32 or 64)||sheet.Width<8||sheet.Height<8||sheet.Width%8!=0||sheet.Height%8!=0||sprite.Tile<0)throw new InvalidDataException("Source sprite frame shape differs.");
        int frameTilesWide=width/8,sheetTilesWide=sheet.Width/8;
        if(sprite.Tile>sheet.Pixels.Length/64-width*height/64)throw new InvalidDataException("Source sprite tile frame exceeds sheet.");
        bool aligned=sheet.Width==width&&sprite.Tile%sheetTilesWide==0;
        int top=sprite.Tile*64/sheet.Width;
        ushort Sample(int x,int y)
        {
            int tile=sprite.Tile+(y>>3)*frameTilesWide+(x>>3);
            int at=aligned?(top+y)*sheet.Width+x:((tile/sheetTilesWide)*8+(y&7))*sheet.Width+(tile%sheetTilesWide)*8+(x&7);
            if(palette is null||sheet.PaletteIndices.Length==0)return sheet.Pixels[at];
            int index=sheet.PaletteIndices[at];return index==0?(ushort)0:(ushort)(0x8000|palette[index]);
        }
        if(sprite.Affine)
        {
            int halfWidth=width*(sprite.DoubleSize?2:1)/2,halfHeight=height*(sprite.DoubleSize?2:1)/2;
            for(int y=-halfHeight;y<halfHeight;y++)for(int x=-halfWidth;x<halfWidth;x++)
            {
                int sx=((x*sprite.MatrixA+y*sprite.MatrixB)>>8)+width/2,sy=((x*sprite.MatrixC+y*sprite.MatrixD)>>8)+height/2;
                int dx=sprite.Position.X+sprite.Offset.X+x,dy=sprite.Position.Y+sprite.Offset.Y+y;
                if(sx<0||sx>=width||sy<0||sy>=height||dx<0||dx>=240||dy<0||dy>=112)continue;
                ushort p=Sample(sx,sy);if((p&0x8000)==0)continue;
                int at=dy*240+dx;dest[at]=sprite.Blend&&blendEnabled?Blend(p,dest[at],eva,evb):p;
            }
            return;
        }
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int dx=sprite.Position.X+sprite.Offset.X-width/2+x,dy=sprite.Position.Y+sprite.Offset.Y-height/2+y;
            if(dx<0||dx>=240||dy<0||dy>=112)continue;
            int sx=sprite.HFlip?width-1-x:x;ushort pixel=Sample(sx,sprite.VFlip?height-1-y:y);
            if((pixel&0x8000)==0)continue;
            int at=dy*240+dx;dest[at]=sprite.Blend&&blendEnabled?Blend(pixel,dest[at],eva,evb):pixel;
        }
    }

    private static ushort Blend(int pixel,int under,int eva,int evb)
    {
        eva=Math.Clamp(eva,0,16);evb=Math.Clamp(evb,0,16);
        int result=0x8000;
        for(int shift=0;shift<=10;shift+=5)result|=Math.Min(31,((((pixel>>shift)&31)*eva+((under>>shift)&31)*evb)>>4))<<shift;
        return (ushort)result;
    }
    private static void AffineImpact(ushort[] dest,SourcePixelAsset source,HitSplatFrame hit,int eva,int evb,bool blendEnabled)
    {
        // Original ConvertScaleParam: inverse OAM matrix, zero rotation, normal 32x32 canvas.
        int inverse=0x10000/hit.Scale;
        for(int dy=-16;dy<16;dy++)for(int dx=-16;dx<16;dx++)
        {
            int sx=((dx*inverse)>>8)+16,sy=((dy*inverse)>>8)+16,x=hit.Center.X+dx,y=hit.Center.Y+dy;
            if(sx<0||sx>=32||sy<0||sy>=32||x<0||x>=240||y<0||y>=112)continue;
            int pixel=source.Pixels[sy*32+sx];if((pixel&0x8000)==0)continue;
            int at=y*240+x;
            dest[at]=blendEnabled?Blend(pixel,dest[at],eva,evb):(ushort)pixel;
        }
    }
}
