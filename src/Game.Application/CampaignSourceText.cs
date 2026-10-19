using System.Text.RegularExpressions;

namespace Game.Application;

/// <summary>Source pages are prepared before their dynamic values are bound. Final text stays literal.</summary>
public static class CampaignSourceText
{
    private static readonly Regex Parameter = new(@"\{([^{}]+)\}", RegexOptions.CultureInvariant);

    public static string Bind(string template, Func<string, string?> resolve) =>
        Parameter.Replace(template, match => resolve(match.Groups[1].Value) ?? match.Value);

    public static string Bind(string template, CampaignState state) => Bind(template, token => token switch
    {
        "PLAYER" => state.PlayerName,
        "RIVAL" => state.RivalName,
        // Both English Kun/Chan expansions are empty in the original string table.
        "KUN" => "",
        _ => state.TextBuffers.GetValueOrDefault(token)
    });

    public static string DisplayPage(string template, CampaignState state) => Bind(PrepareDisplayPage(template), state);

    public static string PrepareDisplayPage(string template)
    {
        // Known field presentation cues, consumed only from the source template.
        // Field audio still reads the untouched Dialogue.Pages; unknown tokens stay visible.
        foreach (string token in new[] { "{FONT_NORMAL}", "{FONT_MALE}", "{PLAY_BGM MUS_ENCOUNTER_GYM_LEADER}",
            "{PLAY_BGM MUS_ENCOUNTER_ROCKET}", "{PLAY_BGM}", "{MUS_ENCOUNTER_GYM_LEADER}",
            "{MUS_ENCOUNTER_ROCKET}", "{PAUSE_MUSIC}", "{MUS_OBTAIN_BADGE}", "{PAUSE 0xFE}",
            "{PAUSE 0x56}", "{RESUME_MUSIC}" })
            template = template.Replace(token, "", StringComparison.Ordinal);
        return template;
    }
}
