using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public sealed record SourceBattlerFormMaterial(AnimationBattler Geometry,SourcePixelAsset Pixels);

/// <summary>Source form geometry and materials from the shared battle-art producer.</summary>
public sealed class SourceBattleFormCatalog
{
    private readonly Dictionary<(int Species,string Side,int Form),AnimationBattler> _geometry=[];
    private readonly Dictionary<(int Species,string Side,bool Shiny),SourcePixelAsset> _baseFrames=[];
    private readonly Dictionary<(int Species,string Side,int Form,bool Shiny),SourcePixelAsset> _pixels=[];
    public SourceBattleFormCatalog(string folder)
    {
        using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"manifest.json")));
        foreach(var row in document.RootElement.GetProperty("formGeometry").EnumerateArray())
        {
            int species=row.GetProperty("species").GetInt32(),form=row.GetProperty("form").GetInt32();
            string side=row.GetProperty("side").GetString()!;
            if(side is not("front" or "back")||form<0)throw new InvalidDataException("Source battler form geometry differs.");
            _geometry.Add((species,side,form),new(new(side=="back"?0:1),"",row.GetProperty("yOffset").GetInt32(),
                row.GetProperty("elevation").GetInt32(),row.GetProperty("sourceWidth").GetInt32(),row.GetProperty("sourceHeight").GetInt32()));
        }
        foreach(var row in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            if(row.TryGetProperty("frame",out var frame))
            {
                int species=row.GetProperty("species").GetInt32();string face=row.GetProperty("side").GetString()!,baseVariant=row.GetProperty("paletteVariant").GetString()!;
                if(frame.GetInt32()!=0||face is not("front" or "back")||baseVariant is not("normal" or "shiny")||row.TryGetProperty("form",out _)||
                    !document.RootElement.GetProperty("geometry").EnumerateArray().Any(g=>g.GetProperty("species").GetInt32()==species&&g.GetProperty("side").GetString()==face))
                    throw new InvalidDataException("Source base-frame asset metadata differs.");
                var basePixels=SourceHitAnimationCatalog.Asset(document,folder,row.GetProperty("id").GetString()!);
                if(basePixels.Width!=64||basePixels.Height!=64)throw new InvalidDataException("Source base-frame canvas differs.");
                _baseFrames.Add((species,face,baseVariant=="shiny"),basePixels);continue;
            }
            if(!row.TryGetProperty("form",out var form))continue;
            string side=row.GetProperty("side").GetString()!,variant=row.GetProperty("paletteVariant").GetString()!;
            var key=(row.GetProperty("species").GetInt32(),side,form.GetInt32());
            if(!_geometry.ContainsKey(key)||variant is not("normal" or "shiny"))throw new InvalidDataException("Source form asset dependency differs.");
            var pixels=SourceHitAnimationCatalog.Asset(document,folder,row.GetProperty("id").GetString()!);
            if(pixels.Width!=64||pixels.Height!=64)throw new InvalidDataException("Source form frame differs.");
            _pixels.Add((key.Item1,side,key.Item3,variant=="shiny"),pixels);
        }
        foreach(var key in _geometry.Keys)
            foreach(bool shiny in new[]{false,true})
                if(!_pixels.ContainsKey((key.Species,key.Side,key.Form,shiny)))throw new InvalidDataException("Source form palette variant is missing.");
    }
    public bool TryBaseFrame(int species,string face,bool shiny,out SourcePixelAsset pixels)
        =>_baseFrames.TryGetValue((species,face,shiny),out pixels!);
    public bool HasForms(int species)=>_geometry.Keys.Any(k=>k.Species==species);
    public SourceBattlerFormMaterial Resolve(int species,AnimationSlot slot,int form,bool shiny,bool transformed=false)
    {
        string side=slot.IsPlayer?"back":"front";
        if(!_geometry.TryGetValue((species,side,form),out var geometry)||!_pixels.TryGetValue((species,side,form,shiny),out var pixels))
            throw new InvalidDataException("Source battler form is unavailable.");
        if(transformed)pixels=TransformPixels(pixels);
        return new(geometry with{Slot=slot},pixels);
    }
    /// <summary>HandleSpeciesGfxDataChange copies the actor-owned palette with white coefficient six into its new base colors.</summary>
    public static SourcePixelAsset TransformPixels(SourcePixelAsset pixels)
    {
        var values=new ushort[pixels.Pixels.Length];
        for(int i=0;i<values.Length;i++)values[i]=SourcePaletteMath.Blend(pixels.Pixels[i],6,0x7fff);
        return new(pixels.Width,pixels.Height,values);
    }

}
