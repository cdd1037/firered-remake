using System.Collections.Immutable;

namespace Game.Domain.Battle;

public sealed record SourcePokedudeInput(ImmutableArray<int> CursorPositions, ImmutableArray<int> Delays);
public sealed record SourcePokedudeInputCursor(int Index = 0, int Timer = 0, int VisibleCursor = 0);
public sealed record SourcePokedudeInputResult(SourcePokedudeInputCursor State, int? Selection,
    bool PlaySelectSound, string? Fault)
{
    public bool Succeeded => Fault is null;
}
public sealed record SourcePokedudeVoiceover(string ControllerCommand, BattleSide Side,
    string StringId, string Callback, string TextSymbol);
public sealed record SourcePokedudeVoiceoverCursor(int Next = 0, SourcePokedudeVoiceover? Pending = null);
public sealed record SourcePokedudeVoiceoverResult(SourcePokedudeVoiceoverCursor State,
    SourcePokedudeVoiceover? Voiceover, bool RunOriginalCommand, string? Fault);

/// <summary>Literal per-battler controller scripts. They supply menu input only; damage,
/// status, switches, item effects, experience and outcome stay in the shared battle owner.</summary>
public static class SourcePokedudeController
{
    public static SourcePokedudeInputResult Tick(ImmutableArray<SourcePokedudeInput> script,
        SourcePokedudeInputCursor cursor, BattleSide side, bool chooseMove)
    {
        SourcePokedudeInputResult Fail(string why) => new(cursor!, null, false, why);
        if (!Enum.IsDefined(side) || script.IsDefaultOrEmpty || cursor is null || cursor.Index < 0 || cursor.Timer < 0 ||
            cursor.VisibleCursor is < 0 or > 3 || script.Any(r => r is null || r.CursorPositions.IsDefault ||
                r.CursorPositions.Length != 2 || r.Delays.IsDefault || r.Delays.Length != 2 ||
                r.Delays.Any(d => d is < 0 or > 255))) return Fail("pokedude_input_shape");
        // Several source arrays have no terminal marker. Out-of-bounds is unsupported,
        // never an invented repeat, forced KO or success result.
        if (cursor.Index >= script.Length) return Fail("pokedude_input_stream_exhausted");
        int battler = (int)side, selected = script[cursor.Index].CursorPositions[battler];
        int delay = script[cursor.Index].Delays[battler], sentinel = chooseMove ? 255 : 4;
        if (selected is < 0 or > 3 || cursor.Timer > delay) return Fail("pokedude_input_cursor");
        if (cursor.Timer == delay)
        {
            int next = cursor.Index + 1;
            if (next < script.Length && script[next].CursorPositions[battler] == sentinel) next = 0;
            return new(cursor with { Index = next, Timer = 0 }, selected, side == BattleSide.Player, null);
        }
        bool moved = selected != cursor.VisibleCursor && cursor.Timer == delay / 2;
        return new(cursor with { Timer = cursor.Timer + 1, VisibleCursor = moved ? selected : cursor.VisibleCursor },
            null, moved, null);
    }

    public static SourcePokedudeVoiceoverResult Intercept(ImmutableArray<SourcePokedudeVoiceover> script,
        SourcePokedudeVoiceoverCursor cursor, string controllerCommand, BattleSide side, string stringId = "0")
    {
        if (script.IsDefault || cursor is null || cursor.Next < 0 || cursor.Next > script.Length || !Enum.IsDefined(side))
            return new(cursor!, null, false, "pokedude_voiceover_shape");
        if (cursor.Pending is not null) return new(cursor, cursor.Pending, false, null);
        if (cursor.Next == script.Length) return new(cursor, null, true, null);
        var cue = script[cursor.Next];
        if (cue.ControllerCommand != controllerCommand || cue.Side != side ||
            controllerCommand == "CONTROLLER_PRINTSTRING" && cue.StringId != stringId)
            return new(cursor, null, true, null);
        if (cue.Callback == "NULL") return new(cursor with { Next = cursor.Next + 1 }, null, true, null);
        if (cue.Callback is not ("PokedudeAction_PrintVoiceoverMessage" or "PokedudeAction_PrintMessageWithHealthboxPals"))
            return new(cursor, null, false, "pokedude_voiceover_callback");
        return new(cursor with { Next = cursor.Next + 1, Pending = cue }, cue, false, null);
    }

    /// <summary>Both source callbacks require completed printing and a fresh A press.
    /// Palette transitions and the healthbox palette variant belong to presentation.</summary>
    public static SourcePokedudeVoiceoverCursor Acknowledge(SourcePokedudeVoiceoverCursor cursor,
        bool textFinished, bool newAPress) => cursor.Pending is not null && textFinished && newAPress
        ? cursor with { Pending = null } : cursor;
}
