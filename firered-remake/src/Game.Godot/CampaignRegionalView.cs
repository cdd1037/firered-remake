using Godot;

public partial class CampaignView
{
    private static bool RegionalServicesProfileRequested => OS.GetEnvironment("FIRERED_REGIONAL_SERVICES")=="1" || OS.GetCmdlineUserArgs().Contains("--story-regional-services");
    private static bool RegionalProfileRequested => RegionalServicesProfileRequested || OS.GetEnvironment("FIRERED_REGIONAL")=="1" || OS.GetCmdlineUserArgs().Contains("--story-regional");
}
