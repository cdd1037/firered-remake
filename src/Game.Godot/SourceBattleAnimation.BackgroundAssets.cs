using System.Buffers.Binary;
using System.Text.Json;

namespace Game.Presentation.BattleAnimations;

/// <summary>A source BG3 load: packed 4bpp tile and screenblock-map prefixes, plus bank 2 onward.</summary>
public sealed class SourceBackgroundAsset
{
    private readonly byte[] _tiles, _tilemap;
    private readonly ushort[] _palette;
    public ReadOnlyMemory<byte> Tiles => _tiles;
    public ReadOnlyMemory<byte> Tilemap => _tilemap;
    public ReadOnlyMemory<ushort> Palette => _palette;
    public SourceBackgroundAsset(ReadOnlySpan<byte> tiles, ReadOnlySpan<byte> tilemap, ReadOnlySpan<ushort> palette)
    {
        if (tiles.Length is <32 or >16384 || tiles.Length % 32 != 0 || tilemap.Length is <2 or >4096 || tilemap.Length % 2 != 0 || palette.Length is not (16 or 48))
            throw new InvalidDataException("Source BG3 resource dimensions differ.");
        for (int i = 0; i < tilemap.Length; i += 2)
        {
            int entry = BinaryPrimitives.ReadUInt16LittleEndian(tilemap[i..]);
            if ((entry >> 12) is 1 or >4 || (entry & 1023) * 32 >= tiles.Length)
                throw new InvalidDataException("Source BG3 tile or palette exceeds resource ownership.");
        }
        foreach (ushort color in palette)
            if ((color & 0x8000) != 0) throw new InvalidDataException("Source palette must contain RGB555 values.");
        _tiles = tiles.ToArray(); _tilemap = tilemap.ToArray(); _palette = palette.ToArray();
    }
}

/// <summary>One source BG1 character/map load with its actual palette-bank ownership.</summary>
public sealed class SourceOverlayAsset
{
    private readonly byte[] _tiles,_tilemap;
    private readonly ushort[] _palette;
    public ReadOnlyMemory<ushort> Palette=>_palette;
    public int PaletteBank{get;}
    public int ScreenSize{get;}
    public SourceOverlayAsset(ReadOnlySpan<byte> tiles,ReadOnlySpan<byte> tilemap,ReadOnlySpan<ushort> palette,int paletteBank=8,int screenSize=0)
    {
        if(tiles.Length is <32 or >8192||tiles.Length%32!=0||screenSize is <0 or >1||tilemap.Length!=(screenSize==0?2048:4096)||palette.Length!=16||paletteBank!=8)
            throw new InvalidDataException("Source BG1 overlay shape differs.");
        for(int i=0;i<tilemap.Length;i+=2)
        {
            int entry=BinaryPrimitives.ReadUInt16LittleEndian(tilemap[i..]);
            if((entry&1023)*32>=tiles.Length||(entry>>12) is not (0 or 1 or 8))
                throw new InvalidDataException("Source BG1 overlay map exceeds its owned tiles or palette banks.");
        }
        foreach(ushort color in palette)if((color&0x8000)!=0)throw new InvalidDataException("Source overlay palette is not RGB555.");
        _tiles=tiles.ToArray();_tilemap=tilemap.ToArray();_palette=palette.ToArray();PaletteBank=paletteBank;ScreenSize=screenSize;
    }
    public SourceBackgroundPixel Sample(int x,int y)
    {
        x&=ScreenSize==0?255:511;y&=255;
        int cell=(x>>8)*1024+(y>>3)*32+((x&255)>>3);
        int entry=BinaryPrimitives.ReadUInt16LittleEndian(_tilemap.AsSpan(cell*2));
        int tx=x&7,ty=y&7;
        if((entry&0x400)!=0)tx=7-tx;if((entry&0x800)!=0)ty=7-ty;
        int packed=_tiles[(entry&1023)*32+ty*4+tx/2],index=(packed>>((tx&1)*4))&15;
        return new(entry>>12,index);
    }
    public void Render(Span<ushort> destination,int width,int height,int scrollX,int scrollY,
        SourceAnimationBackgroundSurface? background,ReadOnlySpan<SourcePaletteBlend> blends,ReadOnlySpan<int> inverted,ReadOnlySpan<SourcePaletteEntry> fadedEntries=default,SourceScanlineFrame? scanline=null)
    {
        scanline?.Validate();
        if(width is <1 or >256||height is <1 or >256||destination.Length!=width*height)throw new ArgumentException("Source overlay render shape differs.");
        Span<ushort> colors=stackalloc ushort[9*16];
        foreach(int bank in new[]{0,1,8})
        {
            SourcePaletteBlend? blend=null;foreach(var candidate in blends)if(candidate.Palette==bank){blend=candidate;break;}
            for(int i=0;i<16;i++)colors[bank*16+i]=SourcePaletteMath.Apply(bank==8?_palette[i]:background?.PaletteColor(bank,i)??0,blend,inverted.Contains(bank));
        }
        foreach(var entry in fadedEntries)
        {if(entry.Index is <0 or >255)throw new InvalidDataException("Source BG palette entry differs.");if(entry.Index<colors.Length)colors[entry.Index]=entry.Color;}
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int sx=scrollX,sy=scrollY;
            if(scanline is {Destination:SourceScanlineDestination.Bg1Horizontal})
            {sx=scanline.Words[y*(scanline.Word32?2:1)];if(scanline.Word32)sy=scanline.Words[y*2+1];}
            var pixel=Sample(x+sx,y+sy);
            if(pixel.Index!=0&&pixel.Bank!=8&&background is null)throw new InvalidDataException("Source overlay references an unavailable base palette.");
            destination[y*width+x]=pixel.Index==0?(ushort)0:(ushort)(colors[pixel.Bank*16+pixel.Index]|0x8000);
        }
    }
}

/// <summary>Scene-owned lazy resources. The atlas stream is closed with its owning scene.</summary>
public sealed class SourceAnimationBackgroundCatalog : IDisposable
{
    private readonly record struct Block(int Offset, int Length);
    private readonly record struct Entry(Block Tiles, Block Tilemap, Block Palette);
    private readonly Dictionary<int, Entry> _terrains = [], _moves = [];
    private readonly Dictionary<Entry, SourceBackgroundAsset> _assets = [];
    private readonly record struct OverlayEntry(Entry Data,int ScreenSize);
    private readonly Dictionary<string,OverlayEntry> _overlays=[];
    private readonly Dictionary<OverlayEntry,SourceOverlayAsset> _overlayAssets=[];
    private readonly FileStream _atlas;
    private readonly Block _basePalette;
    private ushort[]? _baseColors;
    private bool _disposed;
    public int CachedAssetCount => _assets.Count;
    public IEnumerable<int> TerrainIds => _terrains.Keys;
    public IEnumerable<int> MoveIds => _moves.Keys;
    public SourceAnimationBackgroundCatalog(string folder)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "manifest.json")));
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported source background format.");
        _atlas = File.OpenRead(Path.Combine(folder, "atlas.bin"));
        try
        {
            if (_atlas.Length is <1 or >1048576) throw new InvalidDataException("Source background atlas exceeds bounded catalog.");
            Block ParseBlock(JsonElement value, int minimum, int maximum, int alignment)
            {
                if (value.GetArrayLength() != 2) throw new InvalidDataException("Source background block shape differs.");
                int offset = value[0].GetInt32(), length = value[1].GetInt32();
                if (offset < 0 || length < minimum || length > maximum || length % alignment != 0 || (long)offset + length > _atlas.Length)
                    throw new InvalidDataException("Source background block exceeds atlas.");
                return new(offset, length);
            }
            _basePalette = ParseBlock(root.GetProperty("basePalette"), 64, 64, 2);
            void ReadEntries(string key, Dictionary<int, Entry> entries, bool terrain)
            {
                foreach (var row in root.GetProperty(key).EnumerateArray())
                {
                    int id = row.GetProperty("id").GetInt32();
                    if (id < 0 || !entries.TryAdd(id, new(ParseBlock(row.GetProperty("tiles"), 32, 16384, 32),
                        ParseBlock(row.GetProperty("tilemap"), terrain ? 4096 : 2, 4096, 2),
                        ParseBlock(row.GetProperty("palette"), terrain ? 96 : 32, terrain ? 96 : 32, 2))))
                        throw new InvalidDataException("Invalid or duplicate source background identifier.");
                }
                if (entries.Count is <1 or >64) throw new InvalidDataException("Unbounded or empty source background catalog.");
            }
            ReadEntries("terrains", _terrains, true); ReadEntries("moves", _moves, false);
            if(root.TryGetProperty("overlays",out var overlayRows))foreach(var row in overlayRows.EnumerateArray())
            {
                string id=row.GetProperty("id").GetString()??throw new InvalidDataException("Null source overlay id.");
                int screenSize=row.TryGetProperty("screenSize",out var size)?size.GetInt32():0;
                int mapLength=screenSize==0?2048:4096;
                if(id.Length is <1 or >80||screenSize is <0 or >1||row.GetProperty("paletteBank").GetInt32()!=8||
                    !_overlays.TryAdd(id,new(new(ParseBlock(row.GetProperty("tiles"),32,8192,32),
                        ParseBlock(row.GetProperty("tilemap"),mapLength,mapLength,2),ParseBlock(row.GetProperty("palette"),32,32,2)),screenSize)))
                    throw new InvalidDataException("Invalid source overlay entry.");
            }
            if(_overlays.Count>64)throw new InvalidDataException("Unbounded source overlay catalog.");
        }
        catch { _atlas.Dispose(); throw; }
    }
    private byte[] Read(Block block)
    {
        var bytes = new byte[block.Length]; _atlas.Position = block.Offset; _atlas.ReadExactly(bytes); return bytes;
    }
    private static ushort[] Colors(byte[] bytes)
    {
        var colors = new ushort[bytes.Length / 2];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2));
            if ((colors[i] & 0x8000) != 0) throw new InvalidDataException("Invalid source palette color.");
        }
        return colors;
    }
    private SourceBackgroundAsset Asset(Dictionary<int, Entry> entries, int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!entries.TryGetValue(id, out var entry)) throw new InvalidDataException($"Missing generated source background {id}.");
        if (!_assets.TryGetValue(entry, out var asset))
        {
            asset = new(Read(entry.Tiles), Read(entry.Tilemap), Colors(Read(entry.Palette))); _assets.Add(entry, asset);
        }
        return asset;
    }
    public SourceOverlayAsset Overlay(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!_overlays.TryGetValue(id,out var entry))throw new InvalidDataException($"Missing source overlay {id}.");
        if(!_overlayAssets.TryGetValue(entry,out var asset))
        {asset=new(Read(entry.Data.Tiles),Read(entry.Data.Tilemap),Colors(Read(entry.Data.Palette)),screenSize:entry.ScreenSize);_overlayAssets.Add(entry,asset);}
        return asset;
    }
    public SourceBackgroundAsset Move(int id) => Asset(_moves, id);
    public SourceBackgroundAsset Terrain(int id) => Asset(_terrains, id);
    public SourceAnimationBackgroundSurface CreateSurface(int terrainId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _baseColors ??= Colors(Read(_basePalette));
        return new(Terrain(terrainId), _baseColors);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _atlas.Dispose();
    }
}

public readonly record struct SourceBackgroundPixel(int Bank, int Index);

/// <summary>Retains source BG3 writes instead of inventing pixels outside a move's partial map.</summary>
public sealed class SourceAnimationBackgroundSurface
{
    // BattleMain clears VRAM. BG3 charbase 2 starts at 0x8000; screenblocks 26/27
    // are at 0xD000/0xD800. Keeping their actual alias in char memory preserves writes.
    private const int TilemapAddress = 0x5000;
    private readonly byte[] _vram = new byte[0x8000];
    private readonly ushort[] _palette = new ushort[80];
    private readonly SourceBackgroundAsset _terrain;
    // Current BG3 resources never address bank 1: LoadBattleMenuWindowGfx later
    // replaces that bank with the selected user frame, owned by the HUD renderer.
    private readonly ushort[] _basePalette;
    public SourceAnimationBackgroundSurface(SourceBackgroundAsset terrain, ReadOnlySpan<ushort> basePalette)
    {
        if (terrain.Tilemap.Length != 4096 || terrain.Palette.Length != 48 || basePalette.Length != 32)
            throw new InvalidDataException("Source terrain must initialize two screenblocks and palette banks 0..4.");
        foreach (ushort color in basePalette)
            if ((color & 0x8000) != 0) throw new InvalidDataException("Invalid source base palette color.");
        _terrain = terrain; _basePalette = basePalette.ToArray(); Reset();
    }
    public ushort PaletteColor(int bank, int index)
    {
        if (bank is <0 or >4 || index is <0 or >15) throw new ArgumentOutOfRangeException(nameof(bank));
        return _palette[bank * 16 + index];
    }
    public void Apply(SourceBackgroundAsset asset)
    {
        // Source LoadMoveBg writes map first, then tiles, then palette bank 2.
        asset.Tilemap.Span.CopyTo(_vram.AsSpan(TilemapAddress));
        asset.Tiles.Span.CopyTo(_vram);
        asset.Palette.Span.CopyTo(_palette.AsSpan(32));
    }
    public void Restore() => Apply(_terrain);
    /// <summary>New battle/cancel reset; normal restore reloads terrain without clearing remaining VRAM.</summary>
    public void Reset()
    {
        Array.Clear(_vram); Array.Clear(_palette); _basePalette.CopyTo(_palette, 0); Restore();
    }
    public SourceBackgroundPixel Sample(int x, int y, int screenSize = 1)
    {
        if (screenSize is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(screenSize));
        return SampleUnchecked(x, y, screenSize);
    }
    private SourceBackgroundPixel SampleUnchecked(int x, int y, int screenSize)
    {
        x &= screenSize == 1 ? 511 : 255; y &= 255;
        int mapIndex = (x >> 8) * 1024 + (y >> 3) * 32 + ((x >> 3) & 31);
        int entry = BinaryPrimitives.ReadUInt16LittleEndian(_vram.AsSpan(TilemapAddress + mapIndex * 2));
        int tx = x & 7, ty = y & 7;
        if ((entry & 0x400) != 0) tx = 7 - tx;
        if ((entry & 0x800) != 0) ty = 7 - ty;
        int packed = _vram[(entry & 1023) * 32 + ty * 4 + tx / 2];
        int index = (packed >> ((tx & 1) * 4)) & 15;
        // BG index zero is transparent regardless of its palette bank's color zero.
        return index == 0 ? new(0, 0) : new(entry >> 12, index);
    }
    public void Render(Span<ushort> destination, int width, int height, int scrollX = 0, int scrollY = 0,
        int screenSize = 1, int darkening = 0, ReadOnlySpan<SourcePaletteBlend> blends = default,
        ReadOnlySpan<int> inverted = default, ReadOnlySpan<ushort> fadedBank2 = default, Span<byte> layerMasks = default, ReadOnlySpan<SourcePaletteEntry> fadedEntries = default)
    {
        if (width is <1 or >512 || height is <1 or >256 || destination.Length != width * height || screenSize is not (0 or 1)
            || darkening is <0 or >16 || fadedBank2.Length is not (0 or 16) || !layerMasks.IsEmpty && layerMasks.Length != destination.Length) throw new ArgumentException("Source background render shape differs.");
        Span<ushort> colors = stackalloc ushort[80];
        for (int bank = 0; bank < 5; bank++)
        {
            SourcePaletteBlend? blend = null;
            foreach (var candidate in blends) if (candidate.Palette == bank) { blend = candidate; break; }
            bool invert = inverted.Contains(bank);
            for (int index = 0; index < 16; index++)
            {
                ushort color = bank == 2 && !fadedBank2.IsEmpty ? fadedBank2[index]
                    : SourcePaletteMath.Apply(_palette[bank * 16 + index], blend, invert);
                colors[bank * 16 + index] = Darken(color, darkening);
            }
        }
        foreach(var entry in fadedEntries)
        {if(entry.Index is <0 or >255)throw new InvalidDataException("Source BG palette entry differs.");if(entry.Index<colors.Length)colors[entry.Index]=Darken(entry.Color,darkening);}
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            var pixel = SampleUnchecked(x + scrollX, y + scrollY, screenSize);
            destination[y * width + x] = (ushort)(colors[pixel.Bank * 16 + pixel.Index] | 0x8000);
            if(!layerMasks.IsEmpty)layerMasks[y*width+x]=(byte)(pixel.Index==0?32:8);
        }
    }
    /// <summary>BLDCNT 0xE8 targets BG3 and backdrop. Hardware darkening rounds before subtraction.</summary>
    public static ushort Darken(ushort color, int coefficient)
    {
        if (coefficient is <0 or >16) throw new ArgumentOutOfRangeException(nameof(coefficient));
        int result = color & 0x8000;
        for (int shift = 0; shift <= 10; shift += 5)
        {
            int channel = (color >> shift) & 31;
            result |= (channel - ((channel * coefficient) >> 4)) << shift;
        }
        return (ushort)result;
    }
}
