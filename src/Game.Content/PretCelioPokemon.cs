using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CelioPokemonContent(GamePokemonCatalog Owned, GameOpponentPokemonCatalog Opponents,
    MachineCatalog Machines);
