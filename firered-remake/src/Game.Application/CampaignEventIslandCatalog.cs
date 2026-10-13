using Game.Domain.Pokemon;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool EventIslandsEnabled => _content.EventIslandsEnabled;
    private static void ValidateEventIslandCatalog(ICampaignCatalog content)
    {
        if (!content.EventIslandsEnabled || content.Maps.Count != 299 || content.EventIslandField is not { } field ||
            content.EventIslandOwnership is not { } owned || content.EventIslandOpponents is not { } opponents ||
            content.EventIslandMachines is not { } machines || content.EventIslandShop is not { } shop ||
            !ReferenceEquals(EventIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(owned.Catalog, content.PokemonContent?.Catalog) || !ReferenceEquals(owned.Predecessor, content.SevenIslandOwnership) ||
            !ReferenceEquals(EventIslandOpponentPokemonCatalog.For(owned), opponents) || !ReferenceEquals(opponents.Predecessor, content.SevenIslandOpponents) ||
            !ReferenceEquals(EventIslandMachineCatalog.For(content.PewterMachines), machines) || !ReferenceEquals(machines.Pokemon, owned) ||
            !ReferenceEquals(machines.Predecessor, content.SevenIslandMachines) || !ReferenceEquals(EventIslandShopCatalog.For(content.EarlyShop), shop) ||
            !ReferenceEquals(shop.Predecessor, content.SevenIslandShop) || owned.Catalog.Species.Count != 149 || opponents.Catalog.Species.Count != 183)
            throw new InvalidDataException("event_island_current_binding");
        foreach (var kind in Enum.GetValues<Game.Domain.WildBattle.EventIslandEncounterKind>())
        {
            var source = EventLegendary(kind);
            var program = content.FindProgram(source.Caller);
            if (program is null || program.Instructions.Count(i => i.Opcode == "seteventmon" && i.Source.Path == source.Source) != 1 ||
                program.Instructions.Count(i => i.Opcode == "special" && i.Args.SequenceEqual(new[] { "StartLegendaryBattle" })) != 1)
                throw new InvalidDataException("event_island_battle_script_binding");
        }
    }
}
