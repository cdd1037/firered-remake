using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public static class SourceIndexedSpriteAssets
{
    public static SourcePixelAsset Read(string folder,JsonElement row,IReadOnlyDictionary<string,int> tags)
    {
        int width=row.GetProperty("width").GetInt32(),height=row.GetProperty("height").GetInt32();
        if(width is <8 or >240||height is <8 or >512||width%8!=0||height%8!=0)throw new InvalidDataException("Indexed sprite tile canvas differs.");
        byte[] ReadFile(string key)
        {
            string path=row.GetProperty(key).GetString()!;
            if(Path.GetFileName(path)!=path)throw new InvalidDataException("Indexed sprite asset must stay inside its generated pack.");
            return File.ReadAllBytes(Path.Combine(folder,path));
        }
        var packed=ReadFile("indicesPath");var paletteBytes=ReadFile("palettePath");
        if(packed.Length!=width*height/2||paletteBytes.Length!=32)throw new InvalidDataException("Indexed sprite tiles/palette length differs.");
        var palette=Enumerable.Range(0,16).Select(i=>(ushort)(paletteBytes[i*2]|paletteBytes[i*2+1]<<8)).ToArray();
        if(palette.Any(c=>c>32767))throw new InvalidDataException("Sprite palette exceeds RGB555.");
        var indices=new byte[width*height];var pixels=new ushort[indices.Length];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int tile=(y/8)*(width/8)+x/8,source=tile*32+(y%8)*4+x%8/2;
            int index=(packed[source]>>((x&1)*4))&15,at=y*width+x;indices[at]=(byte)index;
            pixels[at]=index==0?(ushort)0:(ushort)(0x8000|palette[index]);
        }
        string symbol=row.GetProperty("paletteTag").GetString()!;
        if(!tags.TryGetValue(symbol,out int tag)||tag is <0 or >65535)throw new InvalidDataException("Indexed sprite palette tag lacks source definition.");
        return new(width,height,pixels){PaletteIndices=indices,Palette=palette,PaletteTag=tag};
    }
}
