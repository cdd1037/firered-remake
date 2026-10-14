using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Application;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Game.Presentation.BattleAnimations;
using Godot;

public partial class CampaignView
{
    private readonly SourceBattleAnimationGate _sourceMoveAnimationGate=new();
    private readonly Dictionary<(int,string),AnimationBattler> _sourceBattlerGeometry=[];
    private readonly Dictionary<string,SourcePixelAsset> _sourceMoveAssets=[];
    private readonly Dictionary<WildBattleSide,int> _sourceSubstituteHp=[];
    private readonly Dictionary<string,ImageTexture> _sourceSubstituteTextures=[];
    private JsonDocument? _sourceMoveManifest;
    private IReadOnlyDictionary<int,SourceAnimationScript>? _sourceStatMoveScripts,_sourceExplosionScripts,_sourceTraceScripts;
    private SourceMetallicMaterial? _sourceMetallicMaterial;
    private SourceBattleFrameComposer? _sourceMoveComposer;
    private ImageTexture? _sourceMoveField;
    private SourceBattleAnimationAudioAdapter? _sourceMoveSound;
    private CampaignMoveAnimationDispatch? _sourceMoveContinuation;
    private CampaignSession? _sourceMoveSession;
    private long _sourceMoveRevision;
    private double _sourceMoveRemainder;
    private bool _sourceMoveDoubles;
    private string? _sourceVisualBattle;
    private long _sourceVisualSequence;
    private sealed class SilentMoveAudio:ISourceAnimationAudio
    { public bool IsPlaying=>false; public void Play(SourceAnimationSound cue){} public void StopAnimationEffects(){} }

    private void EnsureSourceMoveAssets()
    {
        if(_sourceMoveManifest is not null)return;
        string folder=System.IO.Path.Combine(_root,"content/generated/source-hit-animations");
        _sourceMoveManifest=SourceHitAnimationCatalog.Read(folder);
        foreach(var row in _sourceMoveManifest.RootElement.GetProperty("assets").EnumerateArray())
        {string id=row.GetProperty("id").GetString()!;_sourceMoveAssets.Add(id,SourceHitAnimationCatalog.Asset(_sourceMoveManifest,folder,id));}
        folder=System.IO.Path.Combine(_root,"content/generated/campaign-hit-art");
        var bytes=SourceHitAnimationCatalog.ReadBounded(System.IO.Path.Combine(folder,"manifest.json"),132335,132335);
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!="f65895babddf0327ad6884ba4d86d2c582e780c17bf91064599a657361acc94c")throw new InvalidDataException("Campaign hit art manifest differs.");
        using var doc=JsonDocument.Parse(bytes);
        foreach(var row in doc.RootElement.GetProperty("geometry").EnumerateArray())
        {
            string side=row.GetProperty("side").GetString()!;
            _sourceBattlerGeometry.Add((row.GetProperty("species").GetInt32(),side),new(new(side=="back"?0:1),"",row.GetProperty("yOffset").GetInt32(),row.GetProperty("elevation").GetInt32(),row.GetProperty("sourceWidth").GetInt32(),row.GetProperty("sourceHeight").GetInt32()));
        }
        VerifyCelioMoveGeometry();
        VerifyMewtwoMoveGeometry();
        VerifyLeagueRematchMoveGeometry();
        VerifySevenIslandMoveGeometry();VerifyEventIslandMoveGeometry();VerifyPowerPlantMoveGeometry();AddTrainerTowerMoveGeometry();VerifyFossilLabMoveGeometry();VerifySeafoamMoveGeometry();VerifyQwilfishMoveGeometry();VerifyStoneEvolutionMoveGeometry();VerifyGameCornerMoveGeometry();VerifyBreedingMoveGeometry();VerifyOrdinaryEvolutionMoveGeometry();
        foreach(var row in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            string path=row.GetProperty("path").GetString()!,id=row.GetProperty("id").GetString()!;
            if(System.IO.Path.GetFileName(path)!=path)throw new InvalidDataException("Campaign hit art path differs.");
            var data=SourceHitAnimationCatalog.ReadBounded(System.IO.Path.Combine(folder,path),8196,8196);
            if(Convert.ToHexStringLower(SHA256.HashData(data))!=row.GetProperty("sha256").GetString()||BinaryPrimitives.ReadUInt16LittleEndian(data)!=64||BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2))!=64)throw new InvalidDataException("Substitute image differs.");
            ushort[] pixels=new ushort[4096];for(int i=0;i<pixels.Length;i++)pixels[i]=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4+i*2));
            var asset=new SourcePixelAsset(64,64,pixels);_sourceMoveAssets.Add(id,asset);
            using var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,SourcePixelRgba(asset));
            _sourceSubstituteTextures.Add(id,ImageTexture.CreateFromImage(image));
        }
    }
    private static SourcePixelAsset SourcePixels(Texture2D texture,int expectedWidth,int expectedHeight)
    {
        using var image=texture.GetImage();
        if(image.GetWidth()!=expectedWidth||image.GetHeight()!=expectedHeight)throw new InvalidDataException("Displayed campaign image dimensions differ.");
        image.Convert(Image.Format.Rgba8);var rgba=image.GetData();ushort[] pixels=new ushort[expectedWidth*expectedHeight];
        for(int i=0;i<pixels.Length;i++)pixels[i]=(ushort)((rgba[i*4]>>3)|((rgba[i*4+1]>>3)<<5)|((rgba[i*4+2]>>3)<<10)|(rgba[i*4+3]!=0?0x8000:0));
        return new(expectedWidth,expectedHeight,pixels);
    }
    private static byte[] SourcePixelRgba(SourcePixelAsset asset)
    {
        var rgba=new byte[asset.Pixels.Length*4];
        for(int i=0;i<asset.Pixels.Length;i++){int p=asset.Pixels[i];rgba[i*4]=(byte)((p&31)*255/31);rgba[i*4+1]=(byte)(((p>>5)&31)*255/31);rgba[i*4+2]=(byte)(((p>>10)&31)*255/31);rgba[i*4+3]=(byte)((p&0x8000)!=0?255:0);}
        return rgba;
    }
    private void SynchronizeSourceMovePresentation()
    {
        if(DoubleBattleVisible)
        {
            var current=_session.State.DoubleBattle!;
            if(_sourceVisualBattle!=current.Id){ResetSourceMoveAnimation("battle-replaced");_sourceVisualBattle=current.Id;}
            if(_sourceMoveContinuation is not null&&!SourceMoveContextMatches())CancelSourceMoveAnimation("context-changed");
            SynchronizeSourceStatPresentation();return;
        }
        var b=PresentedWildBattle;
        if(b is null||!SourceTrainerPresentationActive(b))
        { if(_sourceVisualBattle is not null)ResetSourceMoveAnimation("battle-exit");return; }
        if(_sourceVisualBattle!=b.Id)
        {
            ResetSourceMoveAnimation("battle-replaced");_sourceVisualBattle=b.Id;
            _sourceSubstituteHp[WildBattleSide.Player]=b.State.Player.CalledVolatile?.SubstituteHp??0;
            _sourceSubstituteHp[WildBattleSide.Opponent]=b.State.Opponent.CalledVolatile?.SubstituteHp??0;
            // Normally battle entry has no substitute. This also makes a dedicated live-view host
            // at a committed queue boundary reconstruct the earlier display, without changing rules.
            foreach(var e in b.Events.Reverse())
                if(e is WildBattleSubstituteDamagedEvent hit)_sourceSubstituteHp[hit.Target]=hit.HpBefore;
                else if(e is WildBattleMtMoonVolatileEvent {Volatile:"substitute"} created)_sourceSubstituteHp[created.Side]=created.Before;
        }
        if(_sourceMoveContinuation is {} pending && (_sourceMoveSession!=_session || _sourceMoveRevision!=_session.Revision || WildCurrentEvent(b)?.Sequence!=pending.Sequence))
            CancelSourceMoveAnimation("context-changed");
        int count=b.Phase==CampaignBattlePhase.Events?Math.Min(b.Events.Length,b.EventIndex+1):b.Events.Length;
        foreach(var e in b.Events.Take(count).Where(e=>e.Sequence>_sourceVisualSequence))
        {
            if(e is WildBattleMtMoonVolatileEvent {Volatile:"substitute"} created)_sourceSubstituteHp[created.Side]=created.After;
            else if(e is WildBattleSubstituteDamagedEvent hit)_sourceSubstituteHp[hit.Target]=hit.HpAfter;
            else if(e is WildBattlePlayerSwitchedEvent switched)_sourceSubstituteHp[WildBattleSide.Player]=switched.Replacement.CalledVolatile?.SubstituteHp??0;
            else if(e is WildBattleCalledPartySwitchedEvent {Kind:not WildBattleCalledSwitchKind.BatonPass} swapped)_sourceSubstituteHp[swapped.Side]=0;
            else if(e is WildBattleFaintedEvent faint)_sourceSubstituteHp[faint.Side]=0;
            _sourceVisualSequence=e.Sequence;
        }
        SynchronizeSourceStatPresentation();
    }
    private bool DrawSourceSubstitute(WildBattleSide side)
    {
        if(_sourceSubstituteHp.GetValueOrDefault(side)<=0)return false;
        EnsureSourceMoveAssets();DrawTexture(_sourceSubstituteTextures[side==WildBattleSide.Player?"substitute-back":"substitute-front"],side==WildBattleSide.Player?new(40,65):new(144,24));return true;
    }
    private void AcknowledgeWildAttackText()
    {
        var b=PresentedWildBattle!;
        var decision=CampaignMoveAnimationPresentation.AtAttackText(b,SourceTrainerPresentationActive(b),_session.EffectiveOptions.BattleScene);
        if(decision.Dispatch is not {} dispatch){Apply(new("ack"));return;}
        if(SourceAnimationBlocking)return;
        EnsureSourceMoveAssets();var (assets,battlers)=CreateSourceAnimationScene(false);
        StartSourceMoveAnimation(dispatch,assets,battlers,false);
    }
    private void AcknowledgeDoubleAttackText()
    {
        var b=_session.State.DoubleBattle!;
        var decision=CampaignMoveAnimationPresentation.AtAttackText(b,_session.EffectiveOptions.BattleScene);
        if(decision.Dispatch is not {} dispatch){DoubleSubmit(new("ack"));return;}
        if(SourceAnimationBlocking)return;
        EnsureSourceMoveAssets();var (assets,battlers)=CreateSourceAnimationScene(true);
        StartSourceMoveAnimation(dispatch,assets,battlers,true);
    }
    private (Dictionary<string,SourcePixelAsset> Assets,List<AnimationBattler> Battlers) CreateSourceAnimationScene(bool doubles)
    {
        if(!doubles)
        {
            var b=PresentedWildBattle!;
        var assets=new Dictionary<string,SourcePixelAsset>(_sourceMoveAssets){{"background",SourcePixels(CurrentWildBattleTerrain,240,112)}};
        var battlers=new List<AnimationBattler>();
        foreach(var (side,mon) in new[]{(WildBattleSide.Player,WildDisplayPlayer(b)),(WildBattleSide.Opponent,b.DisplayOpponent)})
        {
            var appearance=FuchsiaBattleAppearance(b,side,mon);string face=side==WildBattleSide.Player?"back":"front";
            var displayed=_battleArt[(appearance.Species,face,appearance.Shiny)];
            var geometry=_sourceBattlerGeometry[(appearance.Species,face)] with{Asset="battler-"+(int)side};
            var center=geometry.Center(false);
            if(displayed.Position!=new Vector2(center.X-32,center.Y-32))throw new InvalidDataException("Authenticated campaign/source battler geometry differs.");
            bool substitute=_sourceSubstituteHp.GetValueOrDefault(side)>0;
            assets.Add(geometry.Asset,substitute?_sourceMoveAssets["substitute-"+face]:SourcePixels(displayed.Texture,64,64));
            if(substitute)geometry=geometry with{DisplayCenter=new(side==WildBattleSide.Player?72:176,side==WildBattleSide.Player?97:56)};
            battlers.Add(geometry);
        }
            return (assets,battlers);
        }
        else
        {
        var assets=new Dictionary<string,SourcePixelAsset>(_sourceMoveAssets){{"background",SourcePixels(CurrentWildBattleTerrain,240,112)}};
        var battlers=new List<AnimationBattler>();
        for(int slot=0;slot<4;slot++)
        {
            // DisplayedSlots is the event-time snapshot, before this hit/KO is acknowledged.
            // The committed frame may already contain damage and later action results.
            var body=DoubleDisplayedBody(slot);var mon=body.Pokemon;var appearance=body.Transform?.CopiedPokemon??mon;string face=(slot&1)==0?"back":"front";
            var displayed=_battleArt[(PresentedRenderSpecies(appearance),face,appearance.IsShiny)];
            var geometry=_sourceBattlerGeometry[(PresentedRenderSpecies(appearance),face)] with{Slot=new(slot),Asset="battler-"+slot,Visible=mon.CurrentHp>0};
            var center=geometry.Center(true);
            if(displayed.Position+DoubleSpriteOffsets[slot]!=new Vector2(center.X-32,center.Y-32))throw new InvalidDataException("Authenticated doubles/source battler geometry differs.");
            bool substitute=body.CalledVolatile?.SubstituteHp>0;
            assets.Add(geometry.Asset,substitute?_sourceMoveAssets["substitute-"+face]:SourcePixels(displayed.Texture,64,64));
            if(substitute)
            {
                var offset=DoubleSpriteOffsets[slot];
                geometry=geometry with{DisplayCenter=new((slot&1)==0?72+(int)offset.X:176+(int)offset.X,(slot&1)==0?97+(int)offset.Y:56+(int)offset.Y)};
            }
            battlers.Add(geometry);
        }
            return (assets,battlers);
        }
    }
    private void StartSourceMoveAnimation(CampaignMoveAnimationDispatch dispatch,Dictionary<string,SourcePixelAsset> assets,List<AnimationBattler> battlers,bool doubles)
    {
        if(SourceExplosionAnimationCatalog.Supports(dispatch.SourceMoveId)&&_sourceExplosionScripts is null)
        {
            var imported=SourceExplosionAnimationCatalog.Read(System.IO.Path.Combine(_root,"content/generated/source-explosion-animations"));
            _sourceExplosionScripts=imported.Scripts;_sourceMoveAssets["explosion"]=imported.Explosion;
        }
        if(SourceExplosionAnimationCatalog.Supports(dispatch.SourceMoveId))assets["explosion"]=_sourceMoveAssets["explosion"];
        if(SourceStatMoveCatalog.Supports(dispatch.SourceMoveId)&&_sourceStatMoveScripts is null)(_sourceStatMoveScripts,_sourceMetallicMaterial)=SourceStatMoveCatalog.Read(System.IO.Path.Combine(_root,"content/generated/source-stat-moves"));
        if(SourceTraceAnimationCatalog.Supports(dispatch.SourceMoveId)&&_sourceTraceScripts is null)_sourceTraceScripts=SourceTraceAnimationCatalog.Read(System.IO.Path.Combine(_root,"content/generated/source-trace-animations"),_sourceMoveManifest!);
        var script=SourceTraceAnimationCatalog.Supports(dispatch.SourceMoveId)?_sourceTraceScripts![dispatch.SourceMoveId]:SourceExplosionAnimationCatalog.Supports(dispatch.SourceMoveId)?_sourceExplosionScripts![dispatch.SourceMoveId]:SourceStatMoveCatalog.Supports(dispatch.SourceMoveId)?_sourceStatMoveScripts![dispatch.SourceMoveId]:SourceHitAnimationCatalog.Script(_sourceMoveManifest!,dispatch.SourceMoveId);
        var cue=new CommittedMoveAnimation(dispatch.BattleId,dispatch.Sequence,dispatch.SourceMoveId,new(dispatch.Actor.Value),new(dispatch.Target.Value));
        _sourceMoveSound=_sourceAudio is SynthSourceAudioPlayer synth&&!_audioFaulted?new(synth,c=>Write("sourceMoveAnimationSound",new{dispatch.BattleId,dispatch.Sequence,c.Id,c.Frame,c.Pan,panningImplemented=_sourceMoveSound?.PanningImplemented==true})):null;
        if(!_sourceMoveAnimationGate.TryStart(cue,()=>new(script,cue,battlers,doubles,(ISourceAnimationAudio?)_sourceMoveSound??new SilentMoveAudio()))){Apply(new("ack"));return;}
        _sourceMoveComposer=new(assets,battlers.ToArray(),metallicMaterial:_sourceMetallicMaterial);_sourceMoveContinuation=dispatch;_sourceMoveSession=_session;_sourceMoveRevision=_session.Revision;_sourceMoveRemainder=0;_sourceMoveDoubles=doubles;
        PresentSourceMoveFrame(_sourceMoveAnimationGate.Current!.Tick());
        Write("sourceMoveAnimationStarted",new{dispatch,doubles,revision=_sourceMoveRevision,actors=battlers.Select(x=>new{slot=x.Slot.Value,x.Asset,x.YOffset,x.Elevation,x.SourceWidth,x.SourceHeight,x.DisplayCenter,x.Visible}),sourceAudioAvailable=_sourceMoveSound is not null,sourcePanApplied=_sourceMoveSound?.PanningImplemented==true});QueueRedraw();
    }
    private bool SourceMoveContextMatches()
    {
        if(_sourceMoveContinuation is not {} pending||_sourceMoveSession!=_session||_sourceMoveRevision!=_session.Revision)return false;
        return _sourceMoveDoubles
            ?_session.State.DoubleBattle is {Stage:CampaignDoubleBattleStage.Combat} d&&d.Id==pending.BattleId&&d.CurrentEvent?.Sequence==pending.Sequence
            :PresentedWildBattle is {} b&&b.Id==pending.BattleId&&WildCurrentEvent(b)?.Sequence==pending.Sequence;
    }
    private void PresentSourceMoveFrame(SourceAnimationFrame frame)
    {
        var rgba=_sourceMoveComposer!.ComposeField(frame);
        using var image=Image.CreateFromData(240,112,false,Image.Format.Rgba8,rgba);
        if(_sourceMoveField is null)_sourceMoveField=ImageTexture.CreateFromImage(image);else _sourceMoveField.Update(image);
    }
    private bool ProcessCommittedMoveAnimation(double delta)
    {
        if(ProcessCommittedStatAnimation(delta))return true;
        if(_sourceMoveContinuation is not {} pending)return false;
        if(!SourceMoveContextMatches())
        {CancelSourceMoveAnimation("context-changed");return false;}
        _sourceMoveRemainder+=delta;
        while(_sourceMoveRemainder>=SourceBattleAnimation.SourceFrameSeconds&&_sourceMoveAnimationGate.IsBlocking)
        {_sourceMoveRemainder-=SourceBattleAnimation.SourceFrameSeconds;PresentSourceMoveFrame(_sourceMoveAnimationGate.Current!.Tick());}
        if(!_sourceMoveAnimationGate.IsBlocking)
        {
            // Retire the continuation before executing the sole original ACK. Reentrant Sync/input
            // sees no pending continuation; the gameplay move transaction is never replayed.
            _sourceMoveContinuation=null;_sourceMoveSession=null;_sourceMoveComposer=null;_sourceMoveSound=null;
            Write("sourceMoveAnimationFinished",new{pending.BattleId,pending.Sequence,sourceFrame=_sourceMoveAnimationGate.Current!.Snapshot().Frame});
            Apply(new("ack"));
        }
        QueueRedraw();return true;
    }
    private void CancelSourceMoveAnimation(string reason)
    {
        CancelSourceStatAnimation(reason);
        var pending=_sourceMoveContinuation;_sourceMoveContinuation=null;_sourceMoveSession=null;
        _sourceMoveAnimationGate.Current?.Cancel();_sourceMoveSound=null;_sourceMoveComposer=null;_sourceMoveRemainder=0;
        if(pending is not null)Write("sourceMoveAnimationCancelled",new{pending.BattleId,pending.Sequence,reason});
    }
    private void ResetSourceMoveAnimation(string reason)
    {
        CancelSourceMoveAnimation(reason);_sourceMoveAnimationGate.Reset();_sourceStatAnimationGate.Reset();_sourceVisualBattle=null;_sourceVisualSequence=0;_sourceSubstituteHp.Clear();
    }
    private void DisposeSourceMoveAnimation()
    {
        ResetSourceMoveAnimation("exit-tree");_sourceMoveField?.Dispose();_sourceMoveField=null;_sourceMoveManifest?.Dispose();_sourceMoveManifest=null;
        foreach(var texture in _sourceSubstituteTextures.Values)texture.Dispose();_sourceSubstituteTextures.Clear();_sourceMoveAssets.Clear();_sourceBattlerGeometry.Clear();
    }
}
