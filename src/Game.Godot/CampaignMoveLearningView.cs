using Game.Domain.Ids;
using Game.Domain.Pokemon;

public partial class CampaignView
{
    private string UnsupportedLearningText => _summaryText.Get("notice.learning-unsupported");

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

public partial class CampaignView
{
    private SummarySelectionStamp? _moveSelectionStamp;
    private string MoveSelectionContext(string origin, MoveId? move) => origin + ":" + (move?.Value ?? "delete");
    private void CaptureMoveSelection(string origin, PartyPokemon owned, MoveId? move)
        => _moveSelectionStamp = new(MoveSelectionContext(origin, move), _session.Revision, owned.InstanceId);
    private bool MoveSelectionCurrent(string origin, PartyPokemon owned, MoveId? move)
    {
        string context = MoveSelectionContext(origin, move);
        if (_moveSelectionStamp is null || _moveSelectionStamp.Context != context)
        { CaptureMoveSelection(origin, owned, move); return true; }
        if (_moveSelectionStamp.Matches(context, _session.Revision, owned.InstanceId)) return true;
        // The caller shows a notice and consumes this input. A fresh selection is
        // required after that notice; an old cursor never commits to a new body.
        CaptureMoveSelection(origin, owned, move); return false;
    }
    private void DrawMoveSelectionSummary(string origin, PartyPokemon owned, MoveId? move, int cursor)
    {
        string context = MoveSelectionContext(origin, move);
        if (_moveSelectionStamp is null || _moveSelectionStamp.Context != context)
            CaptureMoveSelection(origin, owned, move);
        DrawNativeSummary(owned, 2, true, new(move is null ? SummaryInteractionMode.Delete : SummaryInteractionMode.Replace,
            cursor, NewMove: move));
    }
    private bool MouseMoveSelectionSummary(Godot.Vector2 point, ref int cursor, int knownMoves)
    {
        if (new Godot.Rect2(208, 0, 32, 16).HasPoint(point)) return MouseKey(Godot.Key.Backspace);
        if (new Godot.Rect2(152, 0, 56, 16).HasPoint(point)) return MouseKey();
        int row = SummaryInteractionPresentationAssets.MoveRowAt(point.X, point.Y, knownMoves + 1);
        if (row < 0) return false;
        cursor = row; return MouseKey();
    }
    private bool NavigateMoveSelectionSummary(Godot.Vector2 point, int direction, ref int cursor, int knownMoves)
    {
        int row = SummaryInteractionPresentationAssets.MoveRowAt(point.X, point.Y, knownMoves + 1);
        if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (cursor == row) return false;
        cursor = row; return true;
    }
}
