using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FuchsiaRouteHeldItems(SpeciesId Species, string Common, string Rare);
public sealed record FuchsiaRouteMachineRow(SpeciesId Species, ImmutableArray<string> MachineSymbols);
public sealed record FuchsiaRoutePokemonDocument(int SchemaVersion, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MoveId> RequiredMoves,
    int? NaturalLevelLimit, ImmutableArray<FuchsiaRouteHeldItems> HeldItems,
    ImmutableArray<FuchsiaRouteMachineRow> Machines);
public sealed record FuchsiaRouteTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement Actors);

/// <summary>Four exact source species and three missing natural move definitions. Metadata never grants battle execution.</summary>
public sealed class PretFuchsiaRoutePokemon
{
    public const string Profile = "firered-fuchsia-route-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogRelativePath = "content/generated/data/pokemon/fuchsia-route/catalog.json";
    public const string TrainersRelativePath = "content/generated/data/pokemon/fuchsia-route/trainers.json";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public FuchsiaRoutePokemonDocument Document { get; }
    public FuchsiaRouteTrainerDocument TrainerDocument { get; }
    public FuchsiaRouteTrainerCatalog Trainers { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }

    private PretFuchsiaRoutePokemon(PokemonCatalog catalog, FuchsiaRoutePokemonDocument document,
        FuchsiaRouteTrainerDocument trainers, FuchsiaRouteTrainerCatalog trainerCatalog)
    {
        Catalog=catalog; Document=document; TrainerDocument=trainers; Trainers=trainerCatalog;
        AdditionalSpeciesNames=document.SpeciesNames.ToImmutableSortedDictionary(n=>catalog.Species[n.Id].SourceSpeciesId,n=>n.Name);
        AdditionalMoveNames=document.MoveNames.ToImmutableDictionary(n=>n.Id,n=>n.Name);
        Constants=document.Constants.ToImmutableDictionary(c=>c.Symbol,c=>c.Value,StringComparer.Ordinal);
        EncodedNames=document.EncodedNames.ToImmutableDictionary(n=>n.Symbol,StringComparer.Ordinal);
    }

    /// <summary>Resolve regional presentation against the current admitted catalog.</summary>
    public static PretFuchsiaRoutePokemon Load(PretCanonicalPokemonCatalog canonical, PokemonCatalog catalog, string projectRoot)
    {
        var doc = SafariSource.Parse<FuchsiaRoutePokemonDocument>(SafariSource.Read(projectRoot, CatalogRelativePath), canonical: canonical);
        SafariSource.Require(doc.SchemaVersion == 1 && !doc.Species.IsDefaultOrEmpty &&
            doc.Species.All(species => catalog.Species.ContainsKey(species.Id)) &&
            doc.RequiredMoves.All(catalog.Moves.ContainsKey), "FuchsiaRoute presentation references are not admitted.");
        foreach (var name in doc.EncodedNames)
            SafariSource.Require(!name.Bytes.IsDefaultOrEmpty && name.Bytes[^1] == 255,
                "FuchsiaRoute encoded name is unterminated.");
        var tb=PretCeladonApproachSource.Read(projectRoot,TrainersRelativePath);
        var td=ContentJson.ParseStrict<FuchsiaRouteTrainerDocument>(Utf8.GetString(tb),SharedTrainerCatalog.Options(projectRoot));
        Require(td.SchemaVersion == 1, "Unsupported Fuchsia Route trainer document schema.");
        var tc=new FuchsiaRouteTrainerCatalog(td.Trainers.ToImmutableDictionary(t=>t.Id),td.Battles.ToImmutableDictionary(b=>b.ScriptEntry,StringComparer.Ordinal));
        FuchsiaRouteTrainerPartyFactory.ValidateCatalog(tc);
        return new(catalog, doc, td, tc);
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
