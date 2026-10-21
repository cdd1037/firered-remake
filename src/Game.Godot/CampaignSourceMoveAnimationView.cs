using System.Buffers.Binary;
using System.Text.Json;
using Game.Application;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Game.Presentation.BattleAnimations;
using Godot;

public partial class CampaignView
{
    private readonly SourceBattleAnimation.SpriteScene _sourceSpriteScene=new();
    private readonly SourceCompletedBattlerState _sourceCompletedState=new();
    private readonly CampaignChargeAnimationPoseLifetime _sourceChargePoseLifetime=new();
    private readonly CampaignSubstituteAnimationLifetime _sourceSubstituteLifetime=new();
    private bool _sourceMoveRestoresSubstitute;
    private readonly Dictionary<int,PartyMemberKey> _sourceCompletedMembers=[];
    private SourceAnimationFrame? _sourceMoveInitialFrame;
    private IReadOnlyDictionary<string,SourcePixelAsset>? _sourceMoveMaterials;
    private long? _sourceCompletedMaterialSequence;
    private bool _sourceIdleSpritesPresented;
    private double _sourceIdleRemainder;
    private void RetainResidualMoveSprites()
    {
        _sourceIdleRemainder=_sourceMoveRemainder;
        _sourceIdleSpritesPresented=false;
    }
    private void ProcessResidualMoveSprites(double delta)
    {
        if(SourceAnimationBlocking){_sourceIdleSpritesPresented=false;return;}
        bool doubles=DoubleBattleVisible;
        RefreshCompletedSourceState(doubles);
        if(!_sourceSpriteScene.HasResidualSprites&&!_sourceCompletedState.HasState){_sourceIdleSpritesPresented=false;return;}
        var (assets,battlers)=CreateSourceAnimationScene(doubles);
        var composer=new SourceBattleFrameComposer(assets,battlers.ToArray(),backgroundSurface:SourceTerrainSurface(assets["background"]));
        _sourceIdleRemainder+=delta;
        var frame=_sourceSpriteScene.CaptureIdle(battlers.ToArray(),doubles,_sourceCompletedState);
        while(_sourceSpriteScene.HasResidualSprites&&_sourceIdleRemainder>=SourceBattleAnimation.SourceFrameSeconds)
        {
            _sourceIdleRemainder-=SourceBattleAnimation.SourceFrameSeconds;
            frame=_sourceSpriteScene.TickIdle(battlers.ToArray(),doubles,_sourceCompletedState);
        }
        _sourceIdleSpritesPresented=_sourceSpriteScene.HasResidualSprites||_sourceCompletedState.HasState;
        if(_sourceIdleSpritesPresented)PresentSourceMovePixels(composer.ComposeField(frame));
        QueueRedraw();
    }
    private void ClearCompletedSourceState(bool preserveChargeVisibility=false)
    {
        if(preserveChargeVisibility)
        {
            _sourceCompletedState.ClearTransient();
            foreach(int slot in _sourceCompletedMembers.Keys.Where(s=>!_sourceCompletedState.Slots.Contains(s)).ToArray())_sourceCompletedMembers.Remove(slot);
        }
        else{_sourceCompletedState.Clear();_sourceChargePoseLifetime.Clear();_sourceCompletedMembers.Clear();}
        _sourceMoveInitialFrame=null;_sourceMoveMaterials=null;_sourceCompletedMaterialSequence=null;
    }
    private PartyMemberKey SourceDisplayedMember(bool doubles,int slot)
    {
        if(doubles)return _session.State.DoubleBattle!.DisplayedMembers[slot];
        var battle=PresentedWildBattle!;
        if(battle.SwitchDisplay is {} display&&display.Slot.Value==slot)
        {
            if(WildCurrentEvent(battle) is SourceCalledPartySwitchedEvent switched)
                return new(switched.Side.Side,WildSwitchShowingOutgoing(battle,(WildBattleSide)slot)?switched.OutgoingIndex:switched.IncomingIndex);
            if(!WildWithdrawing(battle))return battle.DisplayBatch!.Display.Members[slot];
            var outgoing=battle.LastSwitch?.Outgoing??throw new InvalidDataException("Outgoing player identity is missing.");
            int index=Enumerable.Range(0,PresentedBattlePlayerParty.Length).Single(i=>PresentedBattlePlayerParty[i].InstanceId==outgoing.InstanceId);
            return new(BattleSide.Player,index);
        }
        return battle.DisplayBatch!.Display.Members[slot];
    }
    private void RefreshCompletedSourceState(bool doubles)
    {
        RefreshSubstituteController(doubles);
        if(!_sourceCompletedState.HasState)return;
        if(!doubles&&PresentedWildBattle is null){ClearCompletedSourceState();return;}
        bool changed=false;
        var chargeEvents=doubles?_session.State.DoubleBattle!.Events:PresentedWildBattle!.Events;
        long? currentChargeReceipt=doubles?_session.State.DoubleBattle!.CurrentEvent?.Sequence:WildCurrentEvent(PresentedWildBattle!)?.Sequence;
        // No source MOVEEND visibility receipt is exposed. The charge-owned hold
        // is released conservatively after the complete result batch is ACKed,
        // never at the earlier status3-clear marker or while result text is open.
        while(_sourceChargePoseLifetime.Observe(chargeEvents,currentChargeReceipt) is {} releasedSlot)
        {
            _sourceCompletedState.DropVisibility(releasedSlot);
            if(!_sourceCompletedState.Slots.Contains(releasedSlot))_sourceCompletedMembers.Remove(releasedSlot);
            changed=true;
        }
        // Singles applies the receipt before exposing it; doubles applies it on ACK.
        // Until then the terminal material belongs to the completed visual invocation.
        long? displayedSequence=doubles?_session.State.DoubleBattle!.CurrentEvent?.Sequence:WildCurrentEvent(PresentedWildBattle!)?.Sequence;
        bool materialCommitted=_sourceCompletedMaterialSequence is {} boundary&&
            (displayedSequence is null||(doubles?displayedSequence>boundary:displayedSequence>=boundary));
        foreach(int slot in _sourceCompletedState.Slots)
        {
            bool replaced=SourceDisplayedMember(doubles,slot)!=_sourceCompletedMembers[slot];
            if(doubles)replaced|=_session.State.DoubleBattle!.CurrentEvent switch
            {
                SourceCalledPartySwitchedEvent x=>x.Side.Value==slot&&DoubleSwitchSending(x),
                SourcePlayerSwitchedEvent x=>x.Slot.Value==slot&&_doublePage>0,
                SourcePlayerReplacedEvent x=>x.Slot.Value==slot,
                _=>false
            };
            else replaced|=WildCurrentEvent(PresentedWildBattle!) switch
            {
                SourceCalledPartySwitchedEvent x=>x.Side.Value==slot&&!WildSwitchShowingOutgoing(PresentedWildBattle!, (WildBattleSide)slot),
                SourcePlayerReplacedEvent=>slot==0,
                SourcePlayerSwitchedEvent=>slot==0&&!WildWithdrawing(PresentedWildBattle!),
                _=>false
            };
            if(replaced){_sourceChargePoseLifetime.DropSlot(slot);_sourceCompletedState.DropSlot(slot);_sourceCompletedMembers.Remove(slot);changed=true;}
            else if(materialCommitted&&_sourceCompletedState.HasMaterial(slot)){_sourceCompletedState.DropMaterial(slot);changed=true;}
        }
        if(materialCommitted)_sourceCompletedMaterialSequence=null;
        if(!_sourceCompletedState.HasState)_sourceIdleSpritesPresented=false;
        else if(changed&&!SourceAnimationBlocking)
        {
            var(assets,battlers)=CreateSourceAnimationScene(doubles);
            var composer=new SourceBattleFrameComposer(assets,battlers.ToArray(),backgroundSurface:SourceTerrainSurface(assets["background"]));
            PresentSourceMovePixels(composer.ComposeField(_sourceSpriteScene.CaptureIdle(battlers.ToArray(),doubles,_sourceCompletedState)));
            _sourceIdleSpritesPresented=true;
        }
    }
    private void ClearResidualMoveSprites()
    {ClearCompletedSourceState();_sourceSubstituteLifetime.Clear();_sourceSpriteScene.Cancel();_sourceIdleSpritesPresented=false;_sourceIdleRemainder=0;}
    private readonly SourceBattleAnimationGate _sourceMoveAnimationGate=new();
    private readonly Dictionary<(int,string),AnimationBattler> _sourceBattlerGeometry=[];
    private readonly Dictionary<string,SourcePixelAsset> _sourceMoveAssets=[];
    private readonly Dictionary<WildBattleSide,int> _sourceSubstituteHp=[];
    private readonly Dictionary<string,ImageTexture> _sourceSubstituteTextures=[];
    private SourceAnimationResources? _sourceAnimationResources;
    private SourceBattleFrameComposer? _sourceMoveComposer;
    private readonly Dictionary<ulong,int> _sourceTerrainIds=[];
    private SourceAnimationBackgroundSurface SourceTerrainSurface(SourcePixelAsset field)
    {
        ulong key=CurrentWildBattleTerrain.GetInstanceId();
        if(!_sourceTerrainIds.TryGetValue(key,out int id))
        {id=_sourceAnimationResources!.IdentifyTerrain(field);_sourceTerrainIds.Add(key,id);}
        return _sourceAnimationResources!.CreateBackgroundSurface(id);
    }
    private ImageTexture? _sourceMoveField;
    private SourceBattleAnimationAudioAdapter? _sourceMoveSound;
    private CampaignMoveAnimationDispatch? _sourceMoveContinuation;
    private CampaignSession? _sourceMoveSession;
    private long _sourceMoveRevision;
    private double _sourceMoveRemainder;
    private bool _sourceMoveDoubles;
    private string? _sourceVisualBattle;
    private sealed class SilentMoveAudio:ISourceAnimationAudio
    { public bool IsPlaying=>false; public void Play(SourceAnimationSound cue){} public void StopAnimationEffects(){}public void SetPan(int pan,int frame){} }

    private void EnsureSourceMoveAssets()
    {
        if(_sourceAnimationResources is not null)return;
        _sourceAnimationResources=new(System.IO.Path.Combine(_root,"content/generated/battle/animations"));
        _sourceMoveAssets.Add("impact",_sourceAnimationResources.Asset("impact"));
        string folder;
        folder=System.IO.Path.Combine(_root,"content/generated/battle/art/campaign-hit-art");
        var bytes=System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,"manifest.json"));
        using var doc=JsonDocument.Parse(bytes);
        foreach(var row in doc.RootElement.GetProperty("geometry").EnumerateArray())
        {
            string side=row.GetProperty("side").GetString()!;
            _sourceBattlerGeometry.Add((row.GetProperty("species").GetInt32(),side),new(new(side=="back"?0:1),"",row.GetProperty("yOffset").GetInt32(),row.GetProperty("elevation").GetInt32(),row.GetProperty("sourceWidth").GetInt32(),row.GetProperty("sourceHeight").GetInt32()));
        }
        VerifyCelioMoveGeometry();
        VerifyMewtwoMoveGeometry();
        VerifyLeagueRematchMoveGeometry();
        VerifySevenIslandMoveGeometry();VerifyEventIslandMoveGeometry();VerifyPowerPlantMoveGeometry();AddTrainerTowerMoveGeometry();VerifyFossilLabMoveGeometry();VerifySeafoamMoveGeometry();VerifyQwilfishMoveGeometry();VerifyStoneEvolutionMoveGeometry();VerifyGameCornerMoveGeometry();VerifyBreedingMoveGeometry();VerifyOrdinaryEvolutionMoveGeometry();AddMemorialPillarMoveGeometry();VerifyGiftEggMoveGeometry();
        foreach(var row in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            string path=row.GetProperty("path").GetString()!,id=row.GetProperty("id").GetString()!;
            if(System.IO.Path.GetFileName(path)!=path)throw new InvalidDataException("Campaign hit art path differs.");
            var data=System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,path));
            if(BinaryPrimitives.ReadUInt16LittleEndian(data)!=64||BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2))!=64)throw new InvalidDataException("Substitute image differs.");
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
            RefreshCompletedSourceState(true);
            if(TryStartSourceResultMovePresentation(true))return;
            SynchronizeSourceResultPresentation();return;
        }
        var b=PresentedWildBattle;
        if(b is null||!SourceTrainerPresentationActive(b))
        { if(_sourceVisualBattle is not null)ResetSourceMoveAnimation("battle-exit");return; }
        if(_sourceVisualBattle!=b.Id)
        {
            ResetSourceMoveAnimation("battle-replaced");_sourceVisualBattle=b.Id;

        }
        if(_sourceMoveContinuation is {} pending && (_sourceMoveSession!=_session || _sourceMoveRevision!=_session.Revision || WildCurrentEvent(b)?.Sequence!=pending.Sequence))
            CancelSourceMoveAnimation("context-changed");
        // The shared event-time body reducer owns substitute creation, damage and
        // replacement. A new host at this cursor must not reverse the final frame.
        foreach(var side in new[]{WildBattleSide.Player,WildBattleSide.Opponent})
        {
            var body=SingleDisplayedBody(b,side)??throw new InvalidDataException("Source display body is missing.");
            _sourceSubstituteHp[side]=body.Pokemon.CurrentHp>0?body.CalledVolatile?.SubstituteHp??0:0;
        }
        RefreshCompletedSourceState(false);
        if(TryStartSourceResultMovePresentation(false))return;
        SynchronizeSourceResultPresentation();
    }
    private bool SourceMoveScriptAvailable(int id,string move)=>_sourceAnimationResources!.Scripts.TryGetValue(id,out var script)&&move.StartsWith("pret:MOVE_",StringComparison.Ordinal)&&script.Label=="Move_"+move[10..];
    private bool TryStartSourceResultMovePresentation(bool doubles)
    {
        if(_sourceMoveContinuation is not null||_sourceResultContinuation is not null)return true;
        EnsureSourceMoveAssets();
        var decision=doubles?CampaignMoveAnimationPresentation.AtResult(_session.State.DoubleBattle!,_session.EffectiveOptions.BattleScene,_catalog.PokemonContent!.Catalog.Moves,SourceMoveScriptAvailable):
            CampaignMoveAnimationPresentation.AtResult(PresentedWildBattle!,SourceTrainerPresentationActive(PresentedWildBattle!),_session.EffectiveOptions.BattleScene,_catalog.PokemonContent!.Catalog.Moves,SourceMoveScriptAvailable);
        if(decision.Dispatch is not {} dispatch)return false;
        EnsureSourceMoveAssets();var (assets,battlers)=CreateSourceAnimationScene(doubles,dispatch);
        return StartSourceMoveAnimation(dispatch,assets,battlers,doubles);
    }
    private bool DrawSourceSubstitute(WildBattleSide side)
    {
        if(_sourceSubstituteHp.GetValueOrDefault(side)<=0||SourceSubstituteRevealed(false,(int)side))return false;
        EnsureSourceMoveAssets();DrawTexture(_sourceSubstituteTextures[side==WildBattleSide.Player?"substitute-back":"substitute-front"],side==WildBattleSide.Player?new(40,65):new(144,24));return true;
    }
    private void AcknowledgeWildAttackText()
    {
        if(TryStartAfterTextGeneral(false))return;
        var b=PresentedWildBattle!;
        EnsureSourceMoveAssets();
        var decision=CampaignMoveAnimationPresentation.AtAttackText(b,SourceTrainerPresentationActive(b),_session.EffectiveOptions.BattleScene,_catalog.PokemonContent!.Catalog.Moves,SourceMoveScriptAvailable);
        if(decision.Dispatch is not {} dispatch){AcknowledgePresentedSingleBattle();return;}
        if(SourceAnimationBlocking)return;
        EnsureSourceMoveAssets();var (assets,battlers)=CreateSourceAnimationScene(false);
        if(!StartSourceMoveAnimation(dispatch,assets,battlers,false))AcknowledgePresentedSingleBattle();
    }
    private void AcknowledgeDoubleAttackText()
    {
        if(TryStartAfterTextGeneral(true))return;
        var b=_session.State.DoubleBattle!;
        EnsureSourceMoveAssets();
        var decision=CampaignMoveAnimationPresentation.AtAttackText(b,_session.EffectiveOptions.BattleScene,_catalog.PokemonContent!.Catalog.Moves,SourceMoveScriptAvailable);
        if(decision.Dispatch is not {} dispatch){DoubleSubmit(new("ack"));return;}
        if(SourceAnimationBlocking)return;
        EnsureSourceMoveAssets();var (assets,battlers)=CreateSourceAnimationScene(true);
        if(!StartSourceMoveAnimation(dispatch,assets,battlers,true))DoubleSubmit(new("ack"));
    }
    private (Dictionary<string,SourcePixelAsset> Assets,List<AnimationBattler> Battlers) CreateSourceAnimationScene(bool doubles,CampaignMoveAnimationDispatch? hit=null)
    {
        if(!doubles)
        {
            var b=PresentedWildBattle!;
        var assets=new Dictionary<string,SourcePixelAsset>(_sourceMoveAssets){{"background",SourcePixels(CurrentWildBattleTerrain,240,112)}};
        var battlers=new List<AnimationBattler>();
        foreach(var (side,mon) in new[]{(WildBattleSide.Player,WildDisplayPlayer(b)),(WildBattleSide.Opponent,b.DisplayOpponent)})
        {
            var appearance=FuchsiaBattleAppearance(b,side,mon);string face=side==WildBattleSide.Player?"back":"front";
            var displayed=SingleBattleArt(b,side,mon);
            var sourceGeometry=BattleForms.HasForms(appearance.Species)||SingleDisplayedBody(b,side)?.Transform is not null
                ?FormBattleMaterial(SingleDisplayedBody(b,side)??throw new InvalidDataException("Source form display body is missing."),new((int)side)).Geometry
                :_sourceBattlerGeometry[(appearance.Species,face)];
            var geometry=sourceGeometry with{Slot=new((int)side),Asset="battler-"+(int)side,Visible=!SingleBattlerFaintShown(b,mon,side)};
            var center=geometry.Center(false);
            if(displayed.Position!=new Vector2(center.X-32,center.Y-32))throw new InvalidDataException("Authenticated campaign/source battler geometry differs.");
            bool substitute=hit is {Boundary:CampaignMoveAnimationBoundary.BeforeHitResult}&&hit.Target.Value==(int)side?hit.HitsSubstitute:_sourceSubstituteHp.GetValueOrDefault(side)>0;
            substitute&=!SourceSubstituteRevealed(doubles,geometry.Slot.Value);
            assets.Add(geometry.Asset,substitute?_sourceMoveAssets["substitute-"+face]:SourcePixels(displayed.Texture,64,64));
            if(substitute)geometry=geometry with{DisplayCenter=new(side==WildBattleSide.Player?72:176,side==WildBattleSide.Player?97:56)};
            battlers.Add(geometry);
        }
            _sourceCompletedState.AddMaterials(assets);
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
            var displayed=DoubleBattleArt(body,slot);
            var sourceGeometry=BattleForms.HasForms(PresentedRenderSpecies(appearance))||body.Transform is not null
                ?FormBattleMaterial(body,new(slot)).Geometry:_sourceBattlerGeometry[(PresentedRenderSpecies(appearance),face)];
            var geometry=sourceGeometry with{Slot=new(slot),Asset="battler-"+slot,Visible=!DoubleBattlerFaintShown(_session.State.DoubleBattle!,mon,slot)};
            var center=geometry.Center(true);
            if(displayed.Position+DoubleSpriteOffsets[slot]!=new Vector2(center.X-32,center.Y-32))throw new InvalidDataException("Authenticated doubles/source battler geometry differs.");
            bool substitute=hit is {Boundary:CampaignMoveAnimationBoundary.BeforeHitResult}&&hit.Target.Value==slot?hit.HitsSubstitute:body.CalledVolatile?.SubstituteHp>0;
            substitute&=!SourceSubstituteRevealed(doubles,geometry.Slot.Value);
            assets.Add(geometry.Asset,substitute?_sourceMoveAssets["substitute-"+face]:SourcePixels(displayed.Texture,64,64));
            if(substitute)
            {
                var offset=DoubleSpriteOffsets[slot];
                geometry=geometry with{DisplayCenter=new((slot&1)==0?72+(int)offset.X:176+(int)offset.X,(slot&1)==0?97+(int)offset.Y:56+(int)offset.Y)};
            }
            battlers.Add(geometry);
        }
            _sourceCompletedState.AddMaterials(assets);
            return (assets,battlers);
        }
    }
    private IReadOnlyDictionary<int,int> SourceAnimationPartySpecies(bool doubles)
    {
        var species=new Dictionary<int,int>();
        for(int slot=0;slot<(doubles?4:2);slot++)
        {
            var body=doubles?DoubleDisplayedBody(slot):SingleDisplayedBody(PresentedWildBattle!, (WildBattleSide)slot);
            // LaunchBattleAnimation snapshots the original party identity, even
            // while Transform supplies another species for the displayed body.
            if(body is not null)species.Add(slot,(body.Transform?.OriginalPokemon??body.Pokemon).SourceSpeciesId);
        }
        return species;
    }
    private static bool ApplySourceAnimationVisibility(List<AnimationBattler> battlers,IReadOnlyList<bool> visibility)
    {
        if(visibility.Count!=battlers.Count)return false;
        for(int i=0;i<battlers.Count;i++)
        {
            // Faint presentation owns presence; committed HP may already be zero.
            // Charge replay owns the current source invisible flags.
            bool present=battlers[i].Visible,hidden=!visibility[battlers[i].Slot.Value];
            battlers[i]=battlers[i] with{Visible=present&&!hidden,SourceVisibility=new(present,hidden,hidden)};
        }
        return true;
    }
    private bool StartSourceMoveAnimation(CampaignMoveAnimationDispatch dispatch,Dictionary<string,SourcePixelAsset> assets,List<AnimationBattler> battlers,bool doubles)
    {
        if(_sourceResultEntryCharge is null)return false;
        var sourceEvents=doubles?_session.State.DoubleBattle!.Events:PresentedWildBattle!.Events;
        var visibility=CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(sourceEvents,dispatch,_sourceResultEntryCharge);
        if(visibility is null||!ApplySourceAnimationVisibility(battlers,visibility))return false;
        var resolved=_sourceAnimationResources!.Resolve(dispatch.SourceMoveId);
        var script=resolved.Script;foreach(var asset in resolved.Assets)assets[asset.Key]=asset.Value;
        SourceBattlerFormMaterial? additionalMon=null;
        if(script.Commands.Any(c=>c.Op=="createvisualtask"&&c.Args.FirstOrDefault()=="AnimTask_RolePlaySilhouette"))
        {
            additionalMon=AdditionalMonAnimationMaterial(dispatch,doubles);
            if(additionalMon is null)return false;
        }
        var actorBody=doubles?DoubleDisplayedBody(dispatch.Actor.Value):SingleDisplayedBody(PresentedWildBattle!,(WildBattleSide)dispatch.Actor.Value);
        bool behindSubstitute=actorBody?.CalledVolatile?.SubstituteHp>0;
        var controllerInitial=battlers.ToArray();
        if(behindSubstitute)
        {
            int actorIndex=battlers.FindIndex(b=>b.Slot.Value==dispatch.Actor.Value);
            battlers[actorIndex]=SubstituteControllerBody(actorBody!,battlers[actorIndex],assets);
        }
        Dictionary<int,AnimationBattler>? replacements=null;
        if(script.Commands.Any(c=>c.Op=="createvisualtask"&&c.Args.FirstOrDefault() is "AnimTask_TransformMon" or "AnimTask_MonToSubstitute")&&
            !PrepareMoveBattlerMaterial(dispatch,doubles,assets,battlers,out replacements))return false;
        var backgroundSurface=SourceTerrainSurface(assets["background"]);var backgroundPalette=Enumerable.Range(0,16).Select(i=>backgroundSurface.PaletteColor(2,i)).ToArray();
        var cue=new CommittedMoveAnimation(dispatch.BattleId,dispatch.Sequence,dispatch.SourceMoveId,new(dispatch.Actor.Value),new(dispatch.Target.Value)){MoveTurn=dispatch.MoveTurn,MovePower=dispatch.MovePower,FuryCutterCounter=dispatch.FuryCutterCounter,RolloutCounter=dispatch.RolloutCounter,MoveDamage=dispatch.MoveDamage,AnimationWeather=dispatch.AnimationWeather,IsHealingMove=dispatch.IsHealingMove};
        // Domain terrain is captured at battle entry and is independent of the
        // rendered backdrop. Camouflage consumes only the native terrain range.
        var terrain=(doubles?_session.State.DoubleBattle!.Frame:PresentedWildBattle!.Frame).Field.Terrain;
        if(terrain is {} value&&(int)value is >=0 and <=9)cue=cue with{SourceTerrain=(SourceAnimationTerrain)(int)value};
        // The controller captures gBattleMons[attacker].friendship at launch.
        // The event-time transformed battle body retains its actor's friendship.
        if(actorBody is not null)cue=cue with{Friendship=checked((byte)actorBody.Pokemon.Friendship)};
        var doubleBattle=doubles?_session.State.DoubleBattle:null;
        var singleBattle=doubles?null:PresentedWildBattle;
        var display=doubleBattle is not null
            ?new CampaignBattleDisplayBodies(doubleBattle.DisplayedSlots,doubleBattle.DisplayedMembers)
            :singleBattle?.DisplayBatch?.Display;
        if(display is not null)cue=cue with{ActorHpQuarterTier=CampaignMoveAnimationPresentation.ActorHpQuarterTierAtInvocation(
            dispatch,display,sourceEvents,doubleBattle?.EventIndex??singleBattle!.DisplayBatch!.NextEventIndex,
            _catalog.PokemonContent!.Catalog.Moves)};
        cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
        _sourceMoveSound=_sourceAudio is SynthSourceAudioPlayer synth&&!_audioFaulted?new(synth,c=>Write("sourceMoveAnimationSound",new{dispatch.BattleId,dispatch.Sequence,c.Id,c.Frame,c.Pan,panningImplemented=_sourceMoveSound?.PanningImplemented==true})):null;
        try
        {
            ISourceAnimationAudio audio=(ISourceAnimationAudio?)_sourceMoveSound??new SilentMoveAudio();
            if(!behindSubstitute)
            {
                if(!_sourceMoveAnimationGate.TryStart(cue,()=>new(script,cue,battlers,doubles,audio,backgroundPalette,battlerReplacements:replacements,spriteScene:_sourceSpriteScene,spriteMaterials:assets,originalPartySpecies:SourceAnimationPartySpecies(doubles),additionalMonMaterial:additionalMon)))return false;
                _sourceMoveRestoresSubstitute=false;
            }
            else
            {
                // Validate every selected phase before revealing the doll. Only the
                // controller's current runtime attaches to the shared sprite scene.
                bool reveal=!SourceSubstituteRevealed(doubles,dispatch.Actor.Value);
                bool restore=dispatch.SubstituteControllerHitCounter<2;
                var body=battlers.Single(b=>b.Slot.Value==dispatch.Actor.Value);
                var finalBody=replacements?.GetValueOrDefault(dispatch.Actor.Value)??body;
                SourceBattleAnimation Prepared(SourceAnimationScript program,CommittedMoveAnimation stageCue,
                    IReadOnlyList<AnimationBattler> stageBattlers,IReadOnlyDictionary<int,AnimationBattler>? stageReplacements)=>
                    new(program,stageCue,stageBattlers,doubles,audio,deferSceneAttachment:true,backgroundPalette:backgroundPalette,
                        battlerReplacements:stageReplacements,spriteScene:_sourceSpriteScene,spriteMaterials:assets,
                        originalPartySpecies:SourceAnimationPartySpecies(doubles),additionalMonMaterial:additionalMon);
                SourceBattleAnimation Special(string label,IReadOnlyList<AnimationBattler> initial,AnimationBattler replacement)
                {
                    var named=_sourceAnimationResources.ResolveGeneral(label);
                    foreach(var asset in named.Assets)assets[asset.Key]=asset.Value;
                    var specialCue=cue with{MoveId=0,Target=cue.Actor,GeneralAnimationLabel=label};
                    return Prepared(named.Script,specialCue,initial,new Dictionary<int,AnimationBattler>{{dispatch.Actor.Value,replacement}});
                }
                if(!_sourceMoveAnimationGate.TryStartSequence(cue,()=>new SourceMoveAnimationSequence(
                    Prepared(script,cue,battlers,replacements),
                    reveal?Special("Special_SubstituteToMon",controllerInitial,body):null,
                    restore?Special("Special_MonToSubstitute",battlers,SubstituteControllerDoll(finalBody,doubles)):null)))return false;
                _sourceSubstituteLifetime.Reveal(dispatch.Actor.Value,SourceDisplayedMember(doubles,dispatch.Actor.Value));
                _sourceMoveRestoresSubstitute=restore;
            }
        }
        catch(SourceAnimationException e) when(e.Diagnostic.Reason=="source-cry-audio-unavailable")
        {
            Write("sourceMoveAnimationUnavailable",new{dispatch,e.Diagnostic});return false;
        }
        ClearCompletedSourceState(preserveChargeVisibility:true);_sourceMoveInitialFrame=_sourceMoveAnimationGate.Current!.Snapshot();_sourceMoveMaterials=assets;
        _sourceMoveComposer=new(assets,battlers.ToArray(),statMaterial:resolved.StatMask,metallicMaterial:resolved.Metallic,backgroundSurface:backgroundSurface,moveBackgrounds:resolved.Backgrounds,overlays:resolved.Overlays);_sourceMoveContinuation=dispatch;_sourceMoveSession=_session;_sourceMoveRevision=_session.Revision;_sourceMoveRemainder=0;_sourceMoveDoubles=doubles;
        PresentSourceMoveFrame(_sourceMoveAnimationGate.Tick());
        Write("sourceMoveAnimationStarted",new{dispatch,doubles,revision=_sourceMoveRevision,actors=battlers.Select(x=>new{slot=x.Slot.Value,x.Asset,x.YOffset,x.Elevation,x.SourceWidth,x.SourceHeight,x.DisplayCenter,x.Visible}),sourceAudioAvailable=_sourceMoveSound is not null,sourcePanApplied=_sourceMoveSound?.PanningImplemented==true});QueueRedraw();return true;
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
        PresentSourceMovePixels(_sourceMoveComposer!.ComposeField(frame));
    }
    private void PresentSourceMovePixels(byte[] rgba)
    {
        using var image=Image.CreateFromData(240,112,false,Image.Format.Rgba8,rgba);
        if(_sourceMoveField is null)_sourceMoveField=ImageTexture.CreateFromImage(image);else _sourceMoveField.Update(image);
    }
    private bool ProcessCommittedMoveAnimation(double delta)
    {
        ProcessResidualMoveSprites(delta);
        if(ProcessCommittedResultAnimation(delta))return true;
        if(_sourceMoveContinuation is not {} pending)return false;
        if(!SourceMoveContextMatches())
        {CancelSourceMoveAnimation("context-changed");return false;}
        _sourceMoveRemainder+=delta;
        while(_sourceMoveRemainder>=SourceBattleAnimation.SourceFrameSeconds&&_sourceMoveAnimationGate.IsBlocking)
        {_sourceMoveRemainder-=SourceBattleAnimation.SourceFrameSeconds;PresentSourceMoveFrame(_sourceMoveAnimationGate.Tick());}
        if(!_sourceMoveAnimationGate.IsBlocking)
        {
            // Retire the continuation before executing the sole original ACK. Reentrant Sync/input
            // sees no pending continuation; the gameplay move transaction is never replayed.
            var completed=_sourceMoveAnimationGate.Current!.Snapshot();
            int finishedFrame=completed.Frame;
            if(_sourceMoveRestoresSubstitute)_sourceSubstituteLifetime.Restore(pending.Actor.Value);
            _sourceMoveRestoresSubstitute=false;
            var completedEvents=_sourceMoveDoubles?_session.State.DoubleBattle!.Events:PresentedWildBattle!.Events;
            if(completed.BattlerVisibility.Any(v=>v.Slot==pending.Actor.Value&&!v.SpriteInvisible)&&_sourceChargePoseLifetime.CompleteRelease(pending,completedEvents))_sourceCompletedState.DropVisibility(pending.Actor.Value);
            if(_sourceMoveInitialFrame is {} initial)_sourceCompletedState.Capture(initial,completed,_sourceMoveMaterials);
            if(_sourceChargePoseLifetime.Capture(pending,completedEvents)&&!_sourceCompletedState.RetainVisibility(completed,pending.Actor.Value))
                _sourceChargePoseLifetime.DropSlot(pending.Actor.Value);
            foreach(int slot in _sourceCompletedState.Slots)_sourceCompletedMembers[slot]=SourceDisplayedMember(_sourceMoveDoubles,slot);
            _sourceCompletedMaterialSequence=pending.ResultSequence;
            _sourceMoveInitialFrame=null;_sourceMoveMaterials=null;
            RetainResidualMoveSprites();
            _sourceIdleSpritesPresented=_sourceCompletedState.HasState;
            _sourceMoveContinuation=null;_sourceMoveSession=null;_sourceMoveComposer=null;_sourceMoveSound=null;
            bool acknowledge=pending.AcknowledgesCurrentReceipt;
            _wildElapsed=_doubleElapsed=0;
            Write("sourceMoveAnimationFinished",new{pending.BattleId,pending.Sequence,sourceFrame=finishedFrame,acknowledged=acknowledge});
            if(acknowledge)AcknowledgePresentedSingleBattle();
        }
        QueueRedraw();return true;
    }
    private void CancelSourceMoveAnimation(string reason)
    {
        _sourceMoveAnimationGate.Cancel();ClearResidualMoveSprites();CancelSourceResultAnimation(reason);
        var pending=_sourceMoveContinuation;_sourceMoveContinuation=null;_sourceMoveSession=null;
        _sourceMoveRestoresSubstitute=false;_sourceMoveSound=null;_sourceMoveComposer=null;_sourceMoveRemainder=0;
        if(pending is not null)Write("sourceMoveAnimationCancelled",new{pending.BattleId,pending.Sequence,reason});
    }
    private void ResetSourceMoveAnimation(string reason)
    {
        CancelSourceMoveAnimation(reason);_sourceMoveAnimationGate.Reset();_sourceResultAnimationGate.Reset();_sourceVisualBattle=null;_sourceSubstituteHp.Clear();
    }
    private void DisposeSourceMoveAnimation()
    {
        ClearSourceResultState();ResetSourceMoveAnimation("exit-tree");_sourceMoveField?.Dispose();_sourceMoveField=null;_sourceAnimationResources?.Dispose();_sourceAnimationResources=null;_sourceTerrainIds.Clear();
        foreach(var texture in _sourceSubstituteTextures.Values)texture.Dispose();_sourceSubstituteTextures.Clear();_sourceMoveAssets.Clear();_sourceBattlerGeometry.Clear();
    }
}
