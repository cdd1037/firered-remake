using Game.Domain.Pokemon;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static void ValidateEventIslandCatalog(ICampaignCatalog content)
    {
        if (content.EventIslandField is not { } field ||
            content.GamePokemon is not { } owned || content.OpponentPokemon is not { } opponents ||
            content.Machines is not { } machines || content.EarlyShop is not { } shop ||
            !ReferenceEquals(owned, content.PokemonContent?.Pokemon) ||
            !ReferenceEquals(owned, content.PokemonContent?.Pokemon) || !ReferenceEquals(owned, content.PokemonContent?.Pokemon) ||
            !ReferenceEquals(opponents, content.PokemonContent?.Pokemon.Opponents) || !ReferenceEquals(opponents, content.PokemonContent?.Pokemon.Opponents) ||
            !shop.Items.All(i => content.Inventory.Items.ContainsKey(i.Id)) )
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
