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
    public const string FirstSeviiContentProfile="firered-first-sevii-campaign-v1";
    public PretFirstSeviiRegion? FirstSeviiRegion { get; }
    public FirstSeviiTrainerCatalog? FirstSeviiTrainerParties { get; }
    public FirstSeviiItemCatalog? FirstSeviiItems { get; }
    public FirstSeviiWaterEncounterCatalog? FirstSeviiWaterEncounters { get; }
    public PretFirstSeviiPresentation? FirstSeviiPresentation { get; }
    public bool FirstSeviiEnabled=>FirstSeviiRegion is not null;

    private static void AppendFirstSeviiSources(PretFirstSeviiRegion region,List<PretSourceScriptFile> sources,
        List<string> texts,Dictionary<string,int> constants,List<string> hashes)
    {
        var prior=sources.ToDictionary(s=>s.Path,StringComparer.Ordinal);
        foreach(var source in region.Scripts)
        {
            if(prior.TryGetValue(source.Path,out var old))
            {
                if(old.SourceRef.Sha256!=source.SourceRef.Sha256||old.Text!=source.Text)
                    throw new InvalidDataException("first_sevii_source_conflict: "+source.Path);
            }
            else{prior.Add(source.Path,source);sources.Add(source);}
            if(!texts.Contains(source.Text,StringComparer.Ordinal))texts.Add(source.Text);
        }
        foreach(var text in region.TextSources.Values)if(!texts.Contains(text,StringComparer.Ordinal))texts.Add(text);
        MergeRegionalServiceConstants(constants,region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.Add("first-sevii-trainers:"+PretFirstSeviiRules.TrainersSha256);
        hashes.Add("first-sevii-items:"+PretFirstSeviiRules.ItemsSha256);
        hashes.Add("first-sevii-campaign:"+FirstSeviiContentProfile);
    }

    private static void ValidateFirstSeviiAnchors(PretFirstSeviiRegion region,ImmutableDictionary<MapId,PretWorldMap> maps)
    {
        if(maps.Count!=192||region.NewMaps.Count!=14||region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("first_sevii_predecessor_maps");
        foreach(var (name,events) in region.ReusedAnchorEvents)
        {
            var map=maps.Values.SingleOrDefault(m=>m.SourceRef.Path=="data/maps/"+name+"/map.json");
            if(map is null||!JsonElement.DeepEquals(map.Events,events))throw new InvalidDataException("first_sevii_anchor_events: "+name);
        }
    }

    private static IEnumerable<FieldPickupDefinition> FirstSeviiPickupDefinitions(PretFirstSeviiRegion region,
        FirstSeviiItemCatalog items,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        items.Validate();FirstSeviiShopCatalog.ValidateCatalog(shop);
        foreach(var pickup in items.Pickups)
        {
            var map=region.NewMaps[PretFirstSeviiRegion.MapIdFromSourceSymbol(pickup.MapSymbol)];
            if(!scripts.TryConstant(pickup.Flag,out int flag)||flag!=pickup.SourceFlagId)
                throw new InvalidDataException("first_sevii_pickup_flag");
            var item=shop.Items.SingleOrDefault(i=>i.Id==pickup.Item)
                ??throw new InvalidDataException("first_sevii_pickup_item_missing: "+pickup.ItemSymbol);
            if(item.SourceItemId!=pickup.SourceItemId||item.SourceItemSymbol!=pickup.ItemSymbol||item.Pocket!=pickup.Pocket)
                throw new InvalidDataException("first_sevii_pickup_item");
            var rows=map.Events.GetProperty(pickup.Hidden?"bg_events":"object_events").EnumerateArray();
            var row=pickup.Hidden?rows.Single(r=>r.TryGetProperty("item",out var id)&&id.GetString()==pickup.ItemSymbol&&
                r.GetProperty("flag").GetString()==pickup.Flag):rows.ElementAt(pickup.SourceLocalId!.Value-1);
            if(row.GetProperty("x").GetInt32()!=pickup.X||row.GetProperty("y").GetInt32()!=pickup.Y||
                row.GetProperty("elevation").GetInt32()!=pickup.Elevation||row.GetProperty("flag").GetString()!=pickup.Flag)
                throw new InvalidDataException("first_sevii_pickup_event");
            if(pickup.Hidden)
            {
                if(row.GetProperty("quantity").GetInt32()!=pickup.Quantity||row.GetProperty("underfoot").GetBoolean())
                    throw new InvalidDataException("first_sevii_hidden_pickup");
            }
            else
            {
                if(row.GetProperty("script").GetString()!=pickup.Script)throw new InvalidDataException("first_sevii_pickup_script");
                var program=scripts.FindProgram(pickup.Script)??throw new InvalidDataException("first_sevii_pickup_program");
                if(!program.Instructions.Any(i=>i.Opcode=="setorcopyvar"&&i.Args.SequenceEqual(new[]{"VAR_0x8000",pickup.ItemSymbol})))
                    throw new InvalidDataException("first_sevii_pickup_reward");
            }
            yield return pickup.Hidden?pickup with{Script="EventScript_HiddenItemScript"}:pickup;
        }
    }

    private static void ValidateFirstSeviiComposition(PretFirstSeviiRegion region,ImmutableDictionary<MapId,PretWorldMap> maps,
        ImmutableDictionary<MapId,PretWorldMap> previous,CinnabarOwnedPokemonCatalog owned,CinnabarOpponentPokemonCatalog opponents,
        FirstSeviiTrainerCatalog trainers,PretStoryCompilation scripts,ImmutableDictionary<string,string>.Builder texts)
    {
        if(maps.Count!=previous.Count+14||previous.Any(p=>!ReferenceEquals(p.Value,maps[p.Key]))||
            region.NewMaps.Any(p=>!ReferenceEquals(p.Value,maps[p.Key]))||owned.Catalog.Species.Count!=115||
            opponents.Catalog.Species.Count!=122||owned.Catalog.Moves.Count!=354)
            throw new InvalidDataException("first_sevii_composition_scope");
        foreach(var battle in trainers.BattlesBySource.Values)
        {
            var program=scripts.FindProgram(battle.ScriptEntry)??throw new InvalidDataException("first_sevii_trainer_script");
            if(!program.Instructions.Any(i=>i.Op==StoryOp.TrainerBattle&&i.Source.Line==battle.Source.StartLine))
                throw new InvalidDataException("first_sevii_trainer_instruction");
        }
        foreach(var (symbol,value) in new[]{("HEAL_LOCATION_ONE_ISLAND",14),("HEAL_LOCATION_TWO_ISLAND",15),("HEAL_LOCATION_THREE_ISLAND",16)})
            if(!scripts.TryConstant(symbol,out int actual)||actual!=value)throw new InvalidDataException("first_sevii_heal_location");
        if(!texts.ContainsKey("ThreeIsland_BerryForest_Text_HelpScaryPokemon"))
            throw new InvalidDataException("first_sevii_rescue_text");
    }
}
