using Game.Application;
using Godot;

// Deliberately a small, truthful presentation adapter. The source portraits and font are real;
// the original evolution background, morph animation, fanfare and timing are not reproduced.
public partial class CampaignView
{
    private string? _evolutionViewKey;
    private int _evolutionMoveCursor;

    private void SynchronizeEvolutionView()
    {
        if (_session.State.Evolution is not { } scene) { _evolutionViewKey = null; return; }
        string key = $"{scene.BattleId}:{scene.ProposalIndex}:{scene.Stage}:{scene.PageIndex}:{scene.PendingMoves.Length}";
        if (_evolutionViewKey == key) return;
        _evolutionViewKey = key; _evolutionMoveCursor = 0; _held = null;
        if (scene is { Stage: CampaignEvolutionStage.LearnMove } && MoveLearningUnavailable(scene.PendingMoves[0].Move))
            _evolutionMoveCursor = _session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId).Pokemon.Moves.Length;
    }

    private bool TryEvolutionInput(InputEventKey key)
    {
        if (_session.State.Evolution is not { } scene) return false;
        if (key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        if (!key.Pressed || key.Echo) return true;
        SynchronizeEvolutionView(); _held = null;
        bool confirm = key.Keycode is Key.Enter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.B;
        if (_wildNotice is not null)
        {
            if (confirm || cancel) _wildNotice = null;
            return true;
        }
        if (scene.Stage == CampaignEvolutionStage.Proposal)
        {
            if (confirm || cancel) Apply(new("evolution_choice", Choice: confirm ? 1 : 0));
        }
        else if (scene.Stage == CampaignEvolutionStage.Results)
        {
            if (confirm) Apply(new("ack"));
        }
        else
        {
            var owned = _session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId);
            if (key.Keycode == Key.Up) _evolutionMoveCursor = Math.Max(0, _evolutionMoveCursor - 1);
            if (key.Keycode == Key.Down) _evolutionMoveCursor = Math.Min(owned.Pokemon.Moves.Length, _evolutionMoveCursor + 1);
            if (confirm || cancel)
            {
                if (confirm && _evolutionMoveCursor < owned.Pokemon.Moves.Length && MoveLearningUnavailable(scene.PendingMoves[0].Move)) return true;
                       bool accepted = Apply(new("evolution_learn", MoveSlot: cancel || _evolutionMoveCursor == owned.Pokemon.Moves.Length ? -1 : _evolutionMoveCursor));
                if (!accepted) _wildNotice = MoveLearningError();
            }
        }
        return true;
    }

    private void DrawEvolution()
    {
        SynchronizeEvolutionView();
        var scene = _session.State.Evolution!;
        var owned = _session.State.Party.Single(p => p.InstanceId == scene.Proposal.InstanceId);
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        if (scene.Stage == CampaignEvolutionStage.LearnMove)
        {
            _battleSkin.DrawStandardFrame(this, new(8, 8, 224, 104));
            for (int i = 0; i < owned.Pokemon.Moves.Length; i++)
            {
                SourceText(MoveLearningUnavailable(scene.PendingMoves[0].Move) ? _sourceInactiveCursor : _sourceNormal, new(28, 14 + i * 16), MoveName(owned.Pokemon.Moves[i].Move));
                if (i == _evolutionMoveCursor) SourceText(_sourceNormal, new(14, 14 + i * 16), "▶");
            }
            int y = 14 + owned.Pokemon.Moves.Length * 16;
            SourceText(_sourceNormal, new(28, y), MoveLearningUnavailable(scene.PendingMoves[0].Move) ? "DECLINE" : "CANCEL");
            if (_evolutionMoveCursor == owned.Pokemon.Moves.Length) SourceText(_sourceNormal, new(14, y), "▶");
        }
        else
        {
            var portrait = _battleArt[(PresentedRenderSpecies(owned.Pokemon), "front", owned.Pokemon.IsShiny)];
            DrawTexture(portrait.Texture, new(88, 24));
        }
        _battleSkin.DrawStandardFrame(this, new(0, 112, 240, 48));
        string text = scene.Stage switch
        {
            CampaignEvolutionStage.Proposal => $"What? {owned.Nickname}\nis evolving!",
            CampaignEvolutionStage.LearnMove => MoveLearningPrompt(scene.PendingMoves[0].Move),
            _ => scene.Pages[scene.PageIndex]
        };
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(10, 119 + i * 16), lines[i]);
        if (scene.Stage == CampaignEvolutionStage.Proposal)
            SourceText(_sourceSmall, new(14, 94), "A/Enter: continue  B/Backspace: stop");
        if (_wildNotice is not null) ItemMessageBox(_wildNotice);
    }
}
