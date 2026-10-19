using Game.Application;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private int _candyMoveCursor;
    private string? _candyViewKey, _candyNotice;
    private void SynchronizeRareCandy()
    {
        var scene = _session.State.RareCandy;
        string? key = scene is null ? null : $"{scene.Result.Pokemon.InstanceId}:{scene.Result.Pokemon.Pokemon.Level}:{scene.Stage}:{scene.PageIndex}:{scene.PendingMoves.Length}";
        if (key == _candyViewKey) return;
        _moveSelectionStamp = null;
        _candyViewKey = key; _candyMoveCursor = 0; _candyNotice = null; _held = null;
        if (scene is { Stage: CampaignRareCandyStage.LearnMove } && MoveLearningUnavailable(scene.PendingMoves[0].Move))
            _candyMoveCursor = _session.State.Party.Single(p => p.InstanceId == scene.Result.Pokemon.InstanceId).Pokemon.Moves.Length;
    }

    private bool RareCandyInput(InputEventKey key)
    {
        if (key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        if (!key.Pressed || key.Echo) return true;
        SynchronizeRareCandy();
        var scene = _session.State.RareCandy!;
        bool confirm = key.Keycode is Key.Enter or Key.A;
        bool cancel = key.Keycode is Key.Backspace or Key.B;
        if (_candyNotice is not null) { if (confirm || cancel) _candyNotice = null; return true; }
        if (scene.Stage == CampaignRareCandyStage.LearnMove)
        {
            var owned = _session.State.Party.Single(p => p.InstanceId == scene.Result.Pokemon.InstanceId);
            if (!MoveSelectionCurrent("candy", owned, scene.PendingMoves[0].Move))
            { _candyMoveCursor = 0; _candyNotice = SummaryChangedNotice; return true; }
            int count = owned.Pokemon.Moves.Length;
            if (key.Keycode == Key.Up) _candyMoveCursor = Math.Max(0, _candyMoveCursor - 1);
            if (key.Keycode == Key.Down) _candyMoveCursor = Math.Min(count, _candyMoveCursor + 1);
            if (confirm || cancel)
            {
                if (confirm && _candyMoveCursor < count && MoveLearningUnavailable(scene.PendingMoves[0].Move)) return true;
                       bool accepted = Apply(new("rare_candy_learn", MoveSlot: cancel || _candyMoveCursor == count ? -1 : _candyMoveCursor));
                if (!accepted) _candyNotice = MoveLearningError();
            }
        }
        else if ((confirm || cancel) && _fanfareRemaining <= 0)
            Apply(new("ack"));
        return true;
    }

    private void DrawRareCandy()
    {
        SynchronizeRareCandy();
        var scene = _session.State.RareCandy!;
        var owned = _session.State.Party.Single(p => p.InstanceId == scene.Result.Pokemon.InstanceId);
        DrawRect(new(0, 0, 240, 160), new Color("406060"));
        bool stats = scene.Stage is CampaignRareCandyStage.StatsGain or CampaignRareCandyStage.StatsTotal;
        if (stats)
        {
            _battleSkin.DrawStandardFrame(this, new(48, 0, 192, 112));
            var before = scene.Result.StatsBefore; var after = owned.Pokemon.Stats;
            var values = new[] { ("MAX. HP", before.MaxHp, after.MaxHp), ("ATTACK", before.Attack, after.Attack),
                ("DEFENSE", before.Defense, after.Defense), ("SP. ATK", before.SpecialAttack, after.SpecialAttack),
                ("SP. DEF", before.SpecialDefense, after.SpecialDefense), ("SPEED", before.Speed, after.Speed) };
            for (int i = 0; i < values.Length; i++)
            {
                var (label, oldValue, newValue) = values[i];
                SourceText(_sourceNormal, new(58, 7 + i * 16), label);
                SourceText(_sourceNormal, new(194, 7 + i * 16), scene.Stage == CampaignRareCandyStage.StatsTotal
                    ? newValue.ToString().PadLeft(3) : "+" + (newValue - oldValue).ToString().PadLeft(2));
            }
        }
        else if (scene.Stage == CampaignRareCandyStage.LearnMove)
        {
            DrawMoveSelectionSummary("candy", owned, scene.PendingMoves[0].Move, _candyMoveCursor);
            if (_candyNotice is not null) ItemMessageBox(_candyNotice);
            return;
        }
        else DrawTexture(_battleArt[(PresentedRenderSpecies(owned.Pokemon), "front", owned.Pokemon.IsShiny)].Texture, new(88, 24));
        _battleSkin.DrawStandardFrame(this, new(0, 112, 240, 48));
        string text = stats ? $"{_catalog.PokemonDisplayName(owned)} grew to\nLV. {owned.Pokemon.Level}!"
            : scene.Stage == CampaignRareCandyStage.LearnMove ? MoveLearningPrompt(scene.PendingMoves[0].Move)
            : scene.Pages[scene.PageIndex];
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(10, 119 + i * 16), lines[i]);
        if (_candyNotice is not null) ItemMessageBox(_candyNotice);
    }

    private bool MouseRareCandy(Vector2 point) => _candyNotice is not null ? MouseConfirm(point, MouseMessage) : _session.State.RareCandy!.Stage == CampaignRareCandyStage.LearnMove
        ? MouseMoveSelectionSummary(point, ref _candyMoveCursor,
            _session.State.Party.Single(p => p.InstanceId == _session.State.RareCandy.Result.Pokemon.InstanceId).Pokemon.Moves.Length)
        : MouseConfirm(point, MouseMessage);
}
