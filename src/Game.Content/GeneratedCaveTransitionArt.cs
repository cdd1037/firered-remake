using System.Collections.Immutable;

namespace Game.Content;

/// <summary>Immutable cave-transition indices and RGB555 palette banks.</summary>
public sealed class GeneratedCaveTransitionArt
{
    private sealed record Document(int SchemaVersion, int Width, int Height, string Indices4Bpp,
        ImmutableArray<ushort> TransitionPalette, ImmutableArray<ushort> WhitePalette, ImmutableArray<ushort> BlackPalette);

    public int Width { get; }
    public int Height { get; }
    /// <summary>Row-major 4bpp indices. Zero selects the backdrop, not bank 14 color zero.</summary>
    public ImmutableArray<byte> Indices { get; }
    public ImmutableArray<ushort> TransitionPalette { get; }
    public ImmutableArray<ushort> WhitePalette { get; }
    public ImmutableArray<ushort> BlackPalette { get; }

    private GeneratedCaveTransitionArt(Document document, ImmutableArray<byte> indices)
    {
        Width = document.Width;
        Height = document.Height;
        Indices = indices;
        TransitionPalette = document.TransitionPalette;
        WhitePalette = document.WhitePalette;
        BlackPalette = document.BlackPalette;
    }

    public static GeneratedCaveTransitionArt Load(string generatedRoot)
    {
        var document = ContentJson.ParseStrict<Document>(
            GeneratedResourceFile.Read(generatedRoot, "maps/cave-transition.json"), ContentJson.CreateOptions());
        static bool ValidPalette(ImmutableArray<ushort> values) =>
            !values.IsDefault && values.Length == 16 && values.All(value => value <= 0x7fff);
        if (document.SchemaVersion != 1 || document.Width != 256 || document.Height != 256 ||
            !ValidPalette(document.TransitionPalette) || !ValidPalette(document.WhitePalette) || !ValidPalette(document.BlackPalette) ||
            document.Indices4Bpp.Length != ((256 * 256 / 2 + 2) / 3) * 4)
            throw new InvalidDataException("Invalid generated cave transition art.");
        byte[] packed;
        try { packed = Convert.FromBase64String(document.Indices4Bpp); }
        catch (FormatException error) { throw new InvalidDataException("Invalid cave transition index encoding.", error); }
        if (packed.Length != 256 * 256 / 2 || Convert.ToBase64String(packed) != document.Indices4Bpp)
            throw new InvalidDataException("Incomplete or noncanonical cave transition indices.");
        var indices = ImmutableArray.CreateBuilder<byte>(document.Width * document.Height);
        foreach (byte value in packed)
        {
            indices.Add((byte)(value & 15));
            indices.Add((byte)(value >> 4));
        }
        return new(document, indices.MoveToImmutable());
    }
}
