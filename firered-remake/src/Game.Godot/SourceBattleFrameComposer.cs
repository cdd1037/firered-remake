using System.Collections.ObjectModel;
namespace Game.Presentation.BattleAnimations;
public sealed record SourcePixelAsset(int Width,int Height,ushort[] Pixels);

/// <summary>Bounded 240x112 source RGB555 field. Existing HUD/text remain later layers.</summary>
public sealed class SourceBattleFrameComposer
{
    public const int Width=240,Height=160;
    private readonly Dictionary<string,SourcePixelAsset> _assets=[];
    private readonly AnimationBattler[] _battlers;
    public SourceBattleFrameComposer(IReadOnlyDictionary<string,SourcePixelAsset> assets,AnimationBattler[] battlers)
    {
        foreach(var (id,asset) in assets)
        {
            if(asset.Width is <1 or >240||asset.Height is <1 or >160||asset.Pixels.Length!=asset.Width*asset.Height)throw new InvalidDataException("Campaign animation asset shape differs.");
            _assets.Add(id,asset);
        }
        _battlers=battlers;
        if(_assets["background"].Width!=240||_assets["background"].Height!=112||_assets["impact"].Width!=32||_assets["impact"].Height!=32)throw new InvalidDataException("Source field/impact shape differs.");
        foreach(var b in battlers)if(_assets[b.Asset].Width!=64||_assets[b.Asset].Height!=64)throw new InvalidDataException("Battler canvas differs.");
    }
    public SourcePixelAsset Asset(string id)=>_assets[id];
    public byte[] ComposeField(SourceAnimationFrame frame)
    {
        var pixels=new ushort[Width*112];Blit(pixels,_assets["background"],0,0);
        void Battler(AnimationBattler b)
        {
            if(!b.Visible)return;
            var center=b.DisplayCenter??b.Center(frame.Doubles);var offset=(frame.BackgroundSlots.Contains(b.Slot.Value)?frame.Offsets:frame.ObjectOffsets)[b.Slot.Value];
            Blit(pixels,_assets[b.Asset],center.X-32+offset.X,center.Y-32+offset.Y);
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
        if(frame.Doubles)
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
        var rgba=new byte[pixels.Length*4];
        for(int i=0;i<pixels.Length;i++){int p=pixels[i];rgba[i*4]=(byte)((p&31)*255/31);rgba[i*4+1]=(byte)(((p>>5)&31)*255/31);rgba[i*4+2]=(byte)(((p>>10)&31)*255/31);rgba[i*4+3]=255;}return rgba;
    }
    private static void Blit(ushort[] dest,SourcePixelAsset source,int x,int y)
    {
        for(int sy=0;sy<source.Height;sy++)for(int sx=0;sx<source.Width;sx++)
        {int dx=x+sx,dy=y+sy;ushort p=source.Pixels[sy*source.Width+sx];if(dx>=0&&dx<240&&dy>=0&&dy<112&&(p&0x8000)!=0)dest[dy*240+dx]=p;}
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
