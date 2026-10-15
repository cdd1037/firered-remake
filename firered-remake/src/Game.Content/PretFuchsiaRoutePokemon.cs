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
public sealed record FuchsiaRoutePokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MoveId> RequiredMoves,
    int? NaturalLevelLimit, ImmutableArray<FuchsiaRouteHeldItems> HeldItems,
    ImmutableArray<FuchsiaRouteMachineRow> Machines, ImmutableArray<string> Limitations);
public sealed record FuchsiaRouteTrainerDocument(int SchemaVersion, string Profile, string SourceCommit,
    bool CompleteSourceRoster, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement Actors,
    JsonElement ResolvedMoves, ImmutableArray<string> Limitations);

/// <summary>Four exact source species and three missing natural move definitions. Metadata never grants battle execution.</summary>
public sealed class PretFuchsiaRoutePokemon
{
    public const string Profile = "firered-fuchsia-route-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogRelativePath = "content/generated/fuchsia-route-pokemon/catalog.json";
    public const string TrainersRelativePath = "content/generated/fuchsia-route-pokemon/trainers.json";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public FuchsiaRoutePokemonDocument Document { get; }
    public FuchsiaRouteTrainerDocument TrainerDocument { get; }
    public FuchsiaRouteTrainerCatalog Trainers { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }

    private PretFuchsiaRoutePokemon(PokemonCatalog catalog, FuchsiaRoutePokemonDocument document,
        FuchsiaRouteTrainerDocument trainers, FuchsiaRouteTrainerCatalog trainerCatalog,
        ImmutableArray<PretWorldSourceFile> files)
    {
        Catalog=catalog; Document=document; TrainerDocument=trainers; Trainers=trainerCatalog; SourceFiles=files;
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
        var files = doc.Inputs.Select(input => new PretWorldSourceFile(input.Path)).ToImmutableArray();
        var tb=PretCeladonApproachSource.Read(projectRoot,TrainersRelativePath);
        var td=ContentJson.ParseStrict<FuchsiaRouteTrainerDocument>(Utf8.GetString(tb),ContentJson.CreateOptions());
        Require(td.SchemaVersion==1&&!td.CompleteSourceRoster&&td.Actors.GetArrayLength()==43&&td.ResolvedMoves.GetArrayLength()==40,"Fuchsia trainer document scope differs.");
        var initial=ImmutableDictionary.CreateBuilder<TrainerId,ImmutableArray<ImmutableArray<MoveId>>>();
        foreach(var r in td.ResolvedMoves.EnumerateArray())
        {
            var id=new TrainerId(r.GetProperty("trainer").GetProperty("value").GetString()!);var slots=ImmutableArray.CreateBuilder<ImmutableArray<MoveId>>();
            foreach(var member in r.GetProperty("party").EnumerateArray())
            {
                slots.Add(member.GetProperty("moves").EnumerateArray().Select(m=>new MoveId(m.GetProperty("value").GetString()!)).ToImmutableArray());
            }
            initial.Add(id,slots.ToImmutable());
        }
        var tc=new FuchsiaRouteTrainerCatalog(SourceCommit,td.Profile,false,td.Trainers.ToImmutableDictionary(t=>t.Id),td.Battles.ToImmutableDictionary(b=>b.ScriptEntry,StringComparer.Ordinal),initial.ToImmutable());
        FuchsiaRouteTrainerPartyFactory.ValidateCatalog(tc);
        return new(catalog, doc, td, tc, files);
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
