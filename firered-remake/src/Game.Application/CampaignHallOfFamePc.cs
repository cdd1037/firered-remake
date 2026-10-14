using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

/// <summary>Transient native callback, nested in the existing unsavable PC state.</summary>
public sealed record CampaignHallOfFamePc(HallOfFamePcCursor? Cursor, string PreviousHelpContext, bool ReturnPending = false);
public sealed record CampaignHallOfFamePcMember(CampaignHallOfFamePokemon Snapshot, HallOfFamePcPosition Position, bool Selected)
{
    public bool ShowPokemonDetails => Snapshot.SpeciesOrEgg != HallOfFamePcRules.EggSpecies;
    public bool PlayCry => Selected && ShowPokemonDetails;
    public ushort DisplayTrainerId => (ushort)Snapshot.OriginalTrainerId;
}
public sealed record CampaignHallOfFamePcView(HallOfFamePcCursor Cursor, string HelpTextSymbol,
    CampaignHallOfFameTeam? Team, ImmutableArray<CampaignHallOfFamePcMember> Members)
{
    public string? ErrorTextSymbol => Cursor.Corrupted ? "gText_HOFCorrupted" : null;
}

/// <summary>Projection of recorded teams only. Today's party, nickname, level, form,
/// ribbon and location are never substituted for the historical snapshot.</summary>
public static class CampaignHallOfFamePcProjection
{
    public static ImmutableArray<int> MemberCounts(CampaignHallOfFameArchive? archive) => Readable(archive)
        ? archive!.Teams.Select(t => t.Members.Length).ToImmutableArray() : [];

    public static bool Readable(CampaignHallOfFameArchive? archive)
    {
        if (archive is null || archive.Teams.IsDefault || archive.Teams.Length is < 1 or > HallOfFamePcRules.MaximumTeams) return false;
        foreach (var team in archive.Teams)
        {
            if (team is null || team.Members.IsDefault || team.Members.Length is < 1 or > 6) return false;
            for (int i = 0; i < team.Members.Length; i++)
            {
                var member = team.Members[i];
                if (member is null || member.PartySlot != i || member.SpeciesOrEgg is < 1 or > HallOfFamePcRules.EggSpecies ||
                    member.Level is < 1 or > 100 || member.Nickname is null || member.Nickname.Length is < 1 or > 10 ||
                    member.NicknameBytes.IsDefault || member.NicknameBytes.Length != 10) return false;
            }
        }
        return true;
    }

    public static HallOfFamePcCursor Open(CampaignHallOfFameArchive? archive, int enteredHallCount) =>
        HallOfFamePcRules.Open(MemberCounts(archive), enteredHallCount, Readable(archive));

    public static CampaignHallOfFamePcView Create(CampaignHallOfFameArchive? archive, HallOfFamePcCursor cursor)
    {
        if (cursor.Corrupted) return new(cursor, HallOfFamePcRules.CorruptedHelpSymbol, null, []);
        _ = HallOfFamePcRules.Apply(cursor, MemberCounts(archive), HallOfFamePcButtons.None);
        var team = archive!.Teams[cursor.TeamIndex];
        return new(cursor, HallOfFamePcRules.HelpSymbol(cursor), team, team.Members.Select((member, index) =>
            new CampaignHallOfFamePcMember(member, HallOfFamePcRules.Position(team.Members.Length, index), index == cursor.MemberIndex)).ToImmutableArray());
    }
}

public sealed partial class CampaignSession
{
    private const string HallPcSourceEntry = "EventScript_AccessHallOfFame";
    public bool HallOfFamePcEnabled => _content.PokemonStorageEnabled && _content.PokemonLeagueEnabled;
    public CampaignHallOfFamePcView? HallOfFamePcView => State.Pc is { View: CampaignPcView.HallOfFame, HallOfFame.Cursor: {} cursor }
        ? CampaignHallOfFamePcProjection.Create(State.HallOfFame, cursor) : null;

    private bool TryApplyHallOfFamePcEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Source.Path != CenterPcSource || effect.Source.Symbol != HallPcSourceEntry || effect.Opcode != "special") return false;
        if (effect.Args[0] is not ("BackupHelpContext" or "Script_SetHelpContext" or "HallOfFamePCBeginFade" or "RestoreHelpContext")) return false;
        if (!HallOfFamePcEnabled || !state.PcPoweredOn || !state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR") ||
            !IsSupportedHealingCenter(state.World.Player.MapId)) throw new InvalidDataException("hall_pc_source_context_invalid");
        switch (effect.Args[0])
        {
            case "BackupHelpContext":
                if (state.Pc is not null || state.HelpContext != "HELPCONTEXT_PC") throw new InvalidDataException("hall_pc_help_backup_invalid");
                next = state with { Pc = new(CampaignPcView.HallOfFame) { HallOfFame = new(null, state.HelpContext) } };
                return true;
            case "Script_SetHelpContext":
                if (state.Pc?.HallOfFame is not { Cursor: null, ReturnPending: false } ||
                    !_content.TryConstant("HELPCONTEXT_PC_MISC", out int context) || state.Script.Variables.GetValueOrDefault("VAR_0x8004") != context)
                    throw new InvalidDataException("hall_pc_help_context_invalid");
                next = state with { HelpContext = "HELPCONTEXT_PC_MISC" }; return true;
            case "HallOfFamePCBeginFade":
                if (state.Pc is not { HallOfFame: { Cursor: null, ReturnPending: false } pending } pc || state.HelpContext != "HELPCONTEXT_PC_MISC")
                    throw new InvalidDataException("hall_pc_entry_context_invalid");
                next = state with { Pc = pc with { HallOfFame = pending with { Cursor = CampaignHallOfFamePcProjection.Open(state.HallOfFame,
                    state.GameStats?.GetValueOrDefault("GAME_STAT_ENTERED_HOF") ?? 0) } }, Dialogue = null };
                return true;
            case "RestoreHelpContext":
                if (state.Pc is not { View: CampaignPcView.Main, HallOfFame: { ReturnPending: true, PreviousHelpContext: "HELPCONTEXT_PC" } returned } ||
                    state.HelpContext != "HELPCONTEXT_PC_MISC") throw new InvalidDataException("hall_pc_help_restore_invalid");
                next = state with { Pc = null, HelpContext = returned.PreviousHelpContext }; return true;
        }
        return false;
    }

    private Decision<CampaignState> DecideHallOfFamePc(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!HallOfFamePcEnabled || state.Pc is not { View: CampaignPcView.HallOfFame, HallOfFame: { Cursor: {} cursor, ReturnPending: false } pending } pc ||
            state.HelpContext != "HELPCONTEXT_PC_MISC" || !state.Script.Flags.GetValueOrDefault("FLAG_SYS_GAME_CLEAR")) return No("hall_pc_callback_context_invalid");
        if (command.Kind != "pc_hall_input" || command.Choice is not {} keys || keys < 0 || (keys & ~195) != 0)
            return No("hall_pc_input_required");
        var transition = HallOfFamePcRules.Apply(cursor, CampaignHallOfFamePcProjection.MemberCounts(state.HallOfFame), (HallOfFamePcButtons)keys);
        if (!transition.Exit)
            return new(CommandStatus.Accepted, transition.SelectionChanged ? state with { Pc = pc with { HallOfFame = pending with { Cursor = transition.Cursor } } } : state);
        // src/hof_pc.c recreates the PC menu while the original waitstate remains
        // suspended. Its next selection resumes RestoreHelpContext and then ChoosePCMenu.
        return new(CommandStatus.Accepted, state with { Pc = pc with { View = CampaignPcView.Main, HallOfFame = pending with { ReturnPending = true } },
            Dialogue = new("Text_AccessWhichPC", _content.TextPages("Text_AccessWhichPC"), 0, false, false),
            Motions = [], PresentationTicks = 0, PresentationEffects = [] });
    }
}
