using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

public sealed record IntroMovieTexture(string Name, int Width, int Height);
/// <summary>Generated-only compact layers. Source format and import provenance stay in tooling.</summary>
public sealed class PretIntroMoviePresentation
{
    private readonly string directory;
    public ImmutableArray<IntroMovieTexture> Textures { get; }
    private PretIntroMoviePresentation(string directory, ImmutableArray<IntroMovieTexture> textures)
    { this.directory = directory; Textures = textures; }
    public static PretIntroMoviePresentation Load(string root)
    {
        string directory = Path.Combine(root, "content/generated/ui/intro-movie");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "intro-movie.json")));
        if (doc.RootElement.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Intro movie schema differs.");
        var textures = doc.RootElement.GetProperty("textures").Deserialize<ImmutableArray<IntroMovieTexture>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (textures.IsDefaultOrEmpty || textures.Any(t => string.IsNullOrWhiteSpace(t.Name) || t.Name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || t.Width <= 0 || t.Height <= 0 || t.Width > 512 || t.Height > 512) || textures.Select(t => t.Name).Distinct().Count() != textures.Length)
            throw new InvalidDataException("Intro movie texture definitions invalid.");
        foreach (string required in new[] { "copyright", "gf-bg", "gf-name", "gf-logo", "gf-presents", "star", "sparkles-small", "sparkles-big", "grass-bg", "grass-front", "wide-bg", "wide-plants", "wide-gengar", "wide-nidorino", "close-gengar", "close-nidorino", "fight-bg", "fight-gengar", "nidorino", "grass", "swipe", "dust", "gengar-static" })
            if (!textures.Any(t => t.Name == required) || !File.Exists(Path.Combine(directory, required + ".png"))) throw new InvalidDataException("Intro movie layer missing: " + required);
        return new(directory, textures);
    }
    public byte[] ReadTexture(string name)
    {
        if (!Textures.Any(t => t.Name == name)) throw new ArgumentOutOfRangeException(nameof(name));
        return File.ReadAllBytes(Path.Combine(directory, name + ".png"));
    }
}
