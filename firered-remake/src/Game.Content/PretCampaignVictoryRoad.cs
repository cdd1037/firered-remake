using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string VictoryRoadContentProfile="firered-victory-road-campaign-v1";
    public PretVictoryRoadRegion? VictoryRoadRegion { get; }
    public VictoryRoadTrainerCatalog? VictoryRoadTrainerParties { get; }
    public VictoryRoadOpponentPokemonCatalog? VictoryRoadOpponents { get; }
    public VictoryRoadItemContent? VictoryRoadItems { get; }
    public VictoryRoadWaterEncounterCatalog? VictoryRoadWaterEncounters { get; }
    public bool VictoryRoadEnabled=>VictoryRoadRegion is not null;

    private static void AppendVictoryRoadSources(PretVictoryRoadRegion region,List<PretSourceScriptFile> sources,List<string> texts,Dictionary<string,int> constants)
    {
        var prior=sources.ToDictionary(s=>s.Path,StringComparer.Ordinal);
        foreach(var source in region.Scripts)
        {
            if(prior.TryGetValue(source.Path,out var old))
            {
                if(old.Text!=source.Text)
                    throw new InvalidDataException("victory_road_source_conflict: "+source.Path);
            }
            else{prior.Add(source.Path,source);sources.Add(source);}
            if(!texts.Contains(source.Text,StringComparer.Ordinal))texts.Add(source.Text);
        }
        foreach(var text in region.TextSources.Values)if(!texts.Contains(text,StringComparer.Ordinal))texts.Add(text);
        MergeRegionalServiceConstants(constants,region.Constants);
    }

    private static void ValidateVictoryRoadAnchors(PretVictoryRoadRegion region,ImmutableDictionary<MapId,PretWorldMap> maps)
    {
        if(region.NewMaps.Count!=4||region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("victory_road_predecessor_maps");
        foreach(var (name,events) in region.ReusedAnchorEvents)
        {
            var map=maps.Values.SingleOrDefault(m=>m.SourceRef.Path=="data/maps/"+name+"/map.json");
            if(map is null||!JsonElement.DeepEquals(map.Events,events))throw new InvalidDataException("victory_road_anchor_events: "+name);
        }
        var old=maps[new("map.route-23")];var exact=region.SourceMaps[old.Id];
        if(old.Width!=exact.Width||old.Height!=exact.Height||!old.Cells.Select(c=>c.RawBlock).SequenceEqual(exact.Cells.Select(c=>c.RawBlock)))
            throw new InvalidDataException("victory_road_route23_source_cells");
    }

    private static IEnumerable<FieldPickupDefinition> VictoryRoadPickupDefinitions(PretVictoryRoadRegion region,VictoryRoadItemContent content,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        foreach(var pickup in content.Items.Pickups)
        {
            var map=region.SourceMaps[pickup.Map];var item=content.Items.Items.Single(i=>i.Item.Id==pickup.Item).Item;
            if(pickup.Underfoot||!scripts.TryConstant(pickup.Flag,out int flag))throw new InvalidDataException("victory_road_pickup_flag");
            var rows=map.Events.GetProperty(pickup.Hidden?"bg_events":"object_events").EnumerateArray();
            var row=pickup.Hidden?rows.Single(r=>r.TryGetProperty("item",out var id)&&id.GetString()==item.SourceItemSymbol&&r.GetProperty("flag").GetString()==pickup.Flag):rows.ElementAt(pickup.LocalId-1);
            if(row.GetProperty("x").GetInt32()!=pickup.X||row.GetProperty("y").GetInt32()!=pickup.Y||row.GetProperty("elevation").GetInt32()!=pickup.Elevation||row.GetProperty("flag").GetString()!=pickup.Flag)
                throw new InvalidDataException("victory_road_pickup_source");
            if(pickup.Hidden)
            {
                if(row.GetProperty("quantity").GetInt32()!=pickup.Quantity||row.GetProperty("underfoot").GetBoolean())throw new InvalidDataException("victory_road_hidden_pickup");
            }
            else if(row.GetProperty("script").GetString()!=pickup.ScriptEntry||scripts.FindProgram(pickup.ScriptEntry) is not {} program||
                !program.Instructions.Any(i=>i.Opcode=="setorcopyvar"&&i.Args.SequenceEqual(new[]{"VAR_0x8000",item.SourceItemSymbol})))
                throw new InvalidDataException("victory_road_pickup_script");
            yield return new(pickup.Id,map.SourceMapSymbol,pickup.Hidden,pickup.Hidden?null:pickup.LocalId,pickup.X,pickup.Y,pickup.Elevation,
                pickup.Hidden?"EventScript_HiddenItemScript":pickup.ScriptEntry,item.SourceItemSymbol,item.SourceItemId,item.Id,item.Name,item.Pocket,pickup.Quantity,pickup.Flag,flag);
        }
    }

    private static void ValidateVictoryRoadComposition(PretVictoryRoadRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,ImmutableDictionary<MapId,PretWorldMap> previous,VictoryRoadTrainerCatalog trainers,CinnabarOwnedPokemonCatalog owned,PretStoryCompilation scripts)
    {
        if(            owned.Catalog.Species.Count!=115||owned.Catalog.Moves.Count!=354)throw new InvalidDataException("victory_road_composition_scope");
        VictoryRoadTrainerPartyFactory.ValidateCatalog(trainers);
        foreach(var battle in trainers.BattlesByScript.Values)
        {
            var program=scripts.FindProgram(battle.ScriptEntry)??throw new InvalidDataException("victory_road_trainer_script");
            if(!program.Instructions.Any(i=>i.Op==StoryOp.TrainerBattle&&i.Source.Path==battle.Source.Path&&i.Args.Contains(battle.Trainer.Value[5..])))
                throw new InvalidDataException("victory_road_trainer_instruction");
        }
    }
}
