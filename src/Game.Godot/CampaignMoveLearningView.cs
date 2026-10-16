using Game.Domain.Ids;
using Game.Domain.Pokemon;

public partial class CampaignView
{
    private const string UnsupportedLearningText = "Effect not implemented.\nChoose DECLINE to continue.";

    private bool MoveLearningUnavailable(MoveId move) =>
        !PokemonMoveRules.IsLearningSupported(_catalog.PokemonContent!.Catalog, move);

    private string MoveLearningPrompt(MoveId move) => MoveLearningUnavailable(move)
        ? $"{MoveName(move)} can't be learned.\nEffect not implemented." : $"Forget which move for\n{MoveName(move)}?";

    private string MoveLearningError() => _status switch
    {
        "pokemon_learning_effect_unsupported" => UnsupportedLearningText,
        "hm_move_cannot_be_forgotten" => HmCannotForgetText,
        _ => _status
    };
}
