using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Domain.Events;

public enum FameCheckerPickState { Hidden = 0, Silhouette = 1, Colored = 2 }

/// <summary>Original encoded text, including every color/font token, placeholder, page break and terminator.</summary>
public sealed record FameCheckerText(string Symbol, string SourcePath, int SourceLine, string EncodedText,
    ImmutableArray<string> ControlCodes)
{
    public string Expand(string playerName, string rivalName) => EncodedText
        .Replace("{PLAYER}", playerName, StringComparison.Ordinal).Replace("{RIVAL}", rivalName, StringComparison.Ordinal);
}

public sealed record FameCheckerFact(int Index, FameCheckerText Text, FameCheckerText OriginLocation,
    FameCheckerText OriginObjectName, string IconSymbol, int IconSourceId);
public sealed record FameCheckerPerson(string Id, string SourceSymbol, int SourceIndex, FameCheckerText MenuName,
    string TrainerSymbol, int? TrainerSourceId, string TrainerPictureSymbol, string? NonTrainerPictureSymbol,
    string UnusedGenderSymbol, FameCheckerText NameText, FameCheckerText CompletionText, ImmutableArray<FameCheckerFact> Facts);
public sealed record FameCheckerEventBinding(string SourceMap, string SourcePath, string EventGroup, int EventIndex,
    string ScriptLabel, string? Flag, int? X, int? Y);
public sealed record FameCheckerCollectionEvent(string SourcePath, string ScriptLabel, int SourceLine, int PersonIndex,
    int Value, string Function, ImmutableArray<string> Arguments, ImmutableArray<string> SourceMaps,
    ImmutableArray<FameCheckerEventBinding> DirectBindings, bool ExplicitlyUnused);

/// <summary>Complete immutable catalog with parsed person and fact indices.</summary>
public sealed class FameCheckerCatalog
{
    public const string Profile = "firered-fame-checker-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public ItemId Item => BillItemRules.FameCheckerId;
    public ImmutableArray<FameCheckerPerson> Persons { get; }
    public ImmutableArray<FameCheckerText> UiTexts { get; }
    public ImmutableArray<FameCheckerCollectionEvent> CollectionEvents { get; }
    private FameCheckerCatalog(Document doc)
    { Persons = doc.Persons; UiTexts = doc.UiTexts; CollectionEvents = doc.CollectionEvents; }

    public static FameCheckerCatalog Authenticate(ReadOnlySpan<byte> bytes)
    {
        var doc = JsonSerializer.Deserialize<Document>(bytes, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) ?? throw new InvalidDataException("fame_checker_catalog_empty");
        if (doc.SchemaVersion != 1 || doc.Profile != Profile || doc.SourceCommit != SourceCommit ||
            doc.Persons.IsDefault || doc.Persons.Length != FameCheckerRules.PersonCount ||
            doc.UiTexts.IsDefault || doc.CollectionEvents.IsDefault || doc.Persons.Any(p => p is null || p.Facts.IsDefault) ||
            !doc.Persons.Select(p => p.SourceIndex).SequenceEqual(Enumerable.Range(0, FameCheckerRules.PersonCount)) ||
            doc.Persons.Any(p => p.Facts.Length != FameCheckerRules.FactsPerPerson ||
                !p.Facts.Select(f => f.Index).SequenceEqual(Enumerable.Range(0, FameCheckerRules.FactsPerPerson))))
            throw new InvalidDataException("fame_checker_catalog_shape");
        return new(doc);
    }

    private sealed record Document(int SchemaVersion, string Profile, string SourceCommit,
        ImmutableDictionary<string, int> PickStates,
        ImmutableArray<FameCheckerPerson> Persons, ImmutableArray<FameCheckerText> UiTexts,
        ImmutableArray<FameCheckerCollectionEvent> CollectionEvents);
}

/// <summary>A locked panel deliberately carries no undiscovered text or NPC icon. Reading never changes saved state.</summary>
public sealed record FameCheckerFactView(int Index, bool Unlocked, FameCheckerFact? Fact);
public sealed record FameCheckerPersonView(FameCheckerPerson Person, FameCheckerPickState PickState,
    bool HasAnyFacts, bool HasAllFacts, FameCheckerText? PickText, ImmutableArray<FameCheckerFactView> Facts);
public sealed record FameCheckerMacroResult(StoryState State, string? Error)
{
    public bool Succeeded => Error is null;
}

public static class FameCheckerRules
{
    public const int PersonCount = 16;
    public const int FactsPerPerson = 6;
    public const int AllFactsMask = (1 << FactsPerPerson) - 1;
    // The original save reserves 12 flavor bits; only the low six are assigned by reachable source commands.
    public const int OriginalFlavorStorageBits = 12;
    public const string CollectFactFunction = "SetFlavorTextFlagFromSpecialVars";
    public const string SetPickStateFunction = "UpdatePickStateFromSpecialVar8005";
    public const string GiovanniGymTrainerSymbol = "TRAINER_LEADER_GIOVANNI";

    /// <summary>Equivalent to ResetFameChecker, represented in the existing sparse save dictionary.</summary>
    public static ImmutableSortedDictionary<int, FameEntry> Reset() =>
        ImmutableSortedDictionary<int, FameEntry>.Empty.Add(0, new((int)FameCheckerPickState.Colored, 0));

    /// <summary>Current reachable save schema: IDs 0..15, portrait states 0..2, six fact bits. No new read flags.</summary>
    public static string? Validate(IReadOnlyDictionary<int, FameEntry> entries) =>
        entries.Any(e => e.Key is < 0 or >= PersonCount || e.Value is null || e.Value.PickState is < 0 or > 2 ||
            e.Value.FlavorFlags is < 0 or > AllFactsMask) ? "fame_checker_state_invalid" : null;

    /// <summary>Original macro writes u16 special variables even when the subsequent special is a no-op.
    /// Facts can be collected before the Fame Checker item is acquired. Colored portraits never regress.</summary>
    public static FameCheckerMacroResult ApplyMacro(StoryState state, int person, int value,
        string function = CollectFactFunction)
    {
        if (function is not (CollectFactFunction or SetPickStateFunction)) return new(state, "fame_special_unsupported");
        int personVariable = unchecked((ushort)person), valueVariable = unchecked((ushort)value);
        var fame = state.Fame;
        if (personVariable < PersonCount)
        {
            var entry = fame.GetValueOrDefault(personVariable, new(0, 0));
            if (function == CollectFactFunction && valueVariable < FactsPerPerson)
            {
                entry = entry with { FlavorFlags = entry.FlavorFlags | (1 << valueVariable) };
                valueVariable = (int)FameCheckerPickState.Silhouette;
                if (entry.PickState != (int)FameCheckerPickState.Colored)
                    entry = entry with { PickState = valueVariable };
                fame = fame.SetItem(personVariable, entry);
            }
            else if (function == SetPickStateFunction && valueVariable is 1 or 2 &&
                !(valueVariable == (int)FameCheckerPickState.Silhouette && entry.PickState == (int)FameCheckerPickState.Colored))
                fame = fame.SetItem(personVariable, entry with { PickState = valueVariable });
        }
        return new(state with
        {
            Fame = fame,
            Variables = state.Variables.SetItem("VAR_0x8004", personVariable).SetItem("VAR_0x8005", valueVariable)
        }, null);
    }

    /// <summary>Only visible people, in source menu order. After his Gym battle Giovanni moves behind Blaine.
    /// Portrait mode displays an empty message for silhouettes, a name for incomplete colored people,
    /// and the source personal message for colored people with all six facts.</summary>
    public static ImmutableArray<FameCheckerPersonView> CreateView(FameCheckerCatalog catalog,
        IReadOnlyDictionary<int, FameEntry> entries, bool defeatedGiovanniInGym)
    {
        if (Validate(entries) is { } error) throw new ArgumentException(error, nameof(entries));
        var result = ImmutableArray.CreateBuilder<FameCheckerPersonView>();
        for (int menuIndex = 0; menuIndex < PersonCount; menuIndex++)
        {
            int id = defeatedGiovanniInGym ? menuIndex == 9 ? 15 : menuIndex > 9 ? menuIndex - 1 : menuIndex : menuIndex;
            var entry = entries.GetValueOrDefault(id, new(0, 0));
            if (entry.PickState == (int)FameCheckerPickState.Hidden) continue;
            var person = catalog.Persons[id];
            bool all = (entry.FlavorFlags & AllFactsMask) == AllFactsMask;
            var facts = person.Facts.Select(f => new FameCheckerFactView(f.Index,
                (entry.FlavorFlags & (1 << f.Index)) != 0,
                (entry.FlavorFlags & (1 << f.Index)) != 0 ? f : null)).ToImmutableArray();
            result.Add(new(person, (FameCheckerPickState)entry.PickState, (entry.FlavorFlags & AllFactsMask) != 0, all,
                entry.PickState == (int)FameCheckerPickState.Colored ? all ? person.CompletionText : person.NameText : null, facts));
        }
        return result.ToImmutable();
    }

    /// <summary>Original 3 × 2 fact selector; both vertical directions toggle row, horizontal wraps within row.</summary>
    public static int MoveFactSelection(int index, Game.Domain.World.Direction direction)
    {
        if (index is < 0 or >= FactsPerPerson) throw new ArgumentOutOfRangeException(nameof(index));
        return direction switch
        {
            Game.Domain.World.Direction.North or Game.Domain.World.Direction.South => (index + 3) % FactsPerPerson,
            Game.Domain.World.Direction.West => index / 3 * 3 + (index % 3 + 2) % 3,
            Game.Domain.World.Direction.East => index / 3 * 3 + (index % 3 + 1) % 3,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }
}
