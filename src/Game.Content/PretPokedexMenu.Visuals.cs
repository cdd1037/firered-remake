using System.Collections.Immutable;

namespace Game.Content;

public sealed record PokedexVisualDefinition(ImmutableDictionary<string,PokedexAreaSprite> Sprites,
    ImmutableArray<ImmutableArray<int>> PageTurnColumns, int FadeStep, int FadeFrames);

public sealed partial class PretPokedexMenu
{
    private void ValidateVisuals()
    {
        var visuals=Document.Visuals;
        Require(visuals is not null && visuals.Sprites is not null && visuals.Sprites.Count>0,
            "Pokédex visual atlas is missing.");
        foreach(var sprite in visuals!.Sprites!.Values)
            Require(sprite.Texture is not null && Document.Assets.Contains(sprite.Texture) && sprite.SourceX>=0 && sprite.SourceY>=0 &&
                sprite.Width>0 && sprite.Height>0 && sprite.SourceX+sprite.Width<=sprite.Texture.Width &&
                sprite.SourceY+sprite.Height<=sprite.Texture.Height,"Pokédex visual crop is invalid.");
        foreach(var mode in new[]{"kanto","national"})
        {
            foreach(var id in new[]{"habitat","contents","area","entry","blank","fade","mini","caught","key/A_BUTTON","key/B_BUTTON","key/START_BUTTON","key/DPAD_LEFTRIGHT"})
                Require(visuals.Sprites.ContainsKey(mode+"/"+id),"Pokédex visual component is missing.");
            for(int i=0;i<32;i++)Require(visuals.Sprites.ContainsKey(mode+"/tile/"+i),"Pokédex frame tile missing.");
            for(int i=0;i<5;i++)Require(visuals.Sprites.ContainsKey(mode+"/circle/"+i),"Pokédex circle palette missing.");
        }
        foreach(var type in Entries.Values.SelectMany(e=>new[]{e.Type1,e.Type2}).Distinct())
            Require(visuals.Sprites.ContainsKey(type),"Pokédex type badge is missing.");
        foreach(var habitat in Document.Habitats)
            Require(visuals.Sprites.ContainsKey("category/"+habitat.Id),"Pokédex category icon missing.");
        Require(visuals.FadeStep>0 && visuals.FadeStep<=16 && visuals.FadeFrames>16/visuals.FadeStep*2,"Pokédex fade timing invalid.");
        Require(!visuals.PageTurnColumns.IsDefault && visuals.PageTurnColumns.Length==10 &&
            visuals.PageTurnColumns.All(row=>!row.IsDefault && row.Length==30 && row.All(n=>n>=0&&n<=30)),
            "Pokédex page-turn columns are invalid.");
    }
}

/// <summary>Source tile-space zoom geometry. Presentation only; no simulation clock.</summary>
public static class PokedexZoomGeometry
{
    public static (int Left,int Top,int Width,int Height,int Divider) Frame(PokedexHabitatSlot slot,int scale)
    {
        if(scale<0||scale>6)throw new ArgumentOutOfRangeException(nameof(scale));
        int width=Math.Min(28,6+scale*4),height=Math.Min(14,3+scale*2);
        int left=slot.InfoX/8-scale*2,top=slot.InfoY/8-scale;
        if(left+width+2>=30)left-=left+width+2-30;else if(left<0)left=0;
        if(top+height+2>=18)top-=top+height+2-18;else if(top<2)top=2;
        return(left,top,width,height,top+2+height/2);
    }
}


public readonly record struct PokedexTurnFrame(bool Complete,bool UseNewPage,int ColumnPhase,int FadeLevel);
public static class PokedexTurnGeometry
{
    public static PokedexTurnFrame Frame(PokedexVisualDefinition visuals,bool right,int frame)
    {
        if(frame<0)throw new ArgumentOutOfRangeException(nameof(frame));
        int columns=visuals.PageTurnColumns.Length;
        if(frame>=columns+visuals.FadeFrames)return new(true,true,columns-1,0);
        if(right)
            return frame<columns?new(false,false,columns-1-frame,0):
                new(false,true,columns-1,Math.Max(0,16-(frame-columns)/2*visuals.FadeStep));
        return frame<visuals.FadeFrames?new(false,false,columns-1,Math.Min(16,frame/2*visuals.FadeStep)):
            new(false,true,frame-visuals.FadeFrames,0);
    }
}
