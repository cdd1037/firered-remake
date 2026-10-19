using System.Buffers.Binary;
using System.Collections.Immutable;
using Game.Domain.Events;

namespace Game.Content;

public sealed record DiplomaAsset(string Id, int Width, int Height, string TexturePath);
public sealed record DiplomaDocument(int SchemaVersion, ImmutableArray<DiplomaAsset> Assets,
    string PlayerTemplate, string BodyTemplate, string Kanto, string National, string Signature,
    int CenterX, int PlayerY, int BodyY, int LineHeight, int SignatureX, int SignatureY,
    int FadeFrames, string Fanfare, int FanfareCounterFrames);
public sealed record DiplomaTextLine(string Text, int X, int Y);

/// <summary>Generated certificate artwork, original text and layout. No raw inputs are read at runtime.</summary>
public sealed class PretDiplomaPresentation
{
    public const string Base = "content/generated/ui/diploma/";
    private readonly ImmutableDictionary<string, byte[]> _textures;
    public DiplomaDocument Document { get; }

    private PretDiplomaPresentation(DiplomaDocument document, ImmutableDictionary<string, byte[]> textures)
        => (Document, _textures) = (document, textures);

    public static PretDiplomaPresentation Load(string root, IReadOnlyDictionary<string, string>? sourceTexts = null)
    {
        var document = ContentJson.ParseStrict<DiplomaDocument>(GeneratedResourceFile.Read(root, Base + "diploma.json"), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.Assets.IsDefaultOrEmpty ||
            document.Assets.Length != 2 || !document.Assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(["kanto", "national"]))
            throw new InvalidDataException("Diploma schema or certificate variants invalid.");
        if (document.CenterX is < 0 or >= 240 || document.PlayerY is < 0 or >= 160 ||
            document.BodyY < document.PlayerY || document.LineHeight <= 0 ||
            document.SignatureX is < 0 or >= 240 || document.SignatureY is < 0 or >= 160 ||
            document.FadeFrames is <= 0 or > 60 || document.FanfareCounterFrames is <= 0 or > 3600 ||
            document.Fanfare != "MUS_OBTAIN_BADGE")
            throw new InvalidDataException("Diploma layout or transition metadata invalid.");
        if (!Template(document.PlayerTemplate, "{PLAYER}") || !Template(document.BodyTemplate, "{DEX}") ||
            document.BodyTemplate.Split('\n').Length != 4 ||
            document.BodyY + 3 * document.LineHeight >= document.SignatureY ||
            !Literal(document.Kanto) || !Literal(document.National) || !Literal(document.Signature))
            throw new InvalidDataException("Diploma text or placeholders invalid.");
        var textures = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var asset in document.Assets)
        {
            if (asset.Width != 240 || asset.Height != 160 || asset.TexturePath != asset.Id + ".png")
                throw new InvalidDataException("Diploma artwork geometry or path invalid.");
            var bytes = GeneratedResourceFile.Read(root, Base + asset.TexturePath);
            if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
                BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != asset.Width ||
                BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) != asset.Height)
                throw new InvalidDataException("Diploma PNG header invalid.");
            textures.Add(asset.Id, bytes);
        }
        sourceTexts ??= PretBattleText.Load(root);
        // Keep the certificate's existing layout contract; convert source dynamics before binding saved names.
        var values = new Dictionary<string, string> { ["DYNAMIC 0x00"] = "{PLAYER}", ["DYNAMIC 0x01"] = "{DEX}" };
        string Text(string symbol) => SourceBattleText.Pages(sourceTexts, symbol, values).Single();
        document = document with { PlayerTemplate = Text("gText_Diploma_Player"),
            BodyTemplate = Text("gText_Diploma_ThisDocument"), Kanto = Text("gText_Diploma_Kanto"),
            National = Text("gText_Diploma_National"), Signature = Text("gText_Diploma_GameFreak") };
        return new(document, textures.ToImmutable());
    }

    private static bool Literal(string value) => !string.IsNullOrWhiteSpace(value) &&
        !value.Any(c => c is '{' or '}' || char.IsControl(c));
    private static bool Template(string value, string placeholder) => !string.IsNullOrWhiteSpace(value) &&
        value.Contains(placeholder, StringComparison.Ordinal) &&
        !value.Replace(placeholder, "", StringComparison.Ordinal).Any(c => c is '{' or '}' || char.IsControl(c) && c != '\n');

    public byte[] ReadTexture(bool national) => _textures[national ? "national" : "kanto"].ToArray();

    /// <summary>The original centers the whole multiline block using its longest row, then prints left-aligned rows.</summary>
    public ImmutableArray<DiplomaTextLine> TextLines(string playerName, bool national, Func<string, int> measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (!Literal(playerName)) throw new ArgumentException("Diploma player name invalid.", nameof(playerName));
        string player = Document.PlayerTemplate.Replace("{PLAYER}", playerName, StringComparison.Ordinal);
        var body = Document.BodyTemplate.Replace("{DEX}", national ? Document.National : Document.Kanto, StringComparison.Ordinal).Split('\n');
        int bodyX = Document.CenterX - body.Max(measure) / 2;
        var result = ImmutableArray.CreateBuilder<DiplomaTextLine>();
        result.Add(new(player, Document.CenterX - measure(player) / 2, Document.PlayerY));
        for (int i = 0; i < body.Length; i++) result.Add(new(body[i], bodyX, Document.BodyY + i * Document.LineHeight));
        result.Add(new(Document.Signature, Document.SignatureX, Document.SignatureY));
        return result.ToImmutable();
    }
}
