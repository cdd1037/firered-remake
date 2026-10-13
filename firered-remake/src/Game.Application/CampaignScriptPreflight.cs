using System.Collections.Immutable;
using Game.Domain.Events;
namespace Game.Application;

public sealed partial class CampaignSession
{
    // CommandSession serializes Decide. This transient guard prevents recursive preflight;
    // all probed game data is immutable and never published or saved.
    private bool _preflightingStory;
    private CampaignState StartStory(CampaignState state,string program)
    {
        if(_content.ViridianGymEnabled&&!_content.LeagueRivalEnabled&&program.StartsWith("Route22_EventScript_LateRival",StringComparison.Ordinal)&&state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE22")==3)
            throw new InvalidDataException("route22_league_rival_not_yet_available");
        if(_content.NationalDexEnabled&&!_content.SeviiRubyEnabled&&NationalDexEnabledIn(state)&&program=="OneIsland_PokemonCenter_1F_EventScript_Celio")
            throw new InvalidDataException("sevii_ruby_campaign_not_yet_available");
        if(_content.DeferredRegionalServiceReason(state.World.Player.MapId,program) is {} unavailable)throw new InvalidDataException(unavailable);
        if(_content.ConnectedFieldMaps is not null&&!_preflightingStory&&_commands is not null)PreflightStoryEntry(state,program);
        return Advance(state,StoryRules.Start(state.Script,program,_content));
    }
    private void PreflightStoryEntry(CampaignState state,string program) =>
        PreflightStoryPath(()=>Pump(Advance(state,StoryRules.Start(state.Script,program,_content))),program);

    private void PreflightOakPcEntry(CampaignState state,int row) =>
        PreflightStoryPath(()=>ResumePcScript(state,row).State,"EventScript_AccessProfOaksPC");

    private void PreflightStoryPath(Func<CampaignState> enter,string program)
    {
        _preflightingStory=true;
        try
        {
            var pending=new Queue<CampaignState>();pending.Enqueue(enter());
            var billMenuStates=new HashSet<string>(StringComparer.Ordinal);
            int steps=0,budget=512;
            while(pending.TryDequeue(out var probe))
            {
                if(++steps>budget)throw new InvalidDataException("script_preflight_budget: "+program);
                // These are already-owned application workflows with separate admission contracts.
                if(probe.PokemonLeagueCompletion is not null||probe.Seagallop is not null||probe.Pc is not null||probe.Shop is not null||probe.DoubleBattle is not null||probe.ForestTrainerBattle is not null||probe.Battle is not null||probe.WildBattle is not null||probe.OldManTutorial is not null||probe.ItemMenu is not null||probe.Nickname is not null||probe.NameRater is not null)continue;
                if(probe.Script.Completed&&probe.Script.Wait is null)continue;
                if(probe.Script.Wait is not {} wait)throw new InvalidDataException("script_preflight_unstable: "+program);
                bool billMenu=IsBillPreflightMenu(probe);
                bool finiteLoop=billMenu||RainbowFerryMenu(probe)||_content.SeviiRubyEnabled&&probe.Dialogue?.TextSymbol=="OneIsland_PokemonCenter_1F_Text_PleaseINeedYourHelp";
                if(finiteLoop)
                {
                    // The source returns to this menu after each sighting. Keep every
                    // business field, including the complete seen set, scratch variables,
                    // inventory, actors and caller stack. Only monotonically increasing
                    // interpreter/presentation identifiers are irrelevant to convergence.
                    if(billMenu)budget=8192;
                    var semantic=probe with
                    {
                        Script=probe.Script with{Revision=0,EffectSequence=0,Wait=wait with{CorrelationId="preflight"}},
                        PresentationEffects=probe.PresentationEffects.Select(e=>e with{Sequence=0}).ToImmutableArray()
                    };
                    if(!billMenuStates.Add(CanonicalJson.Hash(semantic)))continue;
                }
                // Decide advances non-final text pages before inspecting a choice.
                // Forking here would enqueue identical states once per answer on
                // every page, making a long yes/no message grow exponentially.
                bool moreText=wait.Kind is StoryWaitKind.Dialog or StoryWaitKind.Choice or StoryWaitKind.MenuChoice or StoryWaitKind.MessagePrinting&&
                    probe.Dialogue is {} dialogue&&dialogue.Page+1<dialogue.Pages.Length;
                foreach(var choice in moreText?new int?[]{null}:billMenu?new int?[]{0,1,2,3,4,127}:probe.SeagallopMenu is {} ferryMenu?Enumerable.Range(0,ferryMenu.Options.Length).Select(i=>(int?)i).Append(127).ToArray():SilphMenuPending(probe)?new int?[]{0,1,2,3,4,5,6,7,8,9,10,11,127}:probe.DepartmentElevator?.MenuDefault is not null?new int?[]{0,1,2,3,4,5,127}:probe.RocketElevator?.MenuDefault is not null?new int?[]{0,1,2,3,127}:wait.Kind==StoryWaitKind.MenuChoice?new int?[]{0,1,127}:wait.Kind==StoryWaitKind.Choice?new int?[]{0,1}:new int?[]{null})
                {
                    var decision=Decide(probe,SilphMenuPending(probe)?new("silph_elevator_choice",Choice:choice):wait.Kind==StoryWaitKind.ButtonPress?new("button_press",Text:"A"):new("ack",Choice:choice));
                    // A declared unavailable Yes option must not block the original No
                    // branch or entering its choice screen. The public Yes command still
                    // rejects atomically until the first-Sevii workflow is admitted.
                    if(decision.Status==CommandStatus.Rejected&&wait.Kind==StoryWaitKind.Choice&&choice==1&&
                        CinnabarFerryChoice(probe)&&decision.Error=="sevii_first_visit_not_yet_available")continue;
                    if(decision.Status==CommandStatus.Rejected&&RainbowFerryMenu(probe)&&decision.Error=="seagallop_destination_not_yet_available")continue;
                    if(decision.Status!=CommandStatus.Accepted)throw new InvalidDataException("script_preflight: "+program+": "+decision.Error);
                    pending.Enqueue(decision.State);
                }
            }
            // The throwaway interpreter advances script revisions, but it deliberately does
            // not commit outer CommandSession revisions. Reserve those acknowledgements too.
            if(Revision>long.MaxValue-steps-1)throw new InvalidDataException("script_command_headroom_required: "+program);
        }
        finally{_preflightingStory=false;}
    }

    private bool IsBillPreflightMenu(CampaignState state)
    {
        if(!_content.BillEnabled||state.World.Player.MapId.Value!=BillSourcePresentation.Cottage||
            state.Script.Wait?.Kind!=StoryWaitKind.MenuChoice||state.Script.Stack.IsEmpty)return false;
        var frame=state.Script.Stack[^1];var program=_content.FindProgram(frame.ProgramId);int index=frame.InstructionIndex-1;
        if(program is null||index<0||index>=program.Instructions.Length)return false;
        var instruction=program.Instructions[index];
        return instruction.Op==StoryOp.Multichoice&&instruction.Source.Path==BillCottageSource&&instruction.Source.Symbol==BillMenu&&
            instruction.Args.SequenceEqual(new[]{"0","0","MULTICHOICE_EEVEELUTIONS","FALSE"});
    }
}
