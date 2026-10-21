using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Events;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignRocketSpin(string PresentationId,TilePosition From,TilePosition To,RocketSpinState? Forced,RocketSpinPresentationCue? Cue,bool AdvanceFriendship=false);

public sealed partial class CampaignSession
{
    private CampaignState StartRocketArrowArrival(CampaignState previous,CampaignState moved)
    {
        if(previous.World.Player.MapId!=moved.World.Player.MapId||previous.World.Player==moved.World.Player)return moved;
        int behavior=SourceBehavior(moved,moved.World.Player.X,moved.World.Player.Y)??-1;
        if(!RocketSpinRules.IsArrow(behavior))return moved;
        // FieldGetPlayerInput excludes forced-movement tiles before step scripts. The
        // ordinary entry animation finishes before the avatar starts its first forced tile.
        string id="rocket-arrow:"+moved.World.Revision;
        return moved with{RocketSpin=new(id,previous.World.Player,moved.World.Player,null,null),PresentationTicks=16};
    }
    private Decision<CampaignState> DecideRocketSpin(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="rocket_spin_ack"||state.RocketSpin is not {} pending||command.Text!=pending.PresentationId)return new(CommandStatus.Rejected,state,"rocket_spin_presentation_pending");
        var completed=RefreshActors(state with{RocketSpin=null,PresentationTicks=0});
        completed=CheckForestTrainerSight(completed);
        if(completed.ForestTrainerBattle is not null||completed.DoubleBattle is not null)return new(CommandStatus.Accepted,pending.AdvanceFriendship?completed with{World=completed.World with{StepCount=completed.World.StepCount-1}}:completed);
        if(pending.AdvanceFriendship)completed=ApplyWalkingFriendship(ApplyRenewableHiddenItemWalkingStep(completed));
        var world=completed.World with{RulesVersion=ConnectedFieldRules.RulesVersion};
        var intent=new RocketSpinIntent(new("rocket-spin:"+state.World.Revision),world.Revision,pending.Forced?.PendingPresentationId);
        var result=pending.Forced is {} active?RocketSpinRules.CompletePresentation(world,RocketSpinRules.ReconcileCallbackRng(active,world),intent,OccupiedFieldMaps(completed)):RocketSpinRules.Start(world,intent,OccupiedFieldMaps(completed));
        if(result.Status!=TransitionStatus.Accepted)return new(result.Status==TransitionStatus.Rejected?CommandStatus.Rejected:CommandStatus.Fault,state,result.Diagnostic?.Code);
        var next=completed with{World=result.State with{RulesVersion=RuleVersions.GridWorldV0},RocketSpin=null,Motions=[],PresentationTicks=0,PresentationEffects=[]};
        next=RefreshActors(next);

        // The six authenticated maps have no wild encounter table. Their step lifecycle
        // advances the source step services on floor/stop arrival; forced motion
        // must never consume poison/Repel ticks, and arrow cells count no step.
        if(result.ActiveSpin is {} spin)
        {
            var e=result.Events.Single();
            spin=RocketSpinRules.ReconcileCallbackRng(spin,next.World with{RulesVersion=ConnectedFieldRules.RulesVersion});
            next=next with{RocketSpin=new(spin.PendingPresentationId.Value,e.Before,e.After,spin,e.Presentation,result.Callbacks.AdvanceFriendship),PresentationTicks=e.Presentation?.MovementTicks??0};
        }
        else if(result.Events.Any(e=>e.Kind==RocketSpinEventKind.Blocked&&e.Presentation is not null))
        {
            long sequence=checked(next.Script.EffectSequence+1);
            next=next with{Script=next.Script with{EffectSequence=sequence},PresentationEffects=next.PresentationEffects.Add(new(sequence,"playse",next.World.Player.MapId,["SE_M_RAZOR_WIND2"]))};
        }
        return new(CommandStatus.Accepted,next);
    }
}
