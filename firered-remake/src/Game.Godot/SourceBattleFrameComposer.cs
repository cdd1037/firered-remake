using System.Collections.ObjectModel;
namespace Game.Presentation.BattleAnimations;
public sealed record SourcePixelAsset(int Width,int Height,ushort[] Pixels);

/// <summary>Bounded 240x112 source RGB555 field. Existing HUD/text remain later layers.</summary>
public sealed class SourceBattleFrameComposer
{
    public const int Width=240,Height=160;
    private readonly Dictionary<string,SourcePixelAsset> _assets=[];
    private readonly AnimationBattler[] _battlers;
    private readonly SourceStatMaskMaterial? _statMaterial;
    private readonly SourceMetallicMaterial? _metallicMaterial;
    public SourceBattleFrameComposer(IReadOnlyDictionary<string,SourcePixelAsset> assets,AnimationBattler[] battlers,SourceStatMaskMaterial? statMaterial=null,SourceMetallicMaterial? metallicMaterial=null)
    {
        foreach(var (id,asset) in assets)
        {
            if(asset.Width is <1 or >240||asset.Height is <1 or >160||asset.Pixels.Length!=asset.Width*asset.Height)throw new InvalidDataException("Campaign animation asset shape differs.");
            _assets.Add(id,asset);
        }
        _battlers=battlers;_statMaterial=statMaterial;_metallicMaterial=metallicMaterial;
        if(_assets["background"].Width!=240||_assets["background"].Height!=112||_assets["impact"].Width!=32||_assets["impact"].Height!=32)throw new InvalidDataException("Source field/impact shape differs.");
        foreach(var b in battlers)if(_assets[b.Asset].Width!=64||_assets[b.Asset].Height!=64)throw new InvalidDataException("Battler canvas differs.");
    }
    public SourcePixelAsset Asset(string id)=>_assets[id];
    public byte[] ComposeField(SourceAnimationFrame frame)
    {
        var pixels=new ushort[Width*112];
        var palettes=frame.PaletteBlends.ToDictionary(p=>p.Palette);
        // These bounded scripts apply one shared transform to BG palettes 1, 2 and 3.
        Blit(pixels,_assets["background"],0,0,palettes.GetValueOrDefault(1));
        void Battler(AnimationBattler b)
        {
            if(!b.Visible)return;
            var center=b.DisplayCenter??b.Center(frame.Doubles);var offset=(frame.BackgroundSlots.Contains(b.Slot.Value)?frame.Offsets:frame.ObjectOffsets)[b.Slot.Value];
            var palette=palettes.GetValueOrDefault(16+b.Slot.Value);bool grey=frame.GreyscaleSlots.Contains(b.Slot.Value);
            if(frame.BattlerAffine.Length>b.Slot.Value&&frame.BattlerAffine[b.Slot.Value].DoubleSize)AffineBattler(pixels,_assets[b.Asset],center.X+offset.X,center.Y+offset.Y,frame.BattlerAffine[b.Slot.Value],palette);
            else Blit(pixels,_assets[b.Asset],center.X-32+offset.X,center.Y-32+offset.Y,palette,grey);
        }
        void Sprite(SourceVisualSpriteFrame sprite)
        {
            if(!sprite.Visible||sprite.Asset is null)return;
            if(sprite.Template=="gBasicHitSplatSpriteTemplate")AffineImpact(pixels,_assets[sprite.Asset],new(sprite.Position,sprite.Scale,sprite.Asset),frame.AlphaA,frame.AlphaB,frame.BlendEnabled);
            else BlitSprite(pixels,_assets[sprite.Asset],sprite,frame.AlphaA,frame.AlphaB,frame.BlendEnabled);
        }
        // monbg transfers the target and its visible partner to BG1/BG2 at priority 2.
        // At equal hardware priority, OBJ is in front; BG1 is in front of BG2.
        // Object battlers and effect sprites share the same persistent source OAM order.
        if(frame.Sprites.Any(s=>s.Template=="ClonedMinizeSprite_Step"))
        {
            // Hardware first selects one frontmost OBJ pixel. A translucent clone
            // blends with the background, never recursively with lower OBJ copies.
            var objects=new ushort[pixels.Length];var translucent=new bool[pixels.Length];
            void AffineObject(SourcePixelAsset asset,PixelPoint center,SourceBattlerAffine matrix,bool blend)
            {
                int half=matrix.DoubleSize?64:32;
                for(int y=-half;y<half;y++)for(int x=-half;x<half;x++)
                {
                    int sx=((x*matrix.XScale+y*matrix.MatrixB)>>8)+32,sy=((x*matrix.MatrixC+y*matrix.YScale)>>8)+32,dx=center.X+x,dy=center.Y+y;
                    if(sx<0||sx>=64||sy<0||sy>=64||dx<0||dx>=240||dy<0||dy>=112)continue;
                    ushort pixel=asset.Pixels[sy*64+sx];if((pixel&0x8000)==0)continue;
                    objects[dy*240+dx]=pixel;translucent[dy*240+dx]=blend;
                }
            }
            var ordered=_battlers.Where(b=>b.Visible).Select(b=>(Rank:frame.BattlerOamRanks[b.Slot.Value],Battler:(AnimationBattler?)b,Sprite:(SourceVisualSpriteFrame?)null))
                .Concat(frame.Sprites.Where(s=>s.Visible).Select(s=>(s.OamRank,(AnimationBattler?)null,(SourceVisualSpriteFrame?)s)));
            foreach(var item in ordered.OrderByDescending(x=>x.Item1))
            {
                if(item.Item2 is {} b)
                {
                    var center=b.DisplayCenter??b.Center(frame.Doubles);var offset=frame.ObjectOffsets[b.Slot.Value];
                    AffineObject(_assets[b.Asset],new(center.X+offset.X,center.Y+offset.Y),frame.BattlerAffine[b.Slot.Value],false);
                }
                else
                {
                    var clone=item.Item3!;
                    if(clone.Template!="ClonedMinizeSprite_Step"||clone.Asset is null)throw new InvalidDataException("Unadmitted object mixed with battler copies.");
                    AffineObject(_assets[clone.Asset],new(clone.Position.X+clone.Offset.X,clone.Position.Y+clone.Offset.Y),new(false,clone.Scale,clone.Scale),true);
                }
            }
            for(int i=0;i<pixels.Length;i++)if((objects[i]&0x8000)!=0)pixels[i]=translucent[i]&&frame.BlendEnabled?Blend(objects[i],pixels[i],frame.AlphaA,frame.AlphaB):objects[i];
        }
        else if(frame.Doubles)
        {
            foreach(var b in _battlers.Where(b=>frame.BackgroundSlots.Contains(b.Slot.Value)).OrderByDescending(b=>b.BackgroundRank))Battler(b);
            var objects=_battlers.Where(b=>!frame.BackgroundSlots.Contains(b.Slot.Value)).Select(b=>(Rank:frame.BattlerOamRanks[b.Slot.Value],Battler:(AnimationBattler?)b,Sprite:(SourceVisualSpriteFrame?)null))
                .Concat(frame.Sprites.Select(s=>(s.OamRank,(AnimationBattler?)null,(SourceVisualSpriteFrame?)s)));
            foreach(var item in objects.OrderByDescending(x=>x.Item1))
                if(item.Item2 is {} b)Battler(b);else Sprite(item.Item3!);
        }
        else
        {
            // Preserve the accepted singles composition and every existing pixel hash.
            foreach(var b in _battlers.OrderBy(b=>frame.BackgroundSlots.Contains(b.Slot.Value)?0:1).ThenBy(b=>b.Slot.IsPlayer?1:0))Battler(b);
            foreach(var sprite in frame.Sprites.OrderByDescending(s=>s.OamRank))Sprite(sprite);
        }
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
    private static void Blit(ushort[] dest,SourcePixelAsset source,int x,int y,SourcePaletteBlend? palette=null,bool greyscale=false)
    {
        for(int sy=0;sy<source.Height;sy++)for(int sx=0;sx<source.Width;sx++)
        {int dx=x+sx,dy=y+sy;ushort p=source.Pixels[sy*source.Width+sx];if(dx>=0&&dx<240&&dy>=0&&dy<112&&(p&0x8000)!=0){if(greyscale){int average=((p&31)+((p>>5)&31)+((p>>10)&31))/3;p=(ushort)(0x8000|average|(average<<5)|(average<<10));}dest[dy*240+dx]=palette is null?p:SourcePaletteMath.Blend(p,palette.Coefficient,palette.Color);}}
    }
    private static void AffineBattler(ushort[] dest,SourcePixelAsset source,int centerX,int centerY,SourceBattlerAffine affine,SourcePaletteBlend? palette)
    {
        // SetSpriteRotScale supplies the inverse sampling matrix directly to the
        // GBA BIOS. Signed products use arithmetic shifts, including the off-diagonal terms.
        for(int y=-64;y<64;y++)for(int x=-64;x<64;x++)
        {
            int sx=((x*affine.XScale+y*affine.MatrixB)>>8)+32,sy=((x*affine.MatrixC+y*affine.YScale)>>8)+32,dx=centerX+x,dy=centerY+y;
            if(sx<0||sx>=64||sy<0||sy>=64||dx<0||dx>=240||dy<0||dy>=112)continue;
            ushort p=source.Pixels[sy*64+sx];if((p&0x8000)==0)continue;
            dest[dy*240+dx]=palette is null?p:SourcePaletteMath.Blend(p,palette.Coefficient,palette.Color);
        }
    }
    private static void BlitSprite(ushort[] dest,SourcePixelAsset sheet,SourceVisualSpriteFrame sprite,int eva,int evb,bool blendEnabled)
    {
        // Source 32x32 4bpp frames are 16 sequential 8x8 tiles, stacked in the imported sheet.
        if(sprite.Tile<0||sprite.Tile%16!=0||sprite.Tile/16*32+32>sheet.Height)throw new InvalidDataException("Source sprite tile frame exceeds sheet.");
        int top=sprite.Tile/16*32;
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {
            int dx=sprite.Position.X+sprite.Offset.X-16+x,dy=sprite.Position.Y+sprite.Offset.Y-16+y;
            if(dx<0||dx>=240||dy<0||dy>=112)continue;
            int sx=sprite.HFlip?31-x:x;ushort pixel=sheet.Pixels[(top+y)*sheet.Width+sx];
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
