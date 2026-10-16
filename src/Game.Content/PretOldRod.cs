using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record OldRodDocument(int SchemaVersion, ImmutableArray<OldRodEncounterTable> Tables, int CastDelayFrames, int DotDelayFrames,
    int FirstRoundMinimumDots, int FirstRoundMaximumDots, int BiteDrawBound, int BiteSuccessRoll,
    bool ReelReactionReachable, bool InFlightCancelSupported, int MinimumRoundDrawBound,
    int HeldItemDrawBound, string HeldItem, ImmutableDictionary<string, string> Texts);

/// <summary>Authenticates both source evidence and the exact bounded Old Rod output.</summary>
public sealed class PretOldRod
{
    public const string Profile = OldRodFishingRules.RulesVersion;
    public ImmutableDictionary<MapId, OldRodEncounterTable> Tables { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    private PretOldRod(ImmutableDictionary<MapId, OldRodEncounterTable> tables,ImmutableDictionary<string, string> texts)
    { Tables = tables; Texts = texts; }

    public static PretOldRod Load(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        byte[] Read(string path)
        {
            return File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
        }
        var document = ContentJson.ParseStrict<OldRodDocument>(Encoding.UTF8.GetString(Read("content/generated/data/services/old-rod.json")), ContentJson.CreateOptions());
                if (document.SchemaVersion != 1 ||
            document.Tables.IsDefault || document.Tables.Length != 10 || document.CastDelayFrames != 60 || document.DotDelayFrames != 20 ||
            document.FirstRoundMinimumDots != 4 || document.FirstRoundMaximumDots != 10 || document.BiteDrawBound != 2 || document.BiteSuccessRoll != 0 ||
            document.ReelReactionReachable || document.InFlightCancelSupported || document.MinimumRoundDrawBound != 1 ||
            document.HeldItemDrawBound != 100 || document.HeldItem != "ITEM_NONE")
            throw new InvalidDataException("Old Rod source scope differs.");
        foreach (string symbol in new[] { "gText_PokemonOnHook", "gText_NotEvenANibble" })
            if (!document.Texts.TryGetValue(symbol, out var text) || !text.EndsWith("{PAUSE_UNTIL_PRESS}", StringComparison.Ordinal))
                throw new InvalidDataException("Old Rod result text must preserve pause-for-button control.");
        foreach (var table in document.Tables) OldRodEncounterRules.ValidateTable(table);
                return new(document.Tables.ToImmutableDictionary(t => t.Map),document.Texts);
    }

}
