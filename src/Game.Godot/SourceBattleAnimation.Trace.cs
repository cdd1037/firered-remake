using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public static class SourceTraceAnimationCatalog
{

    public static IReadOnlyDictionary<int,SourceAnimationScript> Read(string folder,JsonDocument hitManifest)
    {
        var bytes=System.IO.File.ReadAllBytes(folder);
        using var doc=JsonDocument.Parse(bytes);
        var shared=SourceHitAnimationCatalog.Script(hitManifest,1);
        return doc.RootElement.GetProperty("scripts").EnumerateArray().Select(r=>new SourceAnimationScript(r.GetProperty("moveId").GetInt32(),r.GetProperty("label").GetString()!,r.GetProperty("commands").EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray()){SineTable=shared.SineTable,SpriteRecipes=shared.SpriteRecipes}).ToDictionary(s=>s.MoveId);
    }
}
public sealed partial class SourceBattleAnimation
{
    private bool ValidateTraceOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op!="createvisualtask"||a.FirstOrDefault()!="AnimTask_TraceMonBlended")return false;
        // Source task words are signed16; negative timers/copies never reach zero.
        // CloneBattlerSpriteWithBlend naturally skips an allocation when the shared
        // 64-cell registry is full, without changing its bounded attempt count.
        if(a.Length!=6||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >1||a.Skip(3).Any(v=>Number(v) is <0 or >32767))Fail(c,"Trace callback requires a participant and bounded source counters.");
        if(_validatingInvokedProgram)
        {
            if(!IsSourceBattlerPresent((Number(a[2])==0?_cue.Actor:_cue.Target).Value))Fail(c,"Trace cloning requires a present source participant.");
            ValidateTraceCloneClosure(c);
        }
        return true;
    }
    private void ValidateTraceCloneClosure(SourceAnimationCommand command)
    {
        // A raw clone copies the source battler sprite, including its affine and
        // visibility state. This adapter owns only normal translated copies. Use
        // source callback semantics, never move IDs, to keep that claim bounded.
        // Conservatively reject these combinations across all participants; no
        // overlap/presence proof is implied even when only the other mon changes.
        // Shared but uncalled helpers do not belong to the current invocation.
        var invoked=SourceAnimationProgramDependencies.RequiredSubroutines(_script.Commands,_script.Subroutines);
        var commands=_script.Commands.Concat(invoked.Values.SelectMany(x=>x));
        foreach(var c in commands)
        {
            string callback=c.Args.FirstOrDefault()??"";
            if(c.Op=="createvisualtask"&&callback is not ("AnimTask_TraceMonBlended" or "AnimTask_TranslateMonEllipticalRespectSide" or "AnimTask_TranslateMonElliptical" or "AnimTask_ShakeMon" or "AnimTask_ShakeMon2" or "AnimTask_GetReturnPowerLevel" or "SoundTask_PlaySE1WithPanning" or "SoundTask_PlaySE2WithPanning"))
                Fail(command,"Trace closure cannot copy unreviewed battler affine, visibility or form changes.");
            if(c.Op=="createsprite"&&callback is not ("gBasicHitSplatSpriteTemplate" or "gComplexPaletteBlendSpriteTemplate" or "gSimplePaletteBlendSpriteTemplate" or "gCuttingSliceSpriteTemplate" or "gHorizontalLungeSpriteTemplate" or "gVerticalDipSpriteTemplate"))
                Fail(command,"Trace closure requires reviewed non-affine battler companion callbacks.");
        }
    }
    // Original AnimTask_TraceMonBlended / _Step / AnimMonTrace. A clone snapshots
    // the current sprite coordinates and subpriority; its palette stays shared.
    private sealed class TraceMonTask(int participant,int spacing,int lifetime,int copies):VisualTask
    {
        private int _wait,_remaining=copies;
        public int LiveCopies;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_remaining!=0)
            {
                if(_wait!=0){_wait--;return;}
                var battler=owner._battlers[(participant==0?owner._cue.Actor:owner._cue.Target).Value];
                int slot=owner.FindFreeSpriteSlot();
                if(owner.IsSourceBattlerPresent(battler.Slot.Value)&&slot<SourceSpriteRegistry<VisualSprite>.Capacity){owner._sprites.Add(new TraceMonClone(slot,battler,owner._doubles,owner._offsets[battler.Slot.Value],lifetime,this,participant));LiveCopies++;}
                _remaining--;_wait=spacing;
            }
            else if(LiveCopies==0)Finished=true;
        }
    }
    private sealed class TraceMonClone(int slot,AnimationBattler battler,bool doubles,PixelPoint offset,int lifetime,TraceMonTask task,int participant):VisualSprite(slot,battler.Subpriority)
    {
        private int _life=lifetime;private bool _visible;
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"AnimMonTrace",battler.DisplayCenter??battler.Center(doubles),offset,0,_visible,false,true,256,battler.Asset,Subpriority){BattlerCopySlot=battler.Slot.Value,OamPriority=participant==0?2:1};
        public override void Step(SourceBattleAnimation owner){_visible=true;if(_life!=0)_life--;else{task.LiveCopies--;Finished=true;}}
    }
}
