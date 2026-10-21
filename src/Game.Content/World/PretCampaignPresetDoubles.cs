using Game.Domain.Pokemon;
using Game.Domain.Items;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed partial class PretCampaignCatalog
{
    public const string PresetDoublesContentProfile = "firered-preset-doubles-content-v1";
    public SourceTwinsTrainerDefinition Route8Twins { get; }
    public PokemonCatalog? PresetDoublePokemon { get; }
    public SourceMechanicsCatalog? PresetDoubleMechanics { get; }
    public SourceBattleItemCatalog? PresetDoubleItems { get; }
}
