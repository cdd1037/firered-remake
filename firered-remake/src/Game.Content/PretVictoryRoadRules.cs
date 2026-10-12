using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record VictoryRoadTrainerDocument(string SourceCommit,string Profile,bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,JsonElement ResolvedMoves);
public sealed record VictoryRoadFishingTable(MapId Map,string SourceMapSymbol,string SourceCommit,string SourcePath,
    string SourceSymbol,string SourceSha256,int RawEncounterRate,ImmutableArray<OldRodEncounterSlot> Slots,
    ImmutableDictionary<string,ImmutableArray<int>> RodGroups);
public sealed record VictoryRoadWildSpeciesSource(SpeciesId Species,int SourceSpeciesId,string Ability1,string Ability2,
    string ItemCommon,string ItemRare,OpeningItemSourceRef Source,OpeningItemSourceRef LearnsetSource);
public sealed record VictoryRoadWildDocument(string SourceCommit,string Profile,ImmutableArray<EarlyLandEncounterTable> Land,
    ImmutableArray<WaterEncounterTable> Water,ImmutableArray<VictoryRoadFishingTable> Fishing,
    ImmutableArray<VictoryRoadInitialMoves> InitialMoves,ImmutableArray<VictoryRoadWildSpeciesSource> Species,
    ImmutableArray<SpeciesId> OwnedSpeciesAdded,ImmutableArray<string> Limitations);

/// <summary>Authenticated Victory Road rules without new ownership or an Application profile.</summary>
public static class PretVictoryRoadRules
{
    public const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree="7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256="377641036a2e78c7c7c8fbe74820814f59bd988b6526513f93fb50da3f964dc7";
    public const string TrainersSha256="4cfac795d1af3de34cda10f50a4c012e6d1df4e3ecb48a3a1b38fcf41712f752";
    public const string WildSha256="40ad72030b49658741d3d4916ae0863780a11fe4bdf457a21d151159ca176db4";
    public static VictoryRoadTrainerDocument LoadTrainerDocument(string root)
    {
        ValidateSourceEvidence(root);
        var d=Parse<VictoryRoadTrainerDocument>(Read(root,"content/generated/victory-road-rules/trainers.json",TrainersSha256));
        _=Catalog(d);return d;
    }
    public static VictoryRoadTrainerCatalog LoadTrainers(string root)=>Catalog(LoadTrainerDocument(root));
    private static VictoryRoadTrainerCatalog Catalog(VictoryRoadTrainerDocument d)
    {
        var c=new VictoryRoadTrainerCatalog(d.SourceCommit,d.Profile,d.CompleteSourceRoster,
            d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.ScriptEntry,StringComparer.Ordinal));
        VictoryRoadTrainerPartyFactory.ValidateCatalog(c);return c;
    }
    public static VictoryRoadWildDocument LoadWild(string root)
    {
        ValidateSourceEvidence(root);
        var d=Parse<VictoryRoadWildDocument>(Read(root,"content/generated/victory-road-rules/wild.json",WildSha256));
        Need(d.SourceCommit==SourceCommit && d.Profile=="firered-victory-road-wild-v1" && d.Land.Length==4 && d.Water.Length==1 &&
            d.Fishing.Length==1 && d.Fishing[0].Slots.Length==10 && d.InitialMoves.Length==122 && d.Species.Length==19 &&
            d.OwnedSpeciesAdded.IsEmpty,"Victory Road wild source scope differs.");
        foreach(var t in d.Land)
        {
            var expected=VictoryRoadEncounterTables.Land.Single(t2=>t2.Map==t.Map);
            Need(t with {Slots=expected.Slots}==expected && t.Slots.SequenceEqual(expected.Slots),"Victory Road land differs.");
        }
        foreach(var t in d.Water)
        {
            var expected=VictoryRoadEncounterTables.Water.Single(t2=>t2.Map==t.Map);
            Need(t with {Slots=expected.Slots}==expected && t.Slots.SequenceEqual(expected.Slots),"Victory Road water differs.");
        }
        var fish=d.Fishing[0];var f=VictoryRoadEncounterTables.FishingSource.Single();
        Need(new OldRodEncounterTable(fish.Map,fish.SourceCommit,fish.SourcePath,fish.SourceSymbol,fish.SourceSha256,fish.RawEncounterRate,f.Slots)==f &&
            fish.Slots.SequenceEqual(f.Slots) && fish.RodGroups.Count==3 && fish.RodGroups["old_rod"].SequenceEqual([0,1]) &&
            fish.RodGroups["good_rod"].SequenceEqual([2,3,4]) && fish.RodGroups["super_rod"].SequenceEqual([5,6,7,8,9]),"Victory Road fishing declarations differ.");
        for(int i=0;i<d.InitialMoves.Length;i++)
        {
            var expected=VictoryRoadWildSource.InitialMoves[i];var row=d.InitialMoves[i];
            Need(row.Species==expected.Species && row.Level==expected.Level && row.Moves.SequenceEqual(expected.Moves),"Victory Road initial moves differ.");
        }
        for(int i=0;i<d.Species.Length;i++)
        {
            var row=d.Species[i];var expected=VictoryRoadWildSource.Species[i];
            Need(new VictoryRoadWildDependency(row.Species,row.SourceSpeciesId,row.Ability1,row.Ability2,row.ItemCommon,row.ItemRare)==expected,"Victory Road wild dependency differs.");
        }
        return d;
    }
    public static VictoryRoadWaterEncounterCatalog ExtendWater(FirstSeviiWaterEncounterCatalog predecessor,string root)=>
        VictoryRoadWaterEncounterCatalog.Authenticate(predecessor,LoadWild(root).Water);
    public static ImmutableArray<string> ValidateSourceEvidence(string root)
    {
        var source=Parse<SourceLock>(Read(root,"scripts/victory-road-rules.lock.json",LockSha256));
        Need(source.SourceCommit==SourceCommit && source.SourceTree==SourceTree && source.Profile=="firered-victory-road-rules-v1" &&
            source.Files.Length==40 && source.Files.Select(f=>f.Path).Distinct().Count()==40,"Victory Road lock scope differs.");
        foreach(var f in source.Files)Need(Read(root,f.Path,f.Sha256).Length==f.ByteLength,"Victory Road source length differs: "+f.Path);
        var inputs=Parse<InputDocument>(Read(root,"content/generated/victory-road-rules/source-input-hashes.json",
            source.Files.Single(f=>f.Path=="content/generated/victory-road-rules/source-input-hashes.json").Sha256));
        Need(inputs.SourceCommit==SourceCommit && inputs.SourceTree==SourceTree && inputs.Files.Length==34,"Victory Road raw input scope differs.");
        foreach(var file in inputs.Files)
        {
            var bytes=Read(root,"content/raw/victory-road-rules/"+file.Path,file.Sha256);
            Need(bytes.Length==file.ByteLength && Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"),..bytes]))==file.GitBlobSha1,"Victory Road Git blob differs.");
        }
        return ["victory-road-rules-lock:"+LockSha256,..source.Files.Select(f=>"victory-road-rules:"+f.Path+":"+f.Sha256)];
    }
    private static T Parse<T>(byte[] bytes)=>JsonSerializer.Deserialize<T>(bytes,new JsonSerializerOptions {
        PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=false,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true,
        RespectNullableAnnotations=true}) ?? throw new InvalidDataException("Missing Victory Road source document.");
    private static byte[] Read(string root,string relative,string sha)
    {
        Need(!Path.IsPathRooted(relative) && !relative.Split('/').Any(p=>p is "" or "." or ".."),"Invalid Victory Road source path.");
        string full=Path.GetFullPath(Path.Combine(root,relative));
        Need(full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"Source path escapes root.");
        var info=new FileInfo(full);Need(info.Exists && info.LinkTarget is null && info.Length is >0 and <=8000000,"Invalid Victory Road source file.");
        var bytes=File.ReadAllBytes(full);Need(Convert.ToHexStringLower(SHA256.HashData(bytes))==sha,"Victory Road source hash differs: "+relative);return bytes;
    }
    private static void Need(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    private sealed record Artifact(string Path,int ByteLength,string Sha256);
    private sealed record SourceLock(string SourceCommit,string SourceTree,string Profile,ImmutableArray<Artifact> Files);
    private sealed record Input(string Path,int ByteLength,string GitBlobSha1,string Sha256);
    private sealed record InputDocument(string SourceCommit,string SourceTree,ImmutableArray<Input> Files,
        ImmutableArray<Artifact> PredecessorInputs,ImmutableArray<Artifact> RegionInputs);
}
