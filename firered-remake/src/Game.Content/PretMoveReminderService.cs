using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record MoveReminderDocument(int SchemaVersion, ImmutableDictionary<string, string> Texts);

/// <summary>Generated service text; field map admission is PretMoveReminderHouse.</summary>
public sealed class PretMoveReminderService
{
    public const string Profile = "firered-move-reminder-v1";
    public const string RelativePath = "content/generated/data/services/move-reminder.json";
    public MoveReminderCatalog Catalog { get; }
    public ImmutableDictionary<string, string> Texts => Catalog.Texts;

    private PretMoveReminderService(MoveReminderCatalog catalog) { Catalog = catalog; }

    public static PretMoveReminderService Load(string root, PokemonCatalog owned)
    {
        var document = ContentJson.ParseStrict<MoveReminderDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1)
            throw new InvalidDataException("move_reminder_catalog_scope");
        return new(MoveReminderCatalog.Authenticate(owned, document.Texts));
    }
}
