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

public sealed record VictoryRoadTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles);
public sealed record VictoryRoadFishingTable(MapId Map, int RawEncounterRate, ImmutableArray<OldRodEncounterSlot> Slots,
    ImmutableDictionary<string, ImmutableArray<int>> RodGroups);
public sealed record VictoryRoadWildSpeciesSource(SpeciesId Species,int SourceSpeciesId,string Ability1,string Ability2,
    string ItemCommon,string ItemRare,OpeningItemSourceRef Source,OpeningItemSourceRef LearnsetSource);
public sealed record VictoryRoadWildDocument(ImmutableArray<EarlyLandEncounterTable> Land,
    ImmutableArray<WaterEncounterTable> Water,ImmutableArray<VictoryRoadFishingTable> Fishing,
    ImmutableArray<VictoryRoadInitialMoves> InitialMoves,ImmutableArray<VictoryRoadWildSpeciesSource> Species,
    ImmutableArray<SpeciesId> OwnedSpeciesAdded);

/// <summary>Authenticated Victory Road rules without new ownership or an Application profile.</summary>
public static class PretVictoryRoadRules
{
    public const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public static VictoryRoadTrainerDocument LoadTrainerDocument(string root)
    {
        var d=ContentJson.ParseStrict<VictoryRoadTrainerDocument>(Encoding.UTF8.GetString(Read(root,"content/generated/data/regions/victory-road/trainers.json")), SharedTrainerCatalog.Options(root));
        _=Catalog(d);return d;
    }
    public static VictoryRoadTrainerCatalog LoadTrainers(string root)=>Catalog(LoadTrainerDocument(root));
    private static VictoryRoadTrainerCatalog Catalog(VictoryRoadTrainerDocument d)
    {
        var c=new VictoryRoadTrainerCatalog(d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.ScriptEntry,StringComparer.Ordinal));
        VictoryRoadTrainerPartyFactory.ValidateCatalog(c);return c;
    }
    public static VictoryRoadWildDocument LoadWild(string root)
    {
        var d=Parse<VictoryRoadWildDocument>(Read(root,"content/generated/data/regions/victory-road/wild.json"));
        Need(d.Land.Length==4 && d.Water.Length==1 &&
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
        Need(            fish.Slots.SequenceEqual(f.Slots) && fish.RodGroups.Count==3 && fish.RodGroups["old_rod"].SequenceEqual([0,1]) &&
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
    private static T Parse<T>(byte[] bytes)=>JsonSerializer.Deserialize<T>(bytes,new JsonSerializerOptions {
        PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=false,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true,RespectNullableAnnotations=true}) ?? throw new InvalidDataException("Missing Victory Road source document.");
    private static byte[] Read(string root,string relative)
    {
        Need(!Path.IsPathRooted(relative) && !relative.Split('/').Any(p=>p is "" or "." or ".."),"Invalid Victory Road source path.");
        string full=Path.GetFullPath(Path.Combine(root,relative));
        Need(full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"Source path escapes root.");
        var info=new FileInfo(full);Need(info.Exists && info.LinkTarget is null && info.Length is >0 and <=8000000,"Invalid Victory Road source file.");
        var bytes=File.ReadAllBytes(full);return bytes;
    }
    private static void Need(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    private sealed record Artifact(string Path);
    private sealed record SourceLock(ImmutableArray<Artifact> Files);
    private sealed record Input(string Path);
    private sealed record InputDocument(ImmutableArray<Input> Files, ImmutableArray<Artifact> PredecessorInputs,
        ImmutableArray<Artifact> RegionInputs);
}
