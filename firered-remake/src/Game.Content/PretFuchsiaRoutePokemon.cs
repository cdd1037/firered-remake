using System.Collections.Immutable;
using System.Security.Cryptography;
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
    public const string LockSha256 = "7a29f922014507b113be17b8cfa0e3c362d2472e8129d506e6ddfed11bdc61a2";
    public const string CatalogSha256 = "f5ce9810230cf1afca9d3b6fac92619c71ef42cd7c2c2c0ac553c1f1f32498a1";
    public const string TrainersSha256 = "e6b6128ef430bf343171d29ee8ac5cbde758d24f583b9300e944f4a295d95cf9";
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
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretFuchsiaRoutePokemon(PokemonCatalog catalog, FuchsiaRoutePokemonDocument document,
        FuchsiaRouteTrainerDocument trainers, FuchsiaRouteTrainerCatalog trainerCatalog,
        ImmutableArray<PretWorldSourceFile> files)
    {
        Catalog=catalog; Document=document; TrainerDocument=trainers; Trainers=trainerCatalog; SourceFiles=files;
        AdditionalSpeciesNames=document.SpeciesNames.ToImmutableSortedDictionary(n=>catalog.Species[n.Id].SourceSpeciesId,n=>n.Name);
        AdditionalMoveNames=document.MoveNames.ToImmutableDictionary(n=>n.Id,n=>n.Name);
        Constants=document.Constants.ToImmutableDictionary(c=>c.Symbol,c=>c.Value,StringComparer.Ordinal);
        EncodedNames=document.EncodedNames.ToImmutableDictionary(n=>n.Symbol,StringComparer.Ordinal);
        AdditionalSourceHashes=["fuchsia-route-pokemon-lock:"+LockSha256,"fuchsia-route-pokemon-catalog:"+CatalogSha256,
            "fuchsia-route-trainers:"+TrainersSha256,..files.Select(f=>"fuchsia-route-pokemon:"+f.Path+":"+f.Sha256)];
    }

    /// <summary>Strict103 predecessor: campaign241 becomes244 moves; standalone240 becomes243. Old records remain identical objects.</summary>
    public static PretFuchsiaRoutePokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot=null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        Require(baseCatalog.Species.Count==103 && baseCatalog.Moves.Count is 240 or 241,"Fuchsia requires exact103-species predecessor.");
        string root=Path.GetFullPath(projectRoot);
        var bytes=PretCeladonApproachSource.Read(root,CatalogRelativePath,CatalogSha256,46691);
        _=PretCeladonApproachSource.Read(root,"scripts/fuchsia-route-pokemon.lock.json",LockSha256,22167);
        var options=ContentJson.CreateOptions();options.Converters.Add(new ExactEnumConverter<GrowthRate>());options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc=ContentJson.ParseStrict<FuchsiaRoutePokemonDocument>(Utf8.GetString(bytes),options);
        Require(doc.SchemaVersion==1 && doc.Profile==Profile && doc.SourceCommit==SourceCommit && doc.Inputs.Length==93 &&
            doc.Species.Length==4 && doc.Moves.Length==3 && doc.SpeciesNames.Length==4 && doc.MoveNames.Length==3 &&
            doc.EncodedNames.Length==7 && doc.RequiredMoves.Length==17 && doc.HeldItems.Length==4 && doc.Machines.Length==4 &&
            doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty,"Fuchsia source document scope differs.");
        var files=ImmutableArray.CreateBuilder<PretWorldSourceFile>();var lineCounts=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var input in doc.Inputs)
        {
            var source=PretCeladonApproachSource.Read(root,"content/raw/fuchsia-route-pokemon/"+input.Path,input.Sha256,input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {source.Length}\0"),..source]))==input.GitBlobSha1,"Fuchsia source Git blob differs: "+input.Path);
            if(!input.Path.EndsWith(".png",StringComparison.Ordinal))lineCounts.Add(input.Path,Utf8.GetString(source).Count(c=>c=='\n')+1);
            files.Add(new(input.Path,input.GitBlobSha1,input.ByteLength,input.Sha256,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        void Source(PokemonSourceRef r)=>Require(doc.Inputs.Any(f=>f.Path==r.Path&&f.Sha256==r.Sha256)&&r.StartLine>0&&r.EndLine>=r.StartLine&&lineCounts.TryGetValue(r.Path,out int lines)&&r.EndLine<=lines&&!string.IsNullOrWhiteSpace(r.Symbol),"Fuchsia source reference differs.");
        void TrainerSource(TrainerSourceRef r)=>Source(new(r.Path,r.Symbol,r.StartLine,r.EndLine,r.Sha256));
        var species=baseCatalog.Species.ToBuilder();var moves=baseCatalog.Moves.ToBuilder();
        foreach(var s in doc.Species)
        {
            Require(PokemonFactory.ValidFuchsiaSpeciesDefinition(s),"Fuchsia exact source species differs.");Source(s.Source);Source(s.LearnsetSource);
            foreach(var l in s.LevelUpMoves)Source(l.Source);foreach(var e in s.Evolutions)Source(e.Source);
            species.Add(s.Id,s);
        }
        Require(doc.Moves.Select(m=>m.SourceMoveId).SequenceEqual([14,144,210]),"Fuchsia source move delta differs.");
        foreach(var m in doc.Moves){Source(m.Source);Require(!m.BattleEffectImplemented,"Metadata cannot admit effects.");moves.Add(m.Id,m);}
        Require(doc.RequiredMoves.Distinct().Count()==17&&doc.RequiredMoves.All(moves.ContainsKey),"Fuchsia natural move closure differs.");
        foreach(var n in doc.SpeciesNames)Source(n.Source);foreach(var n in doc.MoveNames)Source(n.Source);foreach(var c in doc.Constants)Source(c.Source);
        foreach(var n in doc.EncodedNames){Source(n.Source);Source(n.EncodingSource);Require(n.Bytes.Length==n.Name.Length+1&&n.Bytes[^1]==255,"Fuchsia name encoding differs.");}
        foreach(var h in doc.HeldItems)Source(h.Source);foreach(var m in doc.Machines)Source(m.Source);
        Require(doc.HeldItems.Single(h=>h.Species.Value=="pret:SPECIES_DITTO") is {Common:"ITEM_NONE",Rare:"ITEM_METAL_POWDER"},"Ditto rare item differs.");
        var catalog=new PokemonCatalog(SourceCommit,species.ToImmutable(),moves.ToImmutable());PokemonFactory.ValidateCatalog(catalog);
        Require(catalog.Species.Count==107&&catalog.Moves.Count==baseCatalog.Moves.Count+3&&baseCatalog.Species.All(p=>ReferenceEquals(p.Value,catalog.Species[p.Key]))&&baseCatalog.Moves.All(p=>ReferenceEquals(p.Value,catalog.Moves[p.Key])),"Fuchsia inherited records differ.");
        var tb=PretCeladonApproachSource.Read(root,TrainersRelativePath,TrainersSha256,179695);
        var td=ContentJson.ParseStrict<FuchsiaRouteTrainerDocument>(Utf8.GetString(tb),ContentJson.CreateOptions());
        Require(td.SchemaVersion==1&&td.Profile==FuchsiaRouteTrainerPartyFactory.RulesVersion&&td.SourceCommit==SourceCommit&&!td.CompleteSourceRoster&&td.Trainers.Length==40&&td.Battles.Length==43&&td.Actors.GetArrayLength()==43&&td.ResolvedMoves.GetArrayLength()==40,"Fuchsia trainer document scope differs.");
        var initial=ImmutableDictionary.CreateBuilder<TrainerId,ImmutableArray<ImmutableArray<MoveId>>>();
        foreach(var t in td.Trainers){TrainerSource(t.Source);TrainerSource(t.MoneySource);foreach(var m in t.Party){TrainerSource(m.Source);TrainerSource(m.SpeciesNameSource);}}
        foreach(var b in td.Battles)TrainerSource(b.Source);
        foreach(var r in td.ResolvedMoves.EnumerateArray())
        {
            var id=new TrainerId(r.GetProperty("trainer").GetProperty("value").GetString()!);var slots=ImmutableArray.CreateBuilder<ImmutableArray<MoveId>>();
            foreach(var member in r.GetProperty("party").EnumerateArray())
            {
                TrainerSource(ContentJson.ParseStrict<TrainerSourceRef>(member.GetProperty("learnsetSource").GetRawText(),ContentJson.CreateOptions()));
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
