using System.Collections.ObjectModel;
namespace Game.Presentation.BattleAnimations;
public sealed record SourcePixelAsset(int Width,int Height,ushort[] Pixels)
{ public byte[] PaletteIndices{get;init;}=[]; public ushort[] Palette{get;init;}=[]; public int PaletteTag{get;init;}=-1; }

/// <summary>Bounded 240x112 source RGB555 field. Existing HUD/text remain later layers.</summary>
public sealed class SourceBattleFrameComposer
{
    public const int Width=240,Height=160;
    private readonly Dictionary<string,SourcePixelAsset> _assets=[];
    private readonly AnimationBattler[] _battlers;
    private readonly SourceStatMaskMaterial? _statMaterial;
    private readonly SourceMetallicMaterial? _metallicMaterial;
    private readonly SourceAnimationBackgroundSurface? _backgroundSurface;
    private readonly IReadOnlyDictionary<int,SourceBackgroundAsset> _moveBackgrounds;
    private int _backgroundRevision;
    private readonly ushort[] _objectLayer=new ushort[Width*112],_objectPixels=new ushort[Width*112];
    private readonly bool[] _objectTranslucency=new bool[Width*112];
    private readonly byte[] _backgroundPriorities=new byte[Width*112],_objectPriorities=new byte[Width*112];
    public SourceBattleFrameComposer(IReadOnlyDictionary<string,SourcePixelAsset> assets,AnimationBattler[] battlers,SourceStatMaskMaterial? statMaterial=null,SourceMetallicMaterial? metallicMaterial=null,SourceAnimationBackgroundSurface? backgroundSurface=null,IReadOnlyDictionary<int,SourceBackgroundAsset>? moveBackgrounds=null)
    {
        foreach(var (id,asset) in assets)
        {
            if(asset.Width is <1 or >240||asset.Height is <1 or >512||asset.Pixels.Length!=asset.Width*asset.Height)throw new InvalidDataException("Campaign animation asset shape differs.");
            _assets.Add(id,asset);
        }
        _battlers=battlers;_statMaterial=statMaterial;_metallicMaterial=metallicMaterial;_backgroundSurface=backgroundSurface;_moveBackgrounds=moveBackgrounds??new Dictionary<int,SourceBackgroundAsset>();
        if(_assets["background"].Width!=240||_assets["background"].Height!=112||_assets["impact"].Width!=32||_assets["impact"].Height!=32)throw new InvalidDataException("Source field/impact shape differs.");
        foreach(var b in battlers)if(_assets[b.Asset].Width!=64||_assets[b.Asset].Height!=64)throw new InvalidDataException("Battler canvas differs.");
    }
    public SourcePixelAsset Asset(string id)=>_assets[id];
    public byte[] ComposeResidualSprites(SourceAnimationFrame frame)
    {
        if(frame.Active)throw new InvalidOperationException("Residual overlay requires a retired script.");
        var pixels=new ushort[Width*112];
        foreach(var sprite in frame.Sprites.Where(s=>s.Visible&&s.Asset is not null).OrderByDescending(s=>s.OamRank))
        {
            if(sprite.Blend)throw new InvalidDataException("Residual source blend needs destination layer sampling.");
            BlitSprite(pixels,_assets[sprite.Asset!],sprite,0,0,false,frame.SpritePalettes.GetValueOrDefault(_assets[sprite.Asset!].PaletteTag));
        }
        var rgba=new byte[pixels.Length*4];
        for(int i=0;i<pixels.Length;i++){int p=pixels[i];rgba[i*4]=(byte)((p&31)*255/31);rgba[i*4+1]=(byte)(((p>>5)&31)*255/31);rgba[i*4+2]=(byte)(((p>>10)&31)*255/31);rgba[i*4+3]=(byte)((p&0x8000)!=0?255:0);}
        return rgba;
    }
    public byte[] ComposeField(SourceAnimationFrame frame)
    {
        var pixels=new ushort[Width*112];
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
            _backgroundSurface.Render(pixels,240,112,background.ScrollX,background.ScrollY,background.ScreenSize,background.Darkening,frame.PaletteBlends,frame.InvertedPalettes,background.FadedPalette);
        }
        else
        {
            if(frame.MoveBackground is {Id:not null} or {Darkening:>0})throw new InvalidDataException("Move background requires its indexed source surface.");
        // The flattened terrain contains source BG palette banks 1..3 only. Every
        // admitted selector transforms all three identically. Fail closed if a later
        // script requests distinct banks; it then needs per-pixel bank provenance.
        var backgroundBlend=palettes.GetValueOrDefault(1);
        if(new[]{2,3}.Any(bank=>palettes.GetValueOrDefault(bank) is {} other
            ?backgroundBlend is null||other.Coefficient!=backgroundBlend.Coefficient||other.Color!=backgroundBlend.Color
            :backgroundBlend is not null))throw new InvalidDataException("Terrain palette banks require source membership data.");
        Blit(pixels,_assets["background"],0,0,palettes.GetValueOrDefault(1),inverted:frame.InvertedPalettes.Contains(1));
        }
        // BG copies retain their original 64x64 tiles. PrepareBattlerSpriteForRotScale
        // can expose a distinct affine OBJ while this copy continues scrolling.
        if(frame.BattlerBackgroundPriorities.Length!=2||frame.BattlerBackgroundPriorities.Any(p=>p is <0 or >3))throw new InvalidDataException("Battler background priorities exceed source registers.");
        var backgroundPriority=_backgroundPriorities;Array.Fill(backgroundPriority,(byte)3);
        var layer=_objectLayer;
        foreach(var b in _battlers.Where(b=>b.Visible&&frame.BackgroundSlots.Contains(b.Slot.Value)).OrderByDescending(b=>b.BackgroundRank))
        {
            Array.Clear(layer);var center=b.DisplayCenter??b.Center(frame.Doubles);var offset=frame.Offsets[b.Slot.Value];
            var copy=frame.StaticBackgroundCopies.SingleOrDefault(c=>c.Slot==b.Slot.Value);
            if(copy is not null)offset=copy.Offset;
            Blit(layer,_assets[b.Asset],center.X+offset.X-32,center.Y+offset.Y-32,copy is null?palettes.GetValueOrDefault(16+b.Slot.Value):copy.Palette,copy?.Greyscale??frame.GreyscaleSlots.Contains(b.Slot.Value),copy?.Inverted??frame.InvertedPalettes.Contains(16+b.Slot.Value));
            int priority=frame.BattlerBackgroundPriorities[b.BackgroundRank-1];
            for(int i=0;i<layer.Length;i++)if((layer[i]&0x8000)!=0&&priority<=backgroundPriority[i]){pixels[i]=layer[i];backgroundPriority[i]=(byte)priority;}
        }
        // The PPU selects one frontmost OBJ pixel before alpha blending. Lower OBJ
        // pixels never become another OBJ's second target, including battler copies.
        var objects=_objectPixels;Array.Clear(objects);var translucent=_objectTranslucency;var objectPriority=_objectPriorities;
        if(frame.Sprites.Any(s=>s.OamPriority is <0 or >3))throw new InvalidDataException("Object priority exceeds source hardware field.");
        var ordered=_battlers.Where(b=>b.Visible&&(!frame.BackgroundSlots.Contains(b.Slot.Value)||frame.RestoredObjectSlots.Contains(b.Slot.Value)))
            .Select(b=>(Priority:2,Rank:frame.BattlerOamRanks.Length>b.Slot.Value?frame.BattlerOamRanks[b.Slot.Value]:b.Slot.Value,Battler:(AnimationBattler?)b,Sprite:(SourceVisualSpriteFrame?)null))
            .Concat(frame.Sprites.Where(s=>s.Visible&&s.Asset is not null).Select(s=>(s.OamPriority,s.OamRank,(AnimationBattler?)null,(SourceVisualSpriteFrame?)s)));
        foreach(var item in ordered.OrderByDescending(x=>x.Item1).ThenByDescending(x=>x.Item2))
        {
            Array.Clear(layer);bool blend;
            if(item.Item3 is {} b)
            {
                int slot=b.Slot.Value;var center=b.DisplayCenter??b.Center(frame.Doubles);var offset=frame.ObjectOffsets.Length>slot?frame.ObjectOffsets[slot]:frame.Offsets[slot];
                var matrix=frame.BattlerAffine.Length>slot?frame.BattlerAffine[slot]:new(false,256,256);
                AffineBattler(layer,_assets[b.Asset],center.X+offset.X,center.Y+offset.Y,matrix,palettes.GetValueOrDefault(16+slot),frame.InvertedPalettes.Contains(16+slot),frame.GreyscaleSlots.Contains(slot));
                blend=frame.BlendedObjectSlots.Contains(slot);
            }
            else
            {
                var sprite=item.Item4!;blend=sprite.Blend;var center=new PixelPoint(sprite.Position.X+sprite.Offset.X,sprite.Position.Y+sprite.Offset.Y);
                if(sprite.BattlerCopySlot>=0)
                {
                    int slot=sprite.BattlerCopySlot;var matrix=new SourceBattlerAffine(false,sprite.Scale,sprite.Scale);
                    AffineBattler(layer,_assets[sprite.Asset!],center.X,center.Y,matrix,palettes.GetValueOrDefault(16+slot),frame.InvertedPalettes.Contains(16+slot),frame.GreyscaleSlots.Contains(slot));
                }
                else if(sprite.Template is "gBasicHitSplatSpriteTemplate" or "gFlashingHitSplatSpriteTemplate")AffineImpact(layer,_assets[sprite.Asset!],new(sprite.Position,sprite.Scale,sprite.Asset!),0,0,false);
                else BlitSprite(layer,_assets[sprite.Asset!],sprite,0,0,false,frame.SpritePalettes.GetValueOrDefault(_assets[sprite.Asset!].PaletteTag));
            }
            for(int i=0;i<layer.Length;i++)if((layer[i]&0x8000)!=0){objects[i]=layer[i];translucent[i]=blend;objectPriority[i]=(byte)item.Item1;}
        }
        for(int i=0;i<pixels.Length;i++)if((objects[i]&0x8000)!=0&&objectPriority[i]<=backgroundPriority[i])
            pixels[i]=translucent[i]&&frame.BlendEnabled?Blend(objects[i],pixels[i],frame.AlphaA,frame.AlphaB):objects[i];
        if(frame.StatMask is {WindowEnabled:true,BackgroundLoaded:true,MaskVisible:true} mask)
        {
            if(_statMaterial is null)throw new InvalidDataException("Source stat material missing.");
            var selected=_battlers.Single(b=>b.Slot.Value==mask.MaskSlot);var center=selected.DisplayCenter??selected.Center(frame.Doubles);
            var silhouette=_assets[selected.Asset];
            bool Opaque(AnimationBattler b,int x,int y)
            {var p=b.DisplayCenter??b.Center(frame.Doubles);int sx=x-p.X+32,sy=y-p.Y+32;return b.Visible&&sx>=0&&sx<64&&sy>=0&&sy<64&&(_assets[b.Asset].Pixels[sy*64+sx]&0x8000)!=0;}
            var priority=mask.PriorityPartner<0?null:_battlers.Single(b=>b.Slot.Value==mask.PriorityPartner);
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
            var selected=_battlers.Single(b=>b.Slot.Value==metal.MaskSlot);var center=selected.DisplayCenter??selected.Center(frame.Doubles);var silhouette=_assets[selected.Asset];
            var partner=metal.PriorityPartner<0?null:_battlers.Single(b=>b.Slot.Value==metal.PriorityPartner);
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
    private static void Blit(ushort[] dest,SourcePixelAsset source,int x,int y,SourcePaletteBlend? palette=null,bool greyscale=false,bool inverted=false)
    {
        for(int sy=0;sy<source.Height;sy++)for(int sx=0;sx<source.Width;sx++)
        {int dx=x+sx,dy=y+sy;ushort p=source.Pixels[sy*source.Width+sx];if(dx>=0&&dx<240&&dy>=0&&dy<112&&(p&0x8000)!=0){if(greyscale){int average=((p&31)+((p>>5)&31)+((p>>10)&31))/3;p=(ushort)(0x8000|average|(average<<5)|(average<<10));}dest[dy*240+dx]=SourcePaletteMath.Apply(p,palette,inverted);}}
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
