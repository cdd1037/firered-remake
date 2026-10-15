using System.Text.Json;
namespace Game.Presentation.BattleAnimations;

public static class SourceTraceAnimationCatalog
{

    public static bool Supports(int move)=>move is 97 or 98 or 332;
    public static IReadOnlyDictionary<int,SourceAnimationScript> Read(string folder,JsonDocument hitManifest)
    {
        var bytes=System.IO.File.ReadAllBytes(Path.Combine(folder,"manifest.json"));
        using var doc=JsonDocument.Parse(bytes);
        var shared=SourceHitAnimationCatalog.Script(hitManifest,1);
        return doc.RootElement.GetProperty("scripts").EnumerateArray().Select(r=>new SourceAnimationScript(r.GetProperty("moveId").GetInt32(),r.GetProperty("label").GetString()!,r.GetProperty("commands").EnumerateArray().Select(c=>new SourceAnimationCommand(c.GetProperty("op").GetString()!,c.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),c.GetProperty("line").GetInt32())).ToArray()){SineTable=shared.SineTable,SpriteRecipes=shared.SpriteRecipes}).ToDictionary(s=>s.MoveId);
    }
}
public sealed partial class SourceBattleAnimation
{
    private bool ValidateTraceOperand(SourceAnimationCommand c)
    {
        if(!SourceTraceAnimationCatalog.Supports(_script.MoveId))return false;
        string[]? expected=c.Op switch
        {
            "monbg" or "clearmonbg" when _script.MoveId is 97 or 98=>["ANIM_ATK_PARTNER"],
            "createvisualtask" when c.Args.FirstOrDefault()=="AnimTask_TraceMonBlended"=>["AnimTask_TraceMonBlended","2","0","4","7",_script.MoveId==97?"10":"3"],
            "createsprite" when c.Args.FirstOrDefault()=="gComplexPaletteBlendSpriteTemplate"&&_script.MoveId==332=>["gComplexPaletteBlendSpriteTemplate","ANIM_ATTACKER","2","F_PAL_BG | F_PAL_BATTLERS","3","1","RGB_BLACK","10","RGB_BLACK","0"],
            "playsewithpan" when c.Args.FirstOrDefault() is "SE_M_JUMP_KICK" or "SE_M_RAZOR_WIND2"=>[_script.MoveId==98?"SE_M_JUMP_KICK":"SE_M_RAZOR_WIND2","SOUND_PAN_ATTACKER"],
            _=>null
        };
        if(expected is null)return false;
        if(!c.Args.SequenceEqual(expected))Fail(c,"Unadmitted trace-family source operands.");
        return true;
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
                int slot=Enumerable.Range(0,64).FirstOrDefault(i=>(!owner._battlers.TryGetValue(i,out var b)||!b.Visible)&&owner._sprites.All(s=>s.Slot!=i),-1);
                if(battler.Visible&&slot>=0){owner._sprites.Add(new TraceMonClone(slot,battler,owner._doubles,owner._offsets[battler.Slot.Value],lifetime,this));LiveCopies++;}
                _remaining--;_wait=spacing;
            }
            else if(LiveCopies==0)Finished=true;
        }
    }
    private sealed class TraceMonClone(int slot,AnimationBattler battler,bool doubles,PixelPoint offset,int lifetime,TraceMonTask task):VisualSprite(slot,battler.Subpriority)
    {
        private int _life=lifetime;private bool _visible;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"AnimMonTrace",battler.DisplayCenter??battler.Center(doubles),offset,0,_visible,false,true,256,battler.Asset,Subpriority){BattlerCopySlot=battler.Slot.Value};
        public override void Step(SourceBattleAnimation owner){_visible=true;if(_life!=0)_life--;else{task.LiveCopies--;Finished=true;}}
    }
}
