using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Application;
public sealed record ReferenceWorldState(WorldSnapshot World, DialogueCursor? Dialogue, string PlayerName, string RivalName);
public sealed record ReferenceWorldCommand(string Kind, Direction Direction = Direction.South);
/// <summary>Bounded source-map exploration. Scripts outside the read-only dialogue profile fault explicitly.</summary>
public sealed class ReferenceWorldSession
{
    public const string Rules="fr-reference-exploration-v0";
    private readonly IWorldMap _map;
    private readonly Func<TilePosition,string?> _interaction;
    private readonly IReadOnlyDictionary<string,ReadOnlyDialogue> _dialogues;
    private readonly CommandSession<ReferenceWorldState,ReferenceWorldCommand> _commands;
    public ReferenceWorldState State=>_commands.State;
    public long Revision=>_commands.Revision;
    public string? VisibleText=>State.Dialogue is {} cursor ?
        _dialogues.Values.Single(d=>d.ScriptId==cursor.ScriptId).Pages[cursor.PageIndex]
            .Replace("{PLAYER}",State.PlayerName,StringComparison.Ordinal).Replace("{RIVAL}",State.RivalName,StringComparison.Ordinal) : null;
    public ReferenceWorldSession(IWorldMap map, TilePosition start,string contentVersion,Func<TilePosition,string?> interaction,IReadOnlyDictionary<string,ReadOnlyDialogue> dialogues,ReferenceWorldState? restored=null)
    {
        _map=map;_interaction=interaction;_dialogues=dialogues;_contentVersion=contentVersion;
        var initial=restored??new(new(RuleVersions.GridWorldV0,0,0,start,new(1)),null,"RED","BLUE");
        var error=ValidateSave(initial);if(error is not null)throw new InvalidDataException(error);
        _commands=new(initial,Rules,contentVersion);
    }
    public string? ValidateSave(ReferenceWorldState state)
    {
        if(state.World is null)return "world_missing";
        var error=WorldRules.ValidateState(state.World,_map);if(error is not null)return error.Code;
        if(state.PlayerName is null||state.RivalName is null||state.PlayerName.Length is <1 or >7||state.RivalName.Length is <1 or >7)return "name_invalid";
        if(state.Dialogue is not null)return "save_event_active";
        return null;
    }
    public CommandOutcome<ReferenceWorldState> Execute(string id,long revision,ReferenceWorldCommand command)=>
        _commands.Execute(new(id,revision,Rules,_contentVersion,command),Decide);
    private readonly string _contentVersion;
    private Decision<ReferenceWorldState> Decide(ReferenceWorldState state,ReferenceWorldCommand command)
    {
        if(command.Kind=="move")
        {
            if(state.Dialogue is not null)return new(CommandStatus.Rejected,state,"dialogue_active");
            var moved=WorldRules.Move(state.World,new(new("world:"+state.World.Revision),state.World.Revision,command.Direction),_map);
            return moved.Status switch{TransitionStatus.Accepted=>new(CommandStatus.Accepted,state with{World=moved.State}),TransitionStatus.Rejected=>new(CommandStatus.Rejected,state,moved.Diagnostic?.Code),_=>new(CommandStatus.Fault,state,moved.Diagnostic?.Code)};
        }
        if(command.Kind=="interact")
        {
            if(state.Dialogue is {} cursor)
            {
                var program=_dialogues.Values.Single(x=>x.ScriptId==cursor.ScriptId);
                return new(CommandStatus.Accepted,state with{Dialogue=cursor.PageIndex+1<program.Pages.Length?cursor with{PageIndex=cursor.PageIndex+1}:null});
            }
            var script=_interaction(state.World.Player);
            if(script is null)return new(CommandStatus.Rejected,state,"nothing_to_interact");
            if(!_dialogues.TryGetValue(script,out var dialogue))return new(CommandStatus.Fault,state,"unsupported_script:"+script);
            return new(CommandStatus.Accepted,state with{Dialogue=new(dialogue.ScriptId,0)});
        }
        return new(CommandStatus.Rejected,state,"unknown_command");
    }
}
