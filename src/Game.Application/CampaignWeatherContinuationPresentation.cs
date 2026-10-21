using Game.Domain.Battle;

namespace Game.Application;

public sealed record CampaignWeatherContinuation(long Sequence, long ReceiptSequence, BattlerSlot Controller, string Weather);

/// <summary>One page owns each committed continuation. Temporary weather retains its
/// countdown text; permanent weather uses the standalone typed continuation receipt.</summary>
public static class CampaignWeatherContinuationPresentation
{
    private sealed record Row(long Sequence, int Turn, string Kind, int Slot = -1, string Weather = "",
        int Before = 0, int After = 0, string Reason = "");

    public static CampaignWeatherContinuation? At(IReadOnlyList<SourceBattleEvent> events, int index) =>
        Match(events.Select(Read).ToArray(), index, 4);

    private static CampaignWeatherContinuation? Match(Row[] rows, int index, int count)
    {
        if (index < 0 || index >= rows.Length) return null;
        var current = rows[index]; Row continuation; int receiptIndex;
        if (current.Kind == "timer" && Weather(current.Weather))
        {
            if (!Countdown(current) || index + 1 >= rows.Length) return null;
            var next = rows[index + 1];
            if (next.Kind != "continuation" || next.Sequence != current.Sequence + 1 || next.Turn != current.Turn ||
                next.Weather != current.Weather) return null;
            continuation = next; receiptIndex = index + 1;
        }
        else if (current.Kind == "continuation")
        {
            // A preceding weather timer owns its own text. Do not reinterpret a
            // malformed/mismatched pair as a standalone permanent continuation.
            if (index > 0 && rows[index - 1] is { Kind: "timer" } previous &&
                previous.Turn == current.Turn && Weather(previous.Weather)) return null;
            continuation = current; receiptIndex = index;
        }
        else return null;
        if (!Weather(continuation.Weather) || continuation.Sequence < 0 || continuation.Turn < 0 ||
            continuation.Slot < 0 || continuation.Slot >= count) return null;
        for (int i = 1; i <= receiptIndex; i++) if (rows[i].Sequence <= rows[i - 1].Sequence) return null;
        if (rows.Take(receiptIndex).Any(e => e.Kind == "continuation" &&
            e.Turn == continuation.Turn && e.Weather == continuation.Weather)) return null;
        return new(current.Sequence, continuation.Sequence, new(continuation.Slot), continuation.Weather);
    }

    public static string ContinuationSymbol(string weather) => weather switch {
        "rain" => "sText_RainContinues", "sandstorm" => "sText_SandstormRages",
        "sun" => "sText_SunlightStrong", "hail" => "sText_HailContinues",
        _ => throw new ArgumentOutOfRangeException(nameof(weather), weather, "Unknown weather continuation.")
    };
    private static bool Weather(string value) => value is "rain" or "sandstorm" or "sun" or "hail";
    private static bool Countdown(Row timer) => timer.Slot == 0 && timer.Sequence >= 0 && timer.Turn >= 0 &&
        timer.Reason == "countdown" && timer.Before is >= 2 and <= 5 && timer.After == timer.Before - 1;
    private static Row Read(SourceBattleEvent e) => e switch {
        SourceMtMoonVolatileEvent x => new(x.Sequence, x.Turn, "timer", x.Side.Value, x.Volatile, x.Before, x.After, x.Reason),
        SourceWeatherContinuedEvent x => new(x.Sequence, x.Turn, "continuation", x.Controller.Value, x.Weather),
        _ => new(e.Sequence, e.Turn, "boundary")
    };
}
