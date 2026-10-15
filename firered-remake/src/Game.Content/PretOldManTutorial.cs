using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record OldManTutorialInput(string Path);
public sealed record OldManTutorialAsset(string Path, int Width, int Height);
public sealed record OldManTutorialText(string Symbol, string Text, PokemonSourceRef Source);
public sealed record OldManThrowFrame(int Frame, int Ticks);
public sealed record OldManTeachyTv(ItemId Item, string Description);
public sealed record OldManTutorialDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<OldManTutorialInput> Inputs, ImmutableArray<string> Limitations,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PokemonMoveDefinition> Moves,
    ImmutableArray<PretRoute1SpeciesName> SpeciesNames, ImmutableArray<PretRoute1MoveName> MoveNames,
    ImmutableArray<OldManTutorialText> Texts, OldManTeachyTv TeachyTv, ImmutableArray<OldManTutorialAsset> Assets,
    ImmutableArray<OldManThrowFrame> OldManThrowFrames, PokemonSourceRef OldManThrowSource,
    ImmutableArray<OldManTutorialText> Layouts);
public sealed record PretOldManTutorialContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames,
    ImmutableDictionary<string, OldManTutorialText> Texts, ImmutableArray<OldManTutorialAsset> Assets,
    OldManTeachyTv TeachyTv, ImmutableArray<OldManThrowFrame> OldManThrowFrames,
    PokemonSourceRef OldManThrowSource, ImmutableArray<OldManTutorialText> Layouts);

/// <summary>Separately authenticated complete Weedle-family source closure. The original 14 species remain unmodified.</summary>
public static class PretOldManTutorial
{
    public const string Profile = "firered-old-man-tutorial-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static PretOldManTutorialContent LoadContent(PretCanonicalPokemonCatalog canonical, string route1GeneratedJson, string tutorialGeneratedJson)
    {
        // Authenticate before composing. No caller-supplied manifest can legitimize changed source values.
        var original = PretRoute1PokemonCatalog.LoadContent(canonical, route1GeneratedJson);
        var options = canonical.ReferenceOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        var doc = ContentJson.ParseStrict<OldManTutorialDocument>(tutorialGeneratedJson, options);
        if (doc.SchemaVersion != 1 || doc.Species.Length != 3 || doc.SpeciesNames.Length != 3 ||
            doc.Texts.Length != 4 || doc.Assets.Length != 7 || doc.Limitations.IsDefaultOrEmpty)
            throw Error("profile", "Incomplete or unsupported tutorial source closure.");
        var species = original.Catalog.Species.ToBuilder();
        foreach (var value in doc.Species) species.Add(value.Id, value);
        var moves = original.Catalog.Moves.ToBuilder();
        foreach (var value in doc.Moves)
        {
            if (!moves.ContainsKey(value.Id)) moves.Add(value.Id, value);
        }
        var names = original.SpeciesNames.ToBuilder();
        foreach (var value in doc.SpeciesNames)
            names.Add(species[value.Id].SourceSpeciesId, value.Name);
        var moveNames = original.MoveNames.ToBuilder();
        foreach (var value in doc.MoveNames)
        {
            if (moveNames.TryGetValue(value.Id, out string? existing) && existing != value.Name)
                throw Error("name_collision", "Existing move name differs.");
            moveNames[value.Id] = value.Name;
        }
        var catalog = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        PokemonFactory.ValidateDefinitions(catalog);
        if (catalog.Species.Count != 17 || catalog.Species[new("pret:SPECIES_WEEDLE")].SourceSpeciesId != 13 ||
            catalog.Species[new("pret:SPECIES_KAKUNA")].SourceSpeciesId != 14 ||
            catalog.Species[new("pret:SPECIES_BEEDRILL")].SourceSpeciesId != 15)
            throw Error("species", "Expected original fourteen species and the complete Weedle family.");
        return new(catalog, names.ToImmutable(), moveNames.ToImmutable(), doc.Texts.ToImmutableDictionary(x => x.Symbol),
            doc.Assets, doc.TeachyTv, doc.OldManThrowFrames, doc.OldManThrowSource, doc.Layouts);
    }

    private static ContentValidationException Error(string code, string message) =>
        ContentJson.Error("old_man_tutorial_" + code, "$", message);
}
