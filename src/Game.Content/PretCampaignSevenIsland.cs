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
    public SevenIslandFieldData SevenIslandField => SevenIslandRegion.Field;
    public SevenIslandTrainerCatalog SevenIslandTrainerParties { get; }
    public SevenIslandTrainerCatalog SevenIslandTrainers => SevenIslandTrainerParties;
    public WaterEncounterCatalog SevenIslandWaterEncounters { get; }
    public SevenIslandItemsDocument SevenIslandItems { get; }

    private static void ValidateSevenIslandComposition(PretSevenIslandRegion region, SevenIslandTrainerCatalog trainers, SevenIslandItemsDocument items, PretStoryCompilation scripts)
    {
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
