using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Encounters;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretSevenIslandRegion SevenIslandRegion { get; }
    public bool SevenIslandEnabled => SevenIslandRegion is not null;
    public SevenIslandFieldData SevenIslandField => SevenIslandRegion.Field;
    public SevenIslandOwnedPokemonCatalog SevenIslandOwnership { get; }
    public SevenIslandOpponentPokemonCatalog SevenIslandOpponents { get; }
    public SevenIslandTrainerCatalog SevenIslandTrainerParties { get; }
    public SevenIslandTrainerCatalog SevenIslandTrainers => SevenIslandTrainerParties;
    public SevenIslandMachineCatalog SevenIslandMachines { get; }
    public SevenIslandShopCatalog SevenIslandShop { get; }
    public SevenIslandWaterEncounterCatalog SevenIslandWaterEncounters { get; }
    public SevenIslandItemsDocument SevenIslandItems { get; }

    private static void AppendSevenIslandSources(PretSevenIslandRegion region,
        List<PretSourceScriptFile> sources, List<string> texts, Dictionary<string,int> constants, List<string> hashes)
    {
        var previous = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in region.Scripts)
        {
            if (previous.TryGetValue(file.Path, out var old))
            {
                if (old.SourceRef.Sha256 != file.SourceRef.Sha256 || old.Text != file.Text)
                    throw new InvalidDataException("seven_island_source_conflict:" + file.Path);
            }
            else { previous.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
    }

    private static void ValidateSevenIslandComposition(PretSevenIslandRegion region,
        ImmutableDictionary<MapId,PretWorldMap> maps, ImmutableDictionary<MapId,PretWorldMap> predecessor,
        SevenIslandOwnedPokemonCatalog owned, SevenIslandOpponentPokemonCatalog opponents,
        SevenIslandTrainerCatalog trainers, SevenIslandMachineCatalog machines, SevenIslandShopCatalog shop,
        SevenIslandItemsDocument items, PretStoryCompilation scripts)
    {
        if (predecessor.Count != 257 || maps.Count != 275 || region.NewMaps.Count != 18 ||
            predecessor.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            region.NewMaps.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            owned.Catalog.Species.Count != 146 || opponents.Catalog.Species.Count != 180 ||
            !ReferenceEquals(opponents.Owned, owned) || !ReferenceEquals(machines.Pokemon, owned) ||
            trainers.Trainers.Count != 18 || trainers.BattlesByScript.Count != 20 ||
            shop.Catalog.Items.Length != 114 || items.Pickups.Length != 13 || items.RenewableFlags.Length != 6)
            throw new InvalidDataException("seven_island_composition_scope");
        SevenIslandTrainerPartyFactory.ValidateCatalog(trainers);
        foreach (var battle in trainers.BattlesByScript.Values)
        {
            var program = scripts.FindProgram(battle.ScriptEntry) ?? throw new InvalidDataException("seven_island_trainer_program");
            if (!program.Instructions.Any(i => i.Op == StoryOp.TrainerBattle && i.Source.Path == battle.Source.Path &&
                i.Source.Line >= battle.Source.StartLine && i.Source.Line <= battle.Source.EndLine))
                throw new InvalidDataException("seven_island_trainer_binding:" + battle.ScriptEntry);
        }
        foreach (var pickup in items.Pickups)
        {
            var source = region.Field.Pickups.SingleOrDefault(p => p.Flag == pickup.Flag);
            if (source is null || source.Item != pickup.Item || source.X != pickup.X || source.Y != pickup.Y ||
                source.Hidden != pickup.Hidden || source.Quantity != pickup.Quantity || source.SourceFlagId != pickup.SourceFlagId)
                throw new InvalidDataException("seven_island_pickup_binding");
        }
    }
}
