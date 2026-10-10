using Godot;

public partial class CampaignView
{
    private static bool RegionalProfileRequested => OS.GetEnvironment("FIRERED_REGIONAL")=="1" || OS.GetCmdlineUserArgs().Contains("--story-regional");
}
