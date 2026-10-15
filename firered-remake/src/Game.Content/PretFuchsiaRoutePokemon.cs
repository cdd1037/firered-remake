using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FuchsiaRouteHeldItems(SpeciesId Species, string Common, string Rare, PokemonSourceRef Source);
public sealed record FuchsiaRouteMachineRow(SpeciesId Species, ImmutableArray<string> MachineSymbols, PokemonSourceRef Source);
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

    /// <summary>Strict103 predecessor: campaign241 becomes244 moves; standalone240 becomes243. Old records remain identical objects.</summary>
    public static PretFuchsiaRoutePokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot=null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        Require(baseCatalog.Species.Count==103 && baseCatalog.Moves.Count is 240 or 241,"Fuchsia requires exact103-species predecessor.");
        string root=Path.GetFullPath(projectRoot);
        var bytes=PretCeladonApproachSource.Read(root,CatalogRelativePath);
        var options=ContentJson.CreateOptions();options.Converters.Add(new ExactEnumConverter<GrowthRate>());options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc=ContentJson.ParseStrict<FuchsiaRoutePokemonDocument>(Utf8.GetString(bytes),options);
        Require(doc.SchemaVersion==1 && doc.RequiredMoves.Length==17 && doc.HeldItems.Length==4 && doc.Machines.Length==4 &&
            doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty,"Fuchsia source document scope differs.");
        var files=ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach(var input in doc.Inputs)
        {
            files.Add(new(input.Path));
        }
        var species=baseCatalog.Species.ToBuilder();var moves=baseCatalog.Moves.ToBuilder();
        foreach(var s in doc.Species)
        {
            Require(PokemonFactory.ValidFuchsiaSpeciesDefinition(s),"Fuchsia exact source species differs.");            species.Add(s.Id,s);
        }
        Require(doc.Moves.Select(m=>m.SourceMoveId).SequenceEqual([14,144,210]),"Fuchsia source move delta differs.");
        foreach(var m in doc.Moves){Require(!m.BattleEffectImplemented,"Metadata cannot admit effects.");moves.Add(m.Id,m);}
        Require(doc.RequiredMoves.Distinct().Count()==17&&doc.RequiredMoves.All(moves.ContainsKey),"Fuchsia natural move closure differs.");
        foreach(var n in doc.EncodedNames){Require(n.Bytes.Length==n.Name.Length+1&&n.Bytes[^1]==255,"Fuchsia name encoding differs.");}
        Require(doc.HeldItems.Single(h=>h.Species.Value=="pret:SPECIES_DITTO") is {Common:"ITEM_NONE",Rare:"ITEM_METAL_POWDER"},"Ditto rare item differs.");
        var catalog=new PokemonCatalog(SourceCommit,species.ToImmutable(),moves.ToImmutable());PokemonFactory.ValidateCatalog(catalog);
        Require(catalog.Species.Count==107&&catalog.Moves.Count==baseCatalog.Moves.Count+3,"Fuchsia inherited records differ.");
        var tb=PretCeladonApproachSource.Read(root,TrainersRelativePath);
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
        return new(catalog,doc,td,tc,files.ToImmutable());
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
