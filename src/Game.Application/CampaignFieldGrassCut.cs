using System.Collections.Immutable;
using Game.Domain.World;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private (Game.Domain.Ids.MapId Map,int X,int Y)? ResolveGrassCell(CampaignState state,int x,int y)=>
        _content.ConnectedFieldMaps is {} maps?ConnectedFieldCoordinates.Resolve(maps,state.World.Player.MapId,x,y):null;
    private FieldMetatileEdit? OriginalGrassCell(CampaignState state,int x,int y)=>ResolveGrassCell(state,x,y) is {} cell&&
        _content.SourceMetatileAt(cell.Map,cell.X,cell.Y) is {} original?original with{X=x,Y=y}:null;
    private FieldMetatileDefinition? GrassDefinition(CampaignState state,FieldMetatileEdit edit)=>ResolveGrassCell(state,edit.X,edit.Y) is {} cell?
        _content.MetatileDefinition(cell.Map,edit.SourceMetatileId):null;
    private static bool InSavedGrassView(FieldMetatileEdit edit,TilePosition player)=>
        edit.X>=player.X-7&&edit.X<=player.X+7&&edit.Y>=player.Y-7&&edit.Y<=player.Y+6;
    private CampaignState CarryGrassAcrossConnection(CampaignState before,CampaignState after)
    {
        if(before.MapEdits is not {Count:>0} edits)return after;
        var old=before.World.Player;var now=after.World.Player;
        var (dx,dy)=now.Facing switch{Direction.North=>(0,-1),Direction.South=>(0,1),Direction.West=>(-1,0),_=>(1,0)};
        int shiftX=now.X-old.X-dx,shiftY=now.Y-old.Y-dy;
        var kept=after.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
        foreach(var edit in edits.Values.Where(e=>IsCutGrassEdit(before,e)&&InSavedGrassView(e,old)))
        {
            var translated=edit with{X=edit.X+shiftX,Y=edit.Y+shiftY};
            // MoveMapViewToBackup drops the row/column scrolled off the saved viewport.
            if(InSavedGrassView(translated,now)&&IsCutGrassEdit(after,translated))kept=kept.SetItem(translated.X+":"+translated.Y,translated);
        }
        return after with{MapEdits=kept.Count==0?after.MapEdits:kept};
    }
    private IEnumerable<FieldMetatileEdit> NearbyCutGrass(CampaignState state)
    {
        var p = state.World.Player;
        for (int y=p.Y-1;y<=p.Y+1;y++)
        for (int x=p.X-1;x<=p.X+1;x++)
        {
            var cell=state.MapEdits?.GetValueOrDefault(x+":"+y)??OriginalGrassCell(state,x,y);
            if(cell is not null&&cell.Elevation==p.Elevation&&GrassDefinition(state,cell)?.Terrain==1)
                yield return cell;
        }
    }
    private Decision<CampaignState> StartFieldGrassCut(CampaignState state,int partyIndex)
    {
        var mon=state.Party[partyIndex];var stats=state.GameStats??ImmutableSortedDictionary<string,int>.Empty;
        return new(CommandStatus.Accepted,state with
        {
            CutEffect=new(partyIndex,mon.Pokemon.SourceSpeciesId,mon.Nickname,"",FieldCutSourcePresentation.ShowMon)
                {Grass=true,WaitId="cut-grass:"+checked(Revision+1)+":show"},
            GameStats=stats.SetItem(CutUsageStat,Math.Min(0xffffff,stats.GetValueOrDefault(CutUsageStat)+1)),
            Script=state.Script with{Locked=true},PresentationTicks=FieldCutSourcePresentation.ShowMonCueFrames,PresentationEffects=[],Motions=[]
        });
    }
    private Decision<CampaignState> CompleteFieldGrassCut(CampaignState state,CampaignCommand command)
    {
        if(state.CutEffect is not {Grass:true} cut)return new(CommandStatus.Rejected,state,"field_cut_grass_not_active");
        if(command.Kind!="field_cut_grass_ack")return new(CommandStatus.Rejected,state,"field_cut_event_active");
        if(command.Text!=cut.WaitId)return new(CommandStatus.Rejected,state,"field_cut_callback_mismatch");
        if(cut.Phase==FieldCutSourcePresentation.ShowMon)
        {
            var edits=state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty;
            foreach(var cell in NearbyCutGrass(state))
                if(_content.CutGrassMetatiles.TryGetValue(cell.SourceMetatileId,out int replacement))
                    // Source keeps elevation but clears collision when writing the replacement ID.
                    edits=edits.SetItem(cell.X+":"+cell.Y,cell with{SourceMetatileId=replacement,Collision=0});
            long sequence=checked(state.Script.EffectSequence+1);
            return new(CommandStatus.Accepted,state with
            {
                MapEdits=edits,CutEffect=cut with{Phase=FieldCutSourcePresentation.GrassCut,WaitId="cut-grass:"+checked(Revision+1)+":scatter"},
                Script=state.Script with{EffectSequence=sequence},PresentationTicks=FieldCutSourcePresentation.GrassCutCueFrames,
                PresentationEffects=[new(sequence,"playse",state.World.Player.MapId,["SE_M_CUT"])]
            });
        }
        if(cut.Phase!=FieldCutSourcePresentation.GrassCut)return new(CommandStatus.Rejected,state,"field_cut_phase_invalid");
        return new(CommandStatus.Accepted,state with{CutEffect=null,PresentationTicks=0,PresentationEffects=[],Script=state.Script with{Locked=false}});
    }
    private bool IsCutGrassEdit(CampaignState state,FieldMetatileEdit edit)
    {
        var original=OriginalGrassCell(state,edit.X,edit.Y);
        return original is not null&&original.Elevation==edit.Elevation&&edit.Collision==0&&
            GrassDefinition(state,original)?.Terrain==1&&
            _content.CutGrassMetatiles.TryGetValue(original.SourceMetatileId,out int replacement)&&replacement==edit.SourceMetatileId;
    }
    // The shared edit layer admits source grass replacements without weakening puzzle contracts.
    private CampaignState WithoutCutGrassEdits(CampaignState state)=>state.MapEdits is not {Count:>0} edits?state:
        state with{MapEdits=edits.RemoveRange(edits.Where(p=>p.Key==p.Value.X+":"+p.Value.Y&&IsCutGrassEdit(state,p.Value)).Select(p=>p.Key))};
    public CampaignState PrepareFieldSave()
    {
        var state=ReconcileFieldTransitionObservation(State);var player=state.World.Player;
        if(state.MapEdits is not {Count:>0} edits)return state;
        var outside=edits.Where(p=>IsCutGrassEdit(state,p.Value)&&!InSavedGrassView(p.Value,player)).Select(p=>p.Key).ToArray();
        return outside.Length==0?state:state with{MapEdits=edits.RemoveRange(outside)};
    }
    public int? FieldEncounterTypeAt(int x,int y)=>FieldEncounterTypeAt(State,x,y);
    private int? FieldEncounterTypeAt(CampaignState state,int x,int y)=>state.MapEdits?.GetValueOrDefault(x+":"+y) is {} edit
        ?_content.MetatileDefinition(state.World.Player.MapId,edit.SourceMetatileId)?.EncounterType:_content.Maps[state.World.Player.MapId].SourceEncounterTypeAt(x,y);
}
