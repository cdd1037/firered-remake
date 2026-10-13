using Game.Domain.Trainers;

/// <summary>Source multichoice geometry and online 60 Hz batching, independent of Godot.</summary>
internal sealed class TrainerTowerMenuPresentation
{
    internal readonly record struct Layout(int X, int Y, int Width, int Height);
    internal const int Cancel = 127;
    internal string SourceSymbol { get; }
    internal IReadOnlyList<string> Options { get; }
    internal int Cursor { get; private set; }
    internal TrainerTowerMenuPresentation(string sourceSymbol, IEnumerable<string> options)
    {
        string[] expected = sourceSymbol switch
        {
            "MULTICHOICE_YES_NO_INFO" => ["YES", "NO", "INFO"],
            "MULTICHOICE_TRAINER_TOWER_MODE" => ["SINGLE", "DOUBLE", "KNOCKOUT", "MIXED", "EXIT"],
            _ => throw new InvalidDataException("Trainer Tower source menu differs.")
        };
        if (!options.SequenceEqual(expected)) throw new InvalidDataException("Trainer Tower menu rows differ.");
        SourceSymbol = sourceSymbol; Options = Array.AsReadOnly(expected);
    }
    internal Layout Frame(int textWidth)
    {
        if (textWidth is <= 0 or > 200) throw new ArgumentOutOfRangeException(nameof(textWidth));
        int width = (textWidth + 9) / 8 + 1;
        int left = SourceSymbol == "MULTICHOICE_YES_NO_INFO" ? 18 : 13;
        int top = SourceSymbol == "MULTICHOICE_YES_NO_INFO" ? 6 : 3;
        return new(Math.Min(left, 28 - width) * 8, top * 8, (width + 2) * 8, ((Options.Count * 14 + 7) / 8 + 2) * 8);
    }
    internal bool Move(int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        int next = Options.Count > 3 ? (Cursor + direction + Options.Count) % Options.Count : Math.Clamp(Cursor + direction, 0, Options.Count - 1);
        return Select(next);
    }
    internal bool Select(int index)
    {
        if (index < 0 || index >= Options.Count || index == Cursor) return false;
        Cursor = index; return true;
    }
    internal int RowAt(double x, double y, int textWidth)
    {
        var f = Frame(textWidth);
        return x >= f.X + 8 && x < f.X + f.Width - 8 && y >= f.Y + 8 && y < f.Y + 8 + 14 * Options.Count
            ? (int)((y - f.Y - 8) / 14) : -1;
    }
}

/// <summary>Only live process deltas enter this counter. A saved game never supplies wall-clock elapsed time.</summary>
internal sealed class TrainerTowerFrameAccumulator
{
    private double _frames;
    private double _savedWriteSeconds;
    internal void Advance(double seconds, bool running)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        double suspended = Math.Min(seconds, _savedWriteSeconds);
        _savedWriteSeconds = 0; // Only the immediate next process interval can contain this write.
        if (!running) { _frames = 0; return; }
        _frames += (seconds - suspended) * TrainerTowerProgressRules.FramesPerSecond;
    }
    internal int Take(bool flush)
    {
        if (!flush && _frames + 1e-8 < TrainerTowerProgressRules.FramesPerSecond) return 0;
        int frames = (int)Math.Min(3600, Math.Floor(_frames + 1e-8));
        _frames = Math.Max(0, _frames - frames);
        return frames;
    }
    internal void ExcludeSaveWrite(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        _savedWriteSeconds += seconds;
    }
    internal void Reset() { _frames = 0; _savedWriteSeconds = 0; }
}
