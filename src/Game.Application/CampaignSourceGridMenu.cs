using Game.Domain.Events;
namespace Game.Application;

public sealed partial class CampaignSession
{
    // Derive the menu from the suspended instruction. No duplicated transient menu/save state.
    public StoryMenu? SourceMenu => StoryMenuRules.Pending(State.Script, _content);
    public StoryMenu? DisplaySourceMenu => SourceMenu ?? StoryListMenuRules.Display(State.Script, _content);
    public StoryGridMenu? SourceGridMenu => StoryGridMenuRules.Pending(State.Script, _content);
}
