using Game.Domain.Trainers;

/// <summary>Source multichoice geometry and online 60 Hz batching, independent of Godot.</summary>
internal sealed class TrainerTowerMenuPresentation : SourceVerticalMenuPresentation
{
    internal string SourceSymbol { get; }
    internal TrainerTowerMenuPresentation(string sourceSymbol, IEnumerable<string> options, int defaultCursor = 0)
        : base(Validate(sourceSymbol, options), sourceSymbol == "MULTICHOICE_ROOFTOP_B1F" ? 0 : sourceSymbol == "MULTICHOICE_YES_NO_INFO" ? 18 : 13,
            sourceSymbol == "MULTICHOICE_ROOFTOP_B1F" ? 0 : sourceSymbol == "MULTICHOICE_YES_NO_INFO" ? 6 : 3, defaultCursor)
    { SourceSymbol = sourceSymbol; }
    private static string[] Validate(string sourceSymbol, IEnumerable<string> options)
    {
        int count = sourceSymbol switch
        {
            "MULTICHOICE_YES_NO_INFO" => 3,
            "MULTICHOICE_TRAINER_TOWER_MODE" => 5,
            "MULTICHOICE_ROOFTOP_B1F" => 3,
            _ => throw new InvalidDataException("Trainer Tower source menu differs.")
        };
        var labels = options.ToArray();
        if (labels.Length != count || labels.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Trainer Tower menu rows differ.");
        return labels;
    }
    internal static string ElevatorFloorTextSymbol(int floor) => floor switch
    { 3 => "gText_B1F", 15 => "gText_Rooftop", _ => throw new InvalidDataException("Trainer Tower elevator floor differs.") };
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
