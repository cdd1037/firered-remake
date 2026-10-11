using System.Collections.ObjectModel;
using System.Text.Json;
using System.Security.Cryptography;
namespace Game.Presentation.BattleAnimations;
public readonly record struct AnimationSlot
{
    public int Value { get; }
    public AnimationSlot(int value) => Value = value is >= 0 and < 4 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    public bool IsPlayer => (Value & 1) == 0;
    public AnimationSlot Partner => new(Value ^ 2);
}
public readonly record struct PixelPoint(int X, int Y);
public sealed record AnimationBattler(AnimationSlot Slot, string Asset, int YOffset, int Elevation, int SourceWidth, int SourceHeight)
{
    public PixelPoint? DisplayCenter {get;init;}
    public PixelPoint Center(bool doubles, bool animationTarget = false)
    {
        PixelPoint[] singles = [new(72,80),new(176,40),new(48,40),new(112,80)];
        PixelPoint[] pairs = [new(32,80),new(200,40),new(90,88),new(152,32)];
        var p = (doubles ? pairs : singles)[Slot.Value];
        int y = (p.Y + YOffset - (Slot.IsPlayer ? 0 : Elevation)) & 255;
        if (animationTarget) y = Math.Min(104, y + (Slot.IsPlayer ? 8 : 0));
        return new(p.X,y);
    }
}
public sealed record CommittedMoveAnimation(string BattleId, long Sequence, int MoveId, AnimationSlot Actor, AnimationSlot Target);
public sealed record SourceAnimationCommand(string Op, string[] Args, int Line);
public sealed record SourceAnimationScript(int MoveId, string Label, SourceAnimationCommand[] Commands);
public sealed record SourceAnimationSound(string Id, int Pan, int Frame);
public sealed record SourceAnimationDiagnostic(int MoveId, string Script, int SourceLine, string Operation, string Reason);
public sealed class SourceAnimationException(SourceAnimationDiagnostic diagnostic) : Exception(JsonSerializer.Serialize(diagnostic))
{
    public SourceAnimationDiagnostic Diagnostic { get; } = diagnostic;
}
public interface ISourceAnimationAudio
{
    void Play(SourceAnimationSound cue);
    bool IsPlaying { get; }
    void StopAnimationEffects();
}
public sealed record HitSplatFrame(PixelPoint Center, int Scale, string Asset = "impact");
public sealed record SourceAnimationFrame(int Frame, bool Active, PixelPoint[] Offsets, int[] BackgroundSlots,
    int AlphaA, int AlphaB, HitSplatFrame[] HitSplats, string[] Trace);

/// <summary>Translated bounded original scripts/callbacks. No gameplay state or RNG.</summary>
public sealed class SourceBattleAnimation
{
    public const double SourceFrameSeconds=280896d/16777216;
    public static readonly ReadOnlyCollection<string> SupportedCommands=Array.AsReadOnly(new[]{"loadspritegfx","monbg","setalpha","playsewithpan","createsprite","createvisualtask","delay","waitforvisualfinish","clearmonbg","blendoff","end"});
    private readonly SourceAnimationScript _script;
    private readonly CommittedMoveAnimation _cue;
    private readonly Dictionary<int,AnimationBattler> _battlers;
    private readonly ISourceAnimationAudio _audio;
    private readonly List<VisualSprite> _sprites=[];
    private readonly List<ShakeTask> _tasks=[];
    private readonly HashSet<int> _backgrounds=[];
    private readonly PixelPoint[] _offsets=new PixelPoint[4];
    private readonly List<string> _trace=[];
    private int _pc,_frame=-1,_wait,_alphaA,_alphaB,_clearTicks=-1,_soundWait;
    private bool _waitCallback,_cancelled,_loaded;
    public bool Active{get;private set;}=true;
    public CommittedMoveAnimation Cue=>_cue;
    public SourceBattleAnimation(SourceAnimationScript script,CommittedMoveAnimation cue,IEnumerable<AnimationBattler> battlers,bool doubles,ISourceAnimationAudio audio)
    {
        _script=script;_cue=cue;_audio=audio;_battlers=battlers.ToDictionary(x=>x.Slot.Value);
        if(doubles||cue.Actor.Value>1||cue.Target.Value>1)throw new NotSupportedException("This rendered slice admits source singles only.");
        if(script.MoveId!=cue.MoveId||!_battlers.ContainsKey(cue.Actor.Value)||!_battlers.ContainsKey(cue.Target.Value))throw new ArgumentException("Animation participants/script differ.");
        if(script.Commands.Length is <1 or >32||script.Commands[^1].Op!="end")throw new InvalidDataException("Unbounded or unterminated script.");
        foreach(var command in script.Commands)Validate(command);
    }
    private void Fail(SourceAnimationCommand c,string reason)=>throw new SourceAnimationException(new(_script.MoveId,_script.Label,c.Line,c.Op,reason));
    private void Validate(SourceAnimationCommand c)
    {
        if(!SupportedCommands.Contains(c.Op))Fail(c,"No accepted source adapter.");
        int count=c.Op switch{"loadspritegfx" or "monbg" or "clearmonbg" or "delay"=>1,"setalpha" or "playsewithpan"=>2,"createsprite"=>c.Args.FirstOrDefault()=="gBasicHitSplatSpriteTemplate"?7:5,"createvisualtask"=>7,_=>0};
        if(c.Args.Length!=count)Fail(c,"Command argument shape differs.");
        if(c.Op=="loadspritegfx"&&c.Args[0]!="ANIM_TAG_IMPACT")Fail(c,"Only source impact graphics are admitted.");
        if(c.Op is "monbg" or "clearmonbg"&&c.Args[0]!="ANIM_TARGET")Fail(c,"Only target monbg is admitted.");
        if(c.Op=="createsprite"&&c.Args[0] is not ("gBasicHitSplatSpriteTemplate" or "gHorizontalLungeSpriteTemplate"))Fail(c,"Unknown sprite callback.");
        if(c.Op=="createvisualtask"&&c.Args[0]!="AnimTask_ShakeMon")Fail(c,"Unknown task callback.");
        if(c.Op=="playsewithpan"&&(c.Args[0] is not ("SE_M_DOUBLE_SLAP" or "SE_M_COMET_PUNCH")||c.Args[1]!="SOUND_PAN_TARGET"))Fail(c,"Unknown sound/pan command.");
    }
    private static int Number(string s)=>s switch{"ANIM_ATTACKER"=>0,"ANIM_TARGET"=>1,_=>int.Parse(s,System.Globalization.CultureInfo.InvariantCulture)};
    private AnimationSlot Resolve(string s)=>Number(s) switch{0=>_cue.Actor,1=>_cue.Target,_=>throw new InvalidDataException("Unknown animation participant.")};
    public SourceAnimationFrame Snapshot()=>new(_frame,Active,(PixelPoint[])_offsets.Clone(),[.._backgrounds.Order()],_alphaA,_alphaB,[.._sprites.OfType<HitSplat>().Select(x=>new HitSplatFrame(x.Center,x.Scale))],[.._trace]);
    public SourceAnimationFrame Tick()
    {
        if(!Active)throw new InvalidOperationException("Animation already ended.");
        _frame++;_trace.Clear();
        // callback1 then BattleMainCB2: AnimateSprites, BuildOamBuffer, RunTasks.
        if(_waitCallback){if(_wait<=0){_waitCallback=false;_wait=0;}else _wait--;}
        else RunScript();
        foreach(var sprite in _sprites.ToArray()){sprite.Step(this);if(sprite.Finished)_sprites.Remove(sprite);}
        foreach(var task in _tasks.ToArray()){task.Step(this);if(task.Finished)_tasks.Remove(task);}
        if(_clearTicks>=0&&++_clearTicks==2){_clearTicks=-1;_backgrounds.Clear();_trace.Add("monbg-cleared");}
        return Snapshot();
    }
    private void Wait(int frames){_wait=frames==0?-1:frames;_waitCallback=true;}
    private void RunScript()
    {
        int budget=64;
        while(Active&&budget-->0)
        {
            if(_pc>=_script.Commands.Length)throw new InvalidDataException("Script fell through without end.");
            var c=_script.Commands[_pc];var a=c.Args;
            switch(c.Op)
            {
                case "loadspritegfx":_loaded=true;_pc++;Wait(1);return;
                case "monbg":_backgrounds.Add(_cue.Target.Value);break;
                case "setalpha":_alphaA=Math.Clamp(Number(a[0]),0,16);_alphaB=Math.Clamp(Number(a[1]),0,16);break;
                case "createsprite":
                    if(!_loaded)Fail(c,"Impact graphics have not been loaded.");
                    if(a[0]=="gBasicHitSplatSpriteTemplate")
                    {
                        var target=Resolve(a[5]);var center=_battlers[target.Value].Center(false,true);
                        int x=Number(a[3]);if(!_cue.Actor.IsPlayer)x=-x;
                        _sprites.Add(new HitSplat(new(center.X+x,center.Y+Number(a[4])),Number(a[6])));
                    }
                    else _sprites.Add(new Lunge(_cue.Actor,Number(a[3]),Number(a[4])));
                    break;
                case "createvisualtask":
                    var shake=new ShakeTask(Resolve(a[2]),Number(a[3]),Number(a[4]),Number(a[5]),Number(a[6]));
                    _offsets[shake.Slot.Value]=new(shake.X,shake.Y);shake.Step(this);_tasks.Add(shake);break;
                case "playsewithpan":_audio.Play(new(a[0],_cue.Target.IsPlayer?(_cue.Actor.IsPlayer?-64:-63):63,_frame));break;
                case "delay":_pc++;Wait(Number(a[0]));return;
                case "waitforvisualfinish":if(_sprites.Count!=0||_tasks.Count!=0)return;break;
                case "clearmonbg":_clearTicks=0;break;
                case "blendoff":_alphaA=_alphaB=0;break;
                case "end":
                    if(_sprites.Count!=0||_tasks.Count!=0||_backgrounds.Count!=0){_soundWait=0;return;}
                    if(_audio.IsPlaying&&++_soundWait<=90)return;
                    if(_audio.IsPlaying)_audio.StopAnimationEffects();
                    _soundWait=0;Active=false;_loaded=false;return;
                default:Fail(c,"Unimplemented source operation.");break;
            }
            _trace.Add($"{c.Line}:{c.Op}");_pc++;
        }
        if(budget<=0)throw new InvalidDataException("Source script exceeded per-frame instruction bound.");
    }
    public void Cancel()
    {
        if(_cancelled)return;_cancelled=true;Active=false;
        _audio.StopAnimationEffects();_sprites.Clear();_tasks.Clear();_backgrounds.Clear();Array.Clear(_offsets);_alphaA=_alphaB=0;_clearTicks=-1;
    }
    private abstract class VisualSprite{public bool Finished{get;protected set;}public abstract void Step(SourceBattleAnimation owner);}
    private sealed class HitSplat:VisualSprite
    {
        public PixelPoint Center{get;}public int Scale{get;}
        private int _state,_delay;
        public HitSplat(PixelPoint center,int affine)
        {
            Center=center;Scale=affine switch{0=>256,1=>216,2=>176,3=>128,_=>throw new InvalidDataException("Unknown source affine animation.")};
            // CreateSpriteAndAnimate already executes callback and BeginAffineAnim once.
            if(affine==0){_state=1;_delay=7;}
        }
        public override void Step(SourceBattleAnimation owner)
        {
            // Sprite callback precedes each affine step. End -> swap callback -> destroy
            // happen on separate source frames, not when the hold counter reaches zero.
            switch(_state){case 0:_state=1;_delay=7;break;case 1:if(_delay>0)_delay--;else _state=2;break;case 2:_state=3;break;case 3:Finished=true;break;}
        }
    }
    private sealed class Lunge(AnimationSlot slot,int duration,int delta):VisualSprite
    {
        private readonly int _duration=duration;
        private int _remaining=duration,_delta=slot.IsPlayer?delta:-delta,_state;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state is 0 or 2)
            {
                if(_remaining>0){_remaining--;var p=owner._offsets[slot.Value];owner._offsets[slot.Value]=new(p.X+_delta,p.Y);}
                else _state++;
            }
            else if(_state==1){_remaining=_duration;_delta=-_delta;_state=2;}
            else Finished=true;
        }
    }
    private sealed class ShakeTask(AnimationSlot slot,int x,int y,int count,int delay)
    {
        public AnimationSlot Slot=>slot;public int X=>x;public int Y=>y;public bool Finished{get;private set;}
        private readonly int _delay=delay;
        private int _count=count,_timer=delay;
        public void Step(SourceBattleAnimation owner)
        {
            if(Finished)return;
            if(_timer==0)
            {
                var p=owner._offsets[slot.Value];owner._offsets[slot.Value]=new(p.X==0?x:0,p.Y==0?y:0);_timer=_delay;
                if(--_count==0){owner._offsets[slot.Value]=default;Finished=true;}
            }
            else _timer--;
        }
    }
}

public sealed class SourceBattleAnimationGate
{
    private readonly HashSet<(string,long)> _started=[];
    public SourceBattleAnimation? Current{get;private set;}
    public bool IsBlocking=>Current?.Active==true;
    public bool TryStart(CommittedMoveAnimation cue,Func<SourceBattleAnimation> create)
    {
        if(IsBlocking||_started.Contains((cue.BattleId,cue.Sequence)))return false;
        var candidate=create();_started.Add((cue.BattleId,cue.Sequence));Current=candidate;return true;
    }
    public void Reset(){Current?.Cancel();Current=null;_started.Clear();}
}

public static class SourceHitAnimationCatalog
{
    public const string ManifestSha256="b3eaf979ce7f1bd512beaea3daa26308df66da0f66fcf9a8a7e1c2791d43c99c";
    public static byte[] ReadBounded(string path,int maximum,int? exact=null)
    {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        long length=file.Length;
        if(length<0||length>maximum||exact is {} n&&length!=n)throw new InvalidDataException("Source asset length differs: "+path);
        var bytes=new byte[(int)length];file.ReadExactly(bytes);
        if(file.ReadByte()!=-1)throw new InvalidDataException("Source asset changed while reading.");return bytes;
    }
    public static JsonDocument Read(string folder)
    {
        var bytes=ReadBounded(Path.Combine(folder,"manifest.json"),65536,3960);
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=ManifestSha256)throw new InvalidDataException("Source animation manifest differs.");
        return JsonDocument.Parse(bytes);
    }
    public static SourceAnimationScript Script(JsonDocument manifest,int move)
    {
        var row=manifest.RootElement.GetProperty("scripts").EnumerateArray().Single(x=>x.GetProperty("moveId").GetInt32()==move);
        return new(move,row.GetProperty("label").GetString()!,row.GetProperty("commands").EnumerateArray().Select(x=>new SourceAnimationCommand(x.GetProperty("op").GetString()!,x.GetProperty("args").EnumerateArray().Select(a=>a.GetString()!).ToArray(),x.GetProperty("line").GetInt32())).ToArray());
    }
    public static SourcePixelAsset Asset(JsonDocument manifest,string folder,string id)
    {
        var row=manifest.RootElement.GetProperty("assets").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==id);
        string path=row.GetProperty("path").GetString()!;
        if(Path.GetFileName(path)!=path)throw new InvalidDataException("Asset path differs.");
        int size=row.GetProperty("bytes").GetInt32();
        var bytes=ReadBounded(Path.Combine(folder,path),8196,size);
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=row.GetProperty("sha256").GetString())throw new InvalidDataException("Source animation asset differs.");
        int width=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes),height=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2));
        if(width!=32||height!=32||size!=4+width*height*2)throw new InvalidDataException("Impact shape differs.");
        var pixels=new ushort[width*height];for(int i=0;i<pixels.Length;i++)pixels[i]=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4+i*2));
        return new(width,height,pixels);
    }
}
