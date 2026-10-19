using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Events;
using Game.Domain.Pokemon;

/// <summary>Presentation-only Summary choices. No mode can issue a game command.</summary>
public enum SummaryInteractionMode { ReadOnly, Reorder, Replace, Delete, SelectNew }

public sealed record SummaryMoveSelection(SummaryInteractionMode Mode, int Cursor, int SwapSource = -1, MoveId? NewMove = null,
    ImmutableArray<MoveId> Candidates = default, int FirstRow = 0)
{
    public bool CanReorder => Mode == SummaryInteractionMode.Reorder;
    public bool Swapping => CanReorder && SwapSource >= 0;
    public int RowCount(int knownMoves) => Mode == SummaryInteractionMode.SelectNew ? Candidates.Length + 1 : knownMoves + (Swapping ? 0 : 1);
    public MoveId? SelectedMove(ImmutableArray<PokemonMoveSlot> moves) => Mode == SummaryInteractionMode.SelectNew
        ? Cursor >= 0 && Cursor < Candidates.Length ? Candidates[Cursor] : null
        : Cursor >= 0 && Cursor < moves.Length
        ? moves[Cursor].Move : Cursor == moves.Length && Mode == SummaryInteractionMode.Replace ? NewMove : null;
    public bool IsCancel(int knownMoves) => Cursor == (Mode == SummaryInteractionMode.SelectNew ? Candidates.Length : knownMoves) && !Swapping;
}

/// <summary>Reopening is required if a menu's authority, identity or revision changes.</summary>
public sealed record SummarySelectionStamp(string Context, long Revision, long InstanceId)
{
    // Only the accepted play-time-only command path may rebase an already-current draft.
    public SummarySelectionStamp AfterPlayTimeOnlyCommit(long before, long after) =>
        Revision == before ? this with { Revision = after } : this;
    public bool Matches(string context, long revision, long instanceId) =>
        Context == context && Revision == revision && InstanceId == instanceId;
}

public enum SummaryPartyAction { Summary, Cancel, Shift, SendOut, Store }
public readonly record struct SummaryPartyMenuEntry(SummaryPartyAction Id, string Label);

/// <summary>Shared Summary screen and battle/Day-Care popup text from the already-loaded source bank.</summary>
internal sealed class SourceSummaryText
{
    private readonly ImmutableDictionary<SummaryPartyAction, string> _labels;
    internal string ChoosePrompt { get; }
    internal string ActionPrompt { get; }
    private readonly string[] _pageTitles;
    private readonly ImmutableArray<ImmutableArray<string>> _eggHatchLines, _eggOriginLines;
    private readonly string _pick, _place, _switch;
    internal string Next { get; }
    internal string Egg { get; }
    internal string Experience { get; }
    internal string NextLevel { get; }
    internal string PageTitle(int page, bool egg) => _pageTitles[egg ? 0 : page];
    internal string Control(SummaryMoveSelection selection) => selection.Mode switch
    {
        SummaryInteractionMode.ReadOnly => "",
        SummaryInteractionMode.Reorder => selection.Swapping ? _place : _switch,
        _ => _pick
    };

    internal ImmutableArray<string> EggHatchLines(int friendship) => _eggHatchLines[friendship switch
    { <= 5 => 0, <= 10 => 1, <= 40 => 2, _ => 3 }];
    internal ImmutableArray<string> EggOriginLines(bool fateful, bool currentOwner, int metGame, bool giftEgg) =>
        _eggOriginLines[fateful ? 0 : metGame is not (4 or 5) || !currentOwner ? 1 : giftEgg ? 2 : 3];

    internal SourceSummaryText(IReadOnlyDictionary<string, string> source)
    {
        string Plain(string symbol, bool pokemonGlyph = false, string? prefix = null)
        {
            if (!source.TryGetValue(symbol, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException("Missing Summary party text: " + symbol);
            if (prefix is not null)
            {
                if (!value.StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidDataException("Missing Summary text button prefix: " + symbol);
                value = value[prefix.Length..];
            }
            // The existing party surface spells out the source's compact PKMN glyph.
            if (pokemonGlyph) value = value.Replace("{PKMN}", "POKéMON", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['{', '}', '\\', '\r', '\n']) >= 0)
                throw new InvalidDataException("Unsupported Summary party text control: " + symbol);
            return value;
        }
        _labels = Enum.GetValues<SummaryPartyAction>().ToImmutableDictionary(action => action,
            action => Plain(action switch
            {
                SummaryPartyAction.Summary => "gText_Summary5",
                SummaryPartyAction.Cancel => "gFameCheckerText_Cancel",
                SummaryPartyAction.Shift => "gText_Shift",
                SummaryPartyAction.SendOut => "gText_SendOut",
                SummaryPartyAction.Store => "gText_Store",
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            }));
        ChoosePrompt = Plain("gText_ChoosePokemon2");
        ActionPrompt = Plain("gText_DoWhatWithPokemon", pokemonGlyph: true);
        _pageTitles = [Plain("gText_PokeSum_PageName_PokemonInfo"), Plain("gText_PokeSum_PageName_PokemonSkills"),
            Plain("gText_PokeSum_PageName_KnownMoves")];
        Experience = Plain("gText_PokeSum_ExpPoints");
        NextLevel = Plain("gText_PokeSum_NextLv");
        Egg = Plain("gText_EggNickname");
        Next = Plain("gText_Next", prefix: "{A_BUTTON}");
        _pick = Plain("gText_PokeSum_Controls_Pick", prefix: "{DPAD_UPDOWN}");
        _place = Plain("gPCText_Place");
        _switch = Plain("gText_Switch2");
        ImmutableArray<string> Lines(string symbol, int count)
        {
            if (!source.TryGetValue(symbol, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException("Missing Summary egg text: " + symbol);
            var pages = SourceBattleText.Pages(source, symbol, ImmutableDictionary<string, string>.Empty);
            if (pages.Length != 1) throw new InvalidDataException("Unsupported Summary egg pages: " + symbol);
            var lines = pages[0].Split('\n').ToImmutableArray();
            if (lines.Length != count || lines.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Unsupported Summary egg lines: " + symbol);
            return lines;
        }
        _eggHatchLines = [Lines("gText_PokeSum_EggHatch_AlmostReady", 3), Lines("gText_PokeSum_EggHatch_Soon", 3),
            Lines("gText_PokeSum_EggHatch_SomeTime", 3), Lines("gText_PokeSum_EggHatch_LongTime", 3)];
        _eggOriginLines = [Lines("gText_PokeSum_EggOrigin_NicePlace", 2), Lines("gText_PokeSum_EggOrigin_Trade", 2),
            Lines("gText_PokeSum_EggOrigin_TravelingMan", 2), Lines("gText_PokeSum_EggOrigin_DayCare", 2)];
    }

    internal string Label(SummaryPartyAction action) => _labels[action];
}

public static class SummaryInteractionPresentationAssets
{
    public const int MoveX = 120, MoveY = 18, MoveWidth = 120, MoveHeight = 28;
    public static SummaryPartyMenuEntry[] BattlePartyActions(int partyCount, bool egg, bool sendOut,
        Func<SummaryPartyAction, string> label)
    {
        SummaryPartyAction[] actions = partyCount == 1 || egg
            ? [SummaryPartyAction.Summary, SummaryPartyAction.Cancel]
            : [sendOut ? SummaryPartyAction.SendOut : SummaryPartyAction.Shift, SummaryPartyAction.Summary, SummaryPartyAction.Cancel];
        return actions.Select(id => new SummaryPartyMenuEntry(id, label(id))).ToArray();
    }
    public static SummaryPartyMenuEntry[] DayCarePartyActions(bool egg, Func<SummaryPartyAction, string> label)
    {
        SummaryPartyAction[] actions = egg ? [SummaryPartyAction.Summary, SummaryPartyAction.Cancel]
            : [SummaryPartyAction.Store, SummaryPartyAction.Summary, SummaryPartyAction.Cancel];
        return actions.Select(id => new SummaryPartyMenuEntry(id, label(id))).ToArray();
    }
    public static SummaryPartyAction? SelectedPartyAction(SummaryPartyMenuEntry[] actions, int cursor, bool confirm, bool cancel)
        => cancel ? SummaryPartyAction.Cancel : confirm ? actions[cursor].Id : null;
    public static int MoveRowAt(float x, float y, int rows) => x >= MoveX && x < MoveX + MoveWidth &&
        y >= MoveY && y < Math.Min(160, MoveY + rows * MoveHeight) ? (int)(y - MoveY) / MoveHeight : -1;
}
