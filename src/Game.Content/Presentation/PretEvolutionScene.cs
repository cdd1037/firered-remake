using System.Collections.Immutable;

namespace Game.Content;
public sealed record EvolutionSceneAsset(string Id, int Width, int Height, string TexturePath);
public sealed record EvolutionSceneDocument(int SchemaVersion, int PaletteCount, int AtlasColumns,
    ImmutableArray<ImmutableArray<int>> PaletteControls, ImmutableArray<EvolutionSceneAsset> Assets);

/// <summary>Converted scene pixels and palette cycling, loaded without original source access.</summary>
public sealed class PretEvolutionScene
{
    public const string Directory = "content/generated/ui/evolution-scene/";
    public EvolutionSceneDocument Document { get; }
    private readonly ImmutableDictionary<string, byte[]> _textures;
    private PretEvolutionScene(EvolutionSceneDocument document, ImmutableDictionary<string, byte[]> textures)
        => (Document,_textures)=(document,textures);
    public static PretEvolutionScene Load(string root)
    {
        var d=ContentJson.ParseStrict<EvolutionSceneDocument>(GeneratedResourceFile.Read(root,Directory+"scene.json"),ContentJson.CreateOptions());
        if(d.SchemaVersion!=1 || d.PaletteCount is <1 or >256 || d.AtlasColumns is <1 or >16 || d.PaletteControls.IsDefaultOrEmpty || d.Assets.IsDefaultOrEmpty)
            throw new InvalidDataException("Invalid evolution scene document.");
        foreach(var c in d.PaletteControls)
            if(c.Length!=4 || c[0]<0 || c[1]<c[0] || c[1]>=d.PaletteCount || c[2]<=0 || c[3]<0)
                throw new InvalidDataException("Invalid evolution palette control.");
        var expected=new HashSet<string>(["background-0","background-1","hatch","sparkle"],StringComparer.Ordinal);
        var textures=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var a in d.Assets)
        {
            int width=a.Id=="hatch"?240:a.Id=="sparkle"?8:d.AtlasColumns*256;
            int height=a.Id=="hatch"?160:a.Id=="sparkle"?8:((d.PaletteCount+d.AtlasColumns-1)/d.AtlasColumns)*256;
            if(!expected.Remove(a.Id) || a.Width!=width || a.Height!=height || a.TexturePath!=a.Id+".png")
                throw new InvalidDataException("Invalid evolution scene asset.");
            var bytes=GeneratedResourceFile.Read(root,Directory+a.TexturePath);
            if(bytes.Length<33 || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) ||
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4))!=width ||
                System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4))!=height)
                throw new InvalidDataException("Invalid evolution scene PNG.");
            textures.Add(a.Id,bytes);
        }
        if(expected.Count!=0)throw new InvalidDataException("Incomplete evolution scene assets.");
        return new(d,textures.ToImmutable());
    }
    public byte[] Texture(string id)=>_textures[id].ToArray();

    // Source Task_UpdateBgPalette: initial 21-frame delay; post-increment timer > DELAY.
    public int PaletteAt(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        int timer=0, stage=0, cycle=0, control=0, loaded=0;
        for(int f=21;f<frame && control<Document.PaletteControls.Length;f++)
        {
            var c=Document.PaletteControls[control];
            if(timer++<=c[3])continue;
            if(stage==c[1])
            {
                if(++cycle==c[2]){cycle=0;control++;}
                if(control<Document.PaletteControls.Length)stage=Document.PaletteControls[control][0];
            }
            else {loaded=stage;timer=0;stage++;}
        }
        return loaded;
    }
}
