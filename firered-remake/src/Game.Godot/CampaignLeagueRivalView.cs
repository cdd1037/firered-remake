using Godot;

public partial class CampaignView
{
    private static bool LeagueRivalProfileRequested => VictoryRoadProfileRequested || OS.GetEnvironment("FIRERED_LEAGUE_RIVAL") == "1" ||
        OS.GetCmdlineUserArgs().Contains("--story-league-rival");
}
