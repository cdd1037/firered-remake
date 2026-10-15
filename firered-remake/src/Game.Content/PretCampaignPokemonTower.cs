using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretPokemonTowerRegion? PokemonTowerRegion { get; }
    public PretPokemonTowerPokemon? PokemonTowerPokemon { get; }
    public PretPokemonTowerItemsDocument? PokemonTowerItems { get; }
    public PretPokemonTowerPickupsDocument? PokemonTowerPickups { get; }
    public PokemonTowerTrainerCatalog? PokemonTowerTrainers { get; }
    public bool PokemonTowerEnabled => PokemonTowerRegion is not null;
    public ImmutableArray<int> PokemonTowerInitiallySetFlags => PokemonTowerRegion?.InitiallySetFlags ?? [];
    private static ImmutableDictionary<MapId,EarlyLandEncounterTable> PokemonTowerEncounterTables(PretPokemonTowerRegion region)
    {
        var result=ImmutableDictionary.CreateBuilder<MapId,EarlyLandEncounterTable>();
        var source=region.SourceFiles.Single(f=>f.Path==EarlyLandEncounterRules.SourcePath);
        foreach(var entry in region.Dependencies.GetProperty("wildEncounters").EnumerateArray())
        {
            var land=entry.GetProperty("land_mons");
            var table=new EarlyLandEncounterTable(PretPokemonTowerRegion.MapIdFromSourceSymbol(entry.GetProperty("map").GetString()!),                EarlyLandEncounterRules.SourceCommit,source.Path,entry.GetProperty("base_label").GetString()!,                land.GetProperty("encounter_rate").GetInt32(),land.GetProperty("mons").EnumerateArray().Select((m,i)=>new EarlyLandEncounterSlot(i,
                new("pret:"+m.GetProperty("species").GetString()),m.GetProperty("min_level").GetByte(),m.GetProperty("max_level").GetByte(),EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
            EarlyLandEncounterRules.ValidateTable(table);result.Add(table.Map,table);
        }
        if(result.Count!=5)throw new InvalidDataException("pokemon_tower_encounter_table_count");
        return result.ToImmutable();
    }
    private static IEnumerable<FieldPickupDefinition> PokemonTowerPickupDefinitions(PretPokemonTowerPickupsDocument pickups,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        foreach(var p in pickups.OrdinaryPickups)
        {
            var item=shop.Items.Single(i=>i.Id==p.Item);
            if(!scripts.TryConstant(p.Flag,out int flag)||flag!=p.SourceFlagId)throw new InvalidDataException("tower_pickup_flag_source");
            bool hidden=p.Kind=="hidden";
            yield return new("pokemon-tower:"+p.Flag,p.Map,hidden,p.LocalId,p.X,p.Y,p.Elevation,hidden?"EventScript_HiddenItemScript":p.Script!,
                item.SourceItemSymbol,item.SourceItemId,item.Id,item.Name,item.Pocket,p.Quantity,p.Flag,flag);
        }
    }
}
