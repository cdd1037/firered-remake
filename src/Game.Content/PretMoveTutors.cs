using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record MoveTutorDocument(int SchemaVersion, ImmutableArray<MoveTutorMove> Moves, ImmutableArray<MoveTutorCompatibility> Compatibility,
    ImmutableDictionary<string, string> Texts);

public sealed class PretMoveTutors
{
    public const string RelativePath = "content/generated/data/services/move-tutors.json";
    public MoveTutorCatalog Catalog { get; }
    private PretMoveTutors(MoveTutorCatalog catalog) { Catalog = catalog; }

    public static PretMoveTutors Load(string root, PokemonCatalog owned, IReadOnlyDictionary<string, string>? sourceTexts = null)
    {
        var document = ContentJson.ParseStrict<MoveTutorDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1)
            throw new InvalidDataException("move_tutor_catalog_scope");
        sourceTexts ??= PretBattleText.Load(root);
        var texts = document.Texts.Keys.ToImmutableDictionary(symbol => symbol, symbol => sourceTexts[symbol], StringComparer.Ordinal);
        return new(MoveTutorCatalog.Authenticate(owned, document.Moves, document.Compatibility, texts));
    }
}
