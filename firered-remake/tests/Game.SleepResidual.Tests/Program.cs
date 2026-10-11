using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if(args.Length!=3)throw new ArgumentException("PRODUCT_ROOT SOURCE_VECTORS_JSON OUTPUT_JSON (or baseline PRODUCT_ROOT OUTPUT_JSON)");
if(args[0]=="baseline"){
 if(File.Exists(args[2]))throw new InvalidOperationException("Never replace frozen baseline");
 File.WriteAllText(args[2],LegacyParity.Run(args[1])+"\n");using var captured=JsonDocument.Parse(File.ReadAllText(args[2]));Console.WriteLine(captured.RootElement.GetProperty("cases"));return 0;
}
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
using var source=JsonDocument.Parse(File.ReadAllBytes(args[1]));
int groups=0,checks=0,failures=0,turns=0,oracleCases=0;var results=new List<object>();
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
void Test(string label,Action work){groups++;try{work();results.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception e){failures++;results.Add(new{label,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+label+" "+e);}}
string Json<T>(T value)=>JsonSerializer.Serialize(value);
MoveId Id(string name)=>new("pret:MOVE_"+name);
WildBattleCombatant Mon(string species,string moves="DEFENSE_CURL",int level=50){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});}
WildBattleCalledPartyMember Member(WildBattleCombatant b)=>new(b.Pokemon,b.HeldItem,b.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? mons=null,WildBattleCombatant[]? players=null,WildBattleCombatant[]? opponents=null,uint seed=31){
 var m=mons??[Mon("PIDGEY"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")];
 var p=new[]{Member(m[0]),Member(m[2])}.Concat((players??[]).Select(Member)).ToImmutableArray();
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,p),new(BattleSide.Opponent,new[]{Member(m[1]),Member(m[3])}.Concat((opponents??[]).Select(Member)).ToImmutableArray())],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
 Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,p.Length).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};}
SourceBattleFrame Put(SourceBattleFrame f,int i,WildBattleCombatant b){var c=f.Slots[i];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=b}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(b))})};}
SourceBattleFrame Hp(SourceBattleFrame f,int slot,int hp)=>Put(f,slot,f.Slots[slot].Body! with{Pokemon=f.Slots[slot].Body!.Pokemon with{CurrentHp=hp}});
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int slot=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,slot,new(target)));Check(r.Succeeded,r.Fault??"action");Check(SourceBattleIdentityRules.Validate(r.State)==null,"action identity");return r;}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"turn");Check(SourceBattleIdentityRules.Validate(r.State)==null,"turn identity");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? slots=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int pos=slots?[c.Slot.Value]??0;var m=catalog.Moves[c.Body!.Pokemon.Moves[pos].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,new((m.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" || m.Effect=="EFFECT_CURSE"&&!WildBattleEffective.HasType(catalog,c.Body!,"TYPE_GHOST"))?c.Slot.Value:targets?[c.Slot.Value]??(c.Slot.Value^1)));}).ToImmutableArray();
SourceFourActionResult Begin(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? choices=null)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices??Choices(f)));
SourceTurnContinuation Token(SourceBattleFrame f)=>new(f.Revision,f.EventSequence,f.NextAction,f.FourTurn!.Phase);
SourceFourActionResult Replay(SourceFourActionResult r,Func<SourceBattleFrame,SourceFourActionResult> step){var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var a=step(r.State);var b=step(restored);Check(Json(a)==Json(b),"serialized exact state/events/RNG continuation");return Good(a);}
SourceFourActionResult Next(SourceFourActionResult r)=>Replay(r,f=>SourceFourActionTurns.Advance(catalog,f,Token(f)));
SourceFourActionResult EndBoundary(SourceBattleFrame f){var r=Begin(f);while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.EndTurn,"actual endturn boundary");return r;}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var e=r.Events.ToList();for(int i=0;i<30&&r.State.FourTurn!=null&&r.State.Outcome==WildBattleOutcome.Ongoing;i++){
 r=r.State.FourTurn!.Phase switch{
  SourceFourActionPhase.RewardsPending=>Replay(r,f=>SourceFourRewards.Settle(catalog,f,Token(f))),
  SourceFourActionPhase.ReplacementsPending=>Replay(r,f=>{var p=f.PendingSwitches[0];return SourceFourActionTurns.ChooseReplacement(catalog,f,Token(f),p.Slot,new(p.Slot.Side,p.LegalPartyIndices[0]));}),
  _=>Next(r)};e.AddRange(r.Events);
 }Check(r.CompletedCleanTurn,"one complete turn");turns++;return(r.State,e);}
SourceActionSelection Switch(SourceBattleFrame f,int slot,int incoming)=>new(f.Slots[slot].Occupant!.Value,SourceActionKind.Switch,Incoming:new((BattleSide)(slot&1),incoming));
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic "+why+": "+r.Fault);


Test("42 frozen singles fingerprints, 36 exact adapter actions and bounded gates",()=>{
 Check(Environment.Version.Major==10,"runtime10");
 Check(LegacyParity.Run(args[0],true)==File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../..","legacy-baseline.json")).Trim(),"frozen singles result/event/RNG hashes");
 foreach(var m in new[]{"NIGHTMARE","CURSE","YAWN"})Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(m)]),m+" admitted");
 foreach(var m in new[]{"UPROAR","METRONOME","TRANSFORM"})Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(m)]),m+" authenticated recipe admitted");
});
Test("Nightmare and Yawn blocking script order, zero draws and ally Pressure",()=>{
 var f=Frame([Mon("GASTLY","NIGHTMARE,YAWN,CURSE"),Mon("SNORLAX"),Mon("DUSCLOPS")with{Status=3},Mon("SNORLAX")]);
 var a=Act(f,0,2);Check(a.State.Slots[2].Body!.Nightmare&&a.Trace.IsEmpty&&a.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==2,"Nightmare actual ally Pressure");
 a=Act(Put(f,2,f.Slots[2].Body!with{Protected=true}),0,2);Check(a.State.Slots[2].Body!.Nightmare&&a.Trace.IsEmpty,"Nightmare source primary effect applies through Protect");
 a=Act(f,0,3);Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="target-awake"&&a.Trace.IsEmpty,"awake Nightmare fails without draw");
 f=Put(f,3,Mon("DROWZEE")with{Protected=true});f=f with{Sides=f.Sides.SetItem(1,f.Sides[1]with{SafeguardTurnsRemaining=4})};a=Act(f,0,3,1);Check(a.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability=="ABILITY_INSOMNIA"&&a.Trace.IsEmpty,"Yawn ability before safeguard/protect");
 f=Put(f,3,Mon("SNORLAX")with{Protected=true});a=Act(f,0,3,1);Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="safeguard","Safeguard before accuracy command");
 a=Act(f,0,3,2);Check(a.State.Slots[3].Body!.Cursed&&a.Events.OfType<SourceTowerHpEvent>().Single().Effect=="curse-cost"&&a.Trace.IsEmpty,"Ghost Curse ignores Protect and Safeguard");
 f=f with{Sides=f.Sides.SetItem(1,new(BattleSide.Opponent))};a=Act(f,0,3,1);Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="protected"&&a.Trace.IsEmpty,"Yawn Protect no accuracy draw");
 f=Put(f,3,Mon("SNORLAX"));a=Act(f,0,3,1);Check(a.State.Slots[3].Body!.YawnTurnsRemaining==2&&a.Trace.IsEmpty,"Yawn begins two-turn counter");
});
Test("Nightmare has no semi-invulnerable test; Yawn and Ghost Curse use no-roll accuracy",()=>{
 foreach(var charge in new[]{"FLY","DIG","DIVE"}){
  var f=Frame([Mon("GASTLY","NIGHTMARE,CURSE,YAWN"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX",charge)with{Status=3}]);
  var cell=f.Slots[3];f=f with{Slots=f.Slots.SetItem(3,cell with{Registers=cell.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Charge,Id(charge),0,Id(charge),new(0),1,0,null,false),0,null)}})};
  var a=Act(f,0,3);Check(a.State.Slots[3].Body!.Nightmare&&a.Trace.IsEmpty,"Nightmare during "+charge);
  foreach(int move in new[]{1,2}){a=Act(f,0,3,move);Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="semi-invulnerable"&&a.Trace.IsEmpty,"no-roll accuracy during "+charge);}
 }
});
Test("unchanged C Curse and Yawn commands through actual four-slot recipes",()=>{
 foreach(var v in source.RootElement.GetProperty("groups").GetProperty("curseApplication").GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();
  var f=Frame();var actor=Mon("GASTLY","CURSE");f=Put(f,x[1],actor with{Pokemon=actor.Pokemon with{Stats=actor.Pokemon.Stats with{MaxHp=x[3]},CurrentHp=x[4]}});f=Put(f,x[2],Mon("SNORLAX")with{Cursed=x[5]!=0});
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.FrameRecipeContext")!;
  // Invoke the actual recipe host with synthetic HP maxima used by unchanged C;
  // factory-valid public action and full-turn admission are covered separately.
  object context=Activator.CreateInstance(type,flags|BindingFlags.Public,null,[catalog,f,new SourceResolvedMoveAction(f.Slots[x[1]].Occupant!.Value,f.Revision,0,new(x[2])),null],null)!;
  type.GetMethod("Run",flags)!.Invoke(context,null);var actual=(SourceBattleFrame)type.GetProperty("Frame",flags)!.GetValue(context)!;var events=(ImmutableArray<SourceBattleEvent>.Builder)type.GetField("Events",flags)!.GetValue(context)!;
  Check(actual.Slots[x[2]].Body!.Cursed==(y[3]!=0)&&events.OfType<SourceMoveFailedEvent>().Any()==(y[0]!=0),"C cursed duplicate result");
  Check(actual.Slots[x[1]].Body!.Pokemon.CurrentHp==y[2]&&(y[0]!=0||events.OfType<SourceTowerHpEvent>().Single().Damage==y[1]),"C exact half-maxHP minimum, clipping and KO");oracleCases++;
 }
 foreach(var v in source.RootElement.GetProperty("groups").GetProperty("yawnApplication").GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();int actor=x[1]^1;
  var f=Put(Frame(),actor,Mon("PIDGEY","YAWN"));f=Put(f,x[1],Mon("SNORLAX")with{YawnTurnsRemaining=x[2],Status=(uint)x[3]});var a=Act(f,actor,x[1]);
  Check(a.State.Slots[x[1]].Body!.YawnTurnsRemaining==y[1]&&a.Trace.IsEmpty,"C Yawn duplicate/status/counter");oracleCases++;
 }
});
Test("unchanged C residual arithmetic, status timer, absence and multi-turn cancellation",()=>{
 var prepared=EndBoundary(Frame(players:[Mon("PIDGEY")],opponents:[Mon("RATTATA")])).State;
 var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.FrameEndTurnContext")!;var baseType=type.BaseType!;
 (SourceBattleFrame State,SourceBattleEvent[] Events,Game.Domain.Randomness.RandomTrace[] Trace) Command(SourceBattleFrame f,int slot,string method){object c=Activator.CreateInstance(type,flags|BindingFlags.Public,null,[f],null)!;
  if(!f.IsSourceAbsent(new(slot)))baseType.GetMethod(method,flags)!.Invoke(c,[new BattlerSlot(slot)]);
  return((SourceBattleFrame)type.GetProperty("Frame",flags)!.GetValue(c)!,((ImmutableArray<SourceBattleEvent>.Builder)type.GetField("Events",flags)!.GetValue(c)!).ToArray(),((ImmutableArray<Game.Domain.Randomness.RandomTrace>.Builder)type.GetField("Trace",flags)!.GetValue(c)!).ToArray());}
 foreach(var group in new[]{"nightmareResidual","curseResidual"})foreach(var v in source.RootElement.GetProperty("groups").GetProperty(group).GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();bool nightmare=group=="nightmareResidual";
  var b=prepared.Slots[x[1]].Body!;var f=Put(prepared,x[1],b with{Nightmare=nightmare&&x[4]!=0,Cursed=!nightmare&&x[4]!=0,Status=nightmare?(uint)x[5]:0,Pokemon=b.Pokemon with{Stats=b.Pokemon.Stats with{MaxHp=x[2]},CurrentHp=x[3]}})with{SourceAbsentMask=(byte)x[nightmare?6:5]};
  var a=Command(f,x[1],nightmare?"NightmareResidual":"CurseResidual");Check(a.State.Slots[x[1]].Body!.Pokemon.CurrentHp==y[2]&&a.Events.OfType<SourceTowerHpEvent>().Count()==y[0],"C residual eligibility and clipped HP");
  if(y[0]!=0)Check(a.Events.OfType<SourceTowerHpEvent>().Single().Damage==y[1],"C nominal quarter damage");
  if(nightmare&&!(y[0]!=0&&y[2]==0))Check(a.State.Slots[x[1]].Body!.Nightmare==(y[3]!=0),"source awake flag removal");Check(a.Trace.Length==0,"residual has no damage RNG");oracleCases++;
 }
 foreach(var v in source.RootElement.GetProperty("groups").GetProperty("yawnResidual").GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();if(x[7]!=0)continue;
  uint seed=Enumerable.Range(0,1024).Select(i=>(uint)i).First(i=>(Game.Domain.Randomness.Gen3Rng.Next(new(i),"test",4).Value&3)==x[6]);
  var b=Mon("SNORLAX","DIG");b=b with{YawnTurnsRemaining=x[2],Status=(uint)x[3],Pokemon=b.Pokemon with{AbilityId=x[4]==72?"ABILITY_VITAL_SPIRIT":x[4]==15?"ABILITY_INSOMNIA":"ABILITY_NONE"}};
  var f=Put(prepared,x[1],b)with{SourceAbsentMask=(byte)x[5],Rng=new(seed)};var c=f.Slots[x[1]];f=f with{Slots=f.Slots.SetItem(x[1],c with{Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Charge,Id("DIG"),0,Id("DIG"),new(x[1]^1),1,0,null,false),0,null)}})};
  var a=Command(f,x[1],"YawnResidual");Check(a.State.Slots[x[1]].Body!.YawnTurnsRemaining==y[1]&&a.State.Slots[x[1]].Body!.Status==(uint)y[2]&&a.Trace.Length==y[3],"C Yawn timing/status/draw");
  Check((a.State.Slots[x[1]].Registers.Turn?.Lock is null)==(y[5]==0),"C cancel semi-invulnerability exactly on sleep application");oracleCases++;
 }
});
Test("verbatim C switch-transfer masks clear Nightmare/Yawn and Baton carries Curse",()=>{
 var type=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.SourceSwitchRules")!;var method=type.GetMethod("PrepareBody",BindingFlags.Static|BindingFlags.NonPublic)!;
 foreach(var v in source.RootElement.GetProperty("groups").GetProperty("switchTransfer").GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();
  var outgoing=Mon("SNORLAX")with{Nightmare=true,Cursed=true,Status=3,YawnTurnsRemaining=2};var result=(WildBattleCombatant)method.Invoke(null,[outgoing,Member(Mon("RATTATA")),x[2]!=0])!;
  Check(result.Cursed==(y[0]!=0)&&result.Nightmare==(y[1]!=0)&&result.YawnTurnsRemaining==y[2],"unchanged C source Baton/ordinary mask");oracleCases++;
 }
});
Test("canonical singles Substitute/ability/Safeguard order and Nightmare Protect correction",()=>{
 WildBattleState State(WildBattleCombatant actor,WildBattleCombatant target)=>new(actor,target,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,CalledField=new()};
 var actor=Mon("GASTLY","NIGHTMARE,CURSE,YAWN");var target=Mon("SNORLAX")with{Status=3,Protected=true};
 var action=WildBattleRules.ResolveSourceMoveAction(catalog,State(actor,target),new(WildBattleSide.Player,0));Check(action.Succeeded,action.Fault??"singles Nightmare");Check(action.State!.Opponent.Nightmare&&action.Battle.Trace.IsEmpty,"protected asleep Nightmare applies without RNG");
 target=target with{CalledVolatile=new(){SubstituteHp=20}};action=WildBattleRules.ResolveSourceMoveAction(catalog,State(actor,target),new(WildBattleSide.Player,0));Check(action.Succeeded&&action.Battle.Events.OfType<WildBattleMoveFailedEvent>().Single().Reason=="protected","Substitute stops Nightmare mechanically; resultmessage prints prior Protect miss");
 target=Mon("DROWZEE")with{CalledVolatile=new(){SubstituteHp=20}};action=WildBattleRules.ResolveSourceMoveAction(catalog,State(actor,target),new(WildBattleSide.Player,2));Check(action.Succeeded&&action.Battle.Events.OfType<WildBattleAbilityPreventedEvent>().Single().Ability=="ABILITY_INSOMNIA","Yawn ability takes precedence over Substitute");
 target=Mon("SNORLAX")with{CalledVolatile=new(){SubstituteHp=20}};var state=State(actor,target)with{CalledField=new(){OpponentSafeguardTurnsRemaining=3}};action=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,2));Check(action.Succeeded&&action.Battle.Events.OfType<WildBattleMoveFailedEvent>().Single().Reason=="substitute","Yawn Substitute before Safeguard");
});
// The independent Substitute increment supplies its storage/admission. Register this
// additional group only when that feature is present; output explicitly records coverage.
bool substituteIntegration=SourceBattleKernel.SupportsRecipe(catalog.Moves[Id("SUBSTITUTE")]);
if(substituteIntegration)Test("combined Substitute script ordering uses the three shared recipes",()=>{
 var f=Frame([Mon("GASTLY","NIGHTMARE,CURSE,YAWN"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")with{Status=3,Protected=true,CalledVolatile=new(){SubstituteHp=20}}]);
 void Cost(SourceKernelResult a){Check(a.Trace.IsEmpty&&a.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==1&&a.Events.OfType<SourceMoveUsedEvent>().Count()==1,"one source PP command and no RNG");Check(a.State.Slots[3].Body!.CalledVolatile!.SubstituteHp==20,"Substitute HP preserved");}
 var a=Act(f,0,3);Cost(a);Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="protected"&&!a.State.Slots[3].Body!.Nightmare,"Nightmare Substitute check precedes source primary set");
 f=Put(f,3,Mon("DROWZEE")with{Protected=true,CalledVolatile=new(){SubstituteHp=20}});a=Act(f,0,3,2);Cost(a);Check(a.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability=="ABILITY_INSOMNIA"&&a.State.Slots[3].Body!.YawnTurnsRemaining==0,"Yawn immunity before Substitute despite Protect marker");
 f=Put(f,3,Mon("SNORLAX")with{Protected=true,CalledVolatile=new(){SubstituteHp=20}})with{Sides=f.Sides.SetItem(1,f.Sides[1]with{SafeguardTurnsRemaining=3})};a=Act(f,0,3,2);Cost(a);Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="protected","Substitute stops Yawn before Safeguard; resultmessage retains Protect text priority");
 var safeguardControl=Act(Put(f,3,Mon("SNORLAX")with{Protected=true}),0,3,2);Check(safeguardControl.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="safeguard","direct Safeguard text bypasses resultmessage Protect priority");
 a=Act(f,0,3,1);Cost(a);Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="substitute"&&a.State.Slots[0].Body!.Pokemon.CurrentHp==f.Slots[0].Body!.Pokemon.CurrentHp,"Ghost Curse Substitute failure prevents HP cost");
 f=Put(f,3,Mon("SNORLAX")with{Protected=true,Status=3});a=Act(f,0,3);Check(a.State.Slots[3].Body!.Nightmare&&a.State.Slots[3].Body!.LastLandedMove?.Value=="pret:MOVE_UNAVAILABLE"&&a.State.Slots[3].Registers.History!.LastHitByType=="TYPE_NORMAL","no Substitute: Nightmare applies while Protect history remains missed");
});
Test("non-Ghost Curse self stats, caps, immunity bypass and Follow Me",()=>{
 var f=Frame([Mon("TENTACOOL","CURSE"),Mon("CLEFAIRY","FOLLOW_ME"),Mon("SPEAROW"),Mon("SNORLAX")]);
 var a=Act(f,0,0);var b=a.State.Slots[0].Body!;Check(b.SpeedStage==5&&b.AttackStage==7&&b.DefenseStage==7&&a.Trace.IsEmpty,"Clear Body does not stop Curse self drop");
 f=Put(f,0,f.Slots[0].Body!with{SpeedStage=0,AttackStage=12,DefenseStage=12});a=Act(f,0,0);Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="stat-limits","three caps fail");
 f=Put(f,0,Mon("TENTACOOL","CURSE"));var done=Finish(Begin(f));Check(done.State.Slots[0].Body!.SpeedStage==5&&!done.State.Slots[1].Body!.Cursed,"actual Follow Me cannot redirect non-Ghost self Curse");
});
Test("Nightmare then Curse in actual four-action scheduler, with independent same-species targets",()=>{
 var f=Frame([Mon("GASTLY","NIGHTMARE,CURSE,DEFENSE_CURL"),Mon("SNORLAX")with{Status=4,Cursed=true},Mon("GASTLY","NIGHTMARE,DEFENSE_CURL"),Mon("SNORLAX")with{Status=4}]);
 var done=Finish(Begin(f));foreach(int i in new[]{1,3})Check(done.Events.OfType<SourceTowerHpEvent>().Count(e=>e.Side.Value==i&&e.Effect=="nightmare")==1,"actual independent sleeping target residual");
 Check(done.Events.OfType<SourceTowerHpEvent>().Single(e=>e.Side.Value==1&&e.Effect=="nightmare").Sequence<done.Events.OfType<SourceTowerHpEvent>().Single(e=>e.Side.Value==1&&e.Effect=="curse").Sequence,"Nightmare before Curse");
 var next=Finish(Begin(done.State,Choices(done.State,[2,0,1,0])));Check(next.State.Slots[1].Body!.Status==2&&next.State.Slots[1].Body!.Nightmare,"sleep countdown retains Nightmare");
});
Test("Yawn two full turns, late Safeguard bypass, charge cancellation and Chesto item timing",()=>{
 var f=Frame([Mon("PIDGEY","YAWN,DEFENSE_CURL",70),Mon("SNORLAX","DEFENSE_CURL,DIG"),Mon("SPEAROW"),Mon("SNORLAX","DEFENSE_CURL,DIG")with{HeldItem="ITEM_CHESTO_BERRY"}]);
 // Apply the second recipient explicitly to keep two independent drowsy bodies.
 f=Act(f,0,3).State;var first=Finish(Begin(f));Check(first.State.Slots[1].Body!.YawnTurnsRemaining==1&&first.State.Slots[3].Body!.YawnTurnsRemaining==1,"same turn decrements to one");
 f=first.State with{Sides=first.State.Sides.SetItem(1,first.State.Sides[1]with{SafeguardTurnsRemaining=4})};var second=Finish(Begin(f,Choices(f,[1,1,0,1])));
 Check(WildBattleStatus.IsSleeping(second.State.Slots[1].Body!.Status)&&second.State.Slots[1].Registers.Turn?.Lock is null,"late Safeguard cannot stop Yawn, cancels Dig");
 Check(second.State.Slots[3].Body!.Status==0&&second.State.Slots[3].Body!.HeldItem=="ITEM_NONE"&&second.State.Slots[3].Registers.Turn?.Lock is null,"Chesto cures at Items2 after cancellation");
 var sleep=second.Events.OfType<SourceStatusChangedEvent>().Single(e=>e.Side.Value==3&&e.Reason=="yawn");Check(second.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Side.Value==3&&e.Reason=="yawn").Sequence<sleep.Sequence&&sleep.Sequence<second.Events.OfType<SourceHeldItemConsumedEvent>().Single().Sequence,"cancel then sleep then berry");
});
Test("wake, Shed Skin and berry cure clear Nightmare before residual damage",()=>{
 var f=Frame([Mon("PIDGEY")with{Status=1,Nightmare=true},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")]);var done=Finish(Begin(f));Check(!done.State.Slots[0].Body!.Nightmare&&!done.Events.OfType<SourceTowerHpEvent>().Any(),"woke-up clears Nightmare");
 f=Put(Frame(),0,Mon("PIDGEY")with{Status=4,Nightmare=true,HeldItem="ITEM_CHESTO_BERRY"});done=Finish(Begin(f));Check(!done.State.Slots[0].Body!.Nightmare&&!done.Events.OfType<SourceTowerHpEvent>().Any(),"berry clears Nightmare before residual");
 var boundary=EndBoundary(Frame()).State;f=Put(boundary,0,Mon("METAPOD")with{Status=4,Nightmare=true});
 uint seed=Enumerable.Range(0,64).Select(i=>(uint)i).First(i=>Game.Domain.Randomness.Gen3Rng.Next(SourceBattleTurnOrder.Resolve(catalog,f with{Rng=new(i)},WildBattleOrderPhase.EndTurn).Rng,"test",3).Value%3==0);f=f with{Rng=new(seed)};done=Finish(Next(new(f,[],[],null)));Check(done.State.Slots[0].Body!.Status==0&&!done.Events.OfType<SourceTowerHpEvent>().Any(),"Shed Skin clears before Nightmare step");
});
Test("switch, Baton Pass and source departure use body-local lifetimes",()=>{
 var f=Frame([Mon("GASTLY","CURSE,DEFENSE_CURL"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX","BATON_PASS")with{Cursed=true,YawnTurnsRemaining=2}],players:[Mon("PIDGEY")],opponents:[Mon("RATTATA")]);
 var r=Begin(f,Choices(f,[1,0,0,0]));while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ActionSwitchPending,"Baton selected");r=Replay(r,x=>SourceFourActionTurns.ChooseActionSwitch(catalog,x,Token(x),new(3),new(BattleSide.Opponent,2)));Check(r.State.Slots[3].Body!.Cursed&&r.State.Slots[3].Body!.YawnTurnsRemaining==0,"Baton transfers Curse, clears Yawn");Finish(r);
 f=Put(f,3,Mon("SNORLAX")with{Cursed=true,Nightmare=true,Status=4,YawnTurnsRemaining=2});r=Begin(f,Choices(f,[1,0,0,0]).SetItem(3,Switch(f,3,2)));r=Next(r);Check(!r.State.Slots[3].Body!.Cursed&&!r.State.Slots[3].Body!.Nightmare&&r.State.Slots[3].Body!.YawnTurnsRemaining==0,"ordinary switch clears all three");Finish(r);
 f=Act(Put(f,3,Mon("SNORLAX")),0,3).State;r=Begin(f,Choices(f,[1,0,0,0]).SetItem(0,Switch(f,0,2)));var completed=Finish(r);Check(completed.State.Slots[3].Body!.Cursed&&completed.Events.OfType<SourceTowerHpEvent>().Any(e=>e.Side.Value==3),"Curse persists after source departure without source pointer");
});
Test("Roar clears target flags, while self-target Ghost Curse uses source target RNG",()=>{
 var f=Frame([Mon("PIDGEY","ROAR",70),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")with{Cursed=true,YawnTurnsRemaining=2}],opponents:[Mon("RATTATA")]);
 var done=Finish(Begin(f,Choices(f,targets:[3,0,3,2])));Check(done.State.Slots[3].EntrySerial==2&&!done.State.Slots[3].Body!.Cursed&&done.State.Slots[3].Body!.YawnTurnsRemaining==0,"forced target departure clears status2/status3");
 f=Frame([Mon("GASTLY","CURSE",70),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")]);var r=Begin(f,Choices(f,targets:[0,0,3,2]));r=Next(r);Check(r.Trace.Any(t=>t.Purpose.Contains("selected-rejection"))&&r.State.Slots.Where(c=>c.Slot.Side==BattleSide.Opponent).Count(c=>c.Body!.Cursed)==1,"GhostCurse getmovetarget before action");Finish(r);
});
Test("Curse self KO and residual KO bypass Focus Band; actual reward and deferred replacements resume once",()=>{
 var f=Frame([Mon("GASTLY","CURSE",70)with{HeldItem="ITEM_FOCUS_BAND"},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")with{HeldItem="ITEM_FOCUS_BAND"}],players:[Mon("PIDGEY")],opponents:[Mon("RATTATA")]);f=Hp(Hp(f,0,1),3,1);
 var r=Begin(f,Choices(f,targets:[3,0,3,2]));r=Next(r);Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==0&&r.State.Slots[3].Body!.Cursed&&r.State.PendingSwitches.IsEmpty,"Curse self KO keeps cost and deferred vacancy");
 while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&r.State.DeferredKoVacancyMask==9,"residual KO reaches real EXP boundary before replacements");
 var done=Finish(r);Check(done.State.Slots[0].EntrySerial==2&&done.State.Slots[3].EntrySerial==2&&done.State.Turn==1,"both reserve occupants, single Ready");Check(done.Events.OfType<SourceOpponentRewardCompletedEvent>().Count()==1&&done.Events.OfType<SourceTowerHpEvent>().Count(e=>e.Effect=="curse")==1,"one reward and no residual replay");Check(done.Events.OfType<SourceRewardAppliedEvent>().Any(),"actual EXP recipient");
});
Test("current recipes and valid Perish Song are admitted while malformed state rejects atomically",()=>{
 var f=Frame();foreach(var name in new[]{"UPROAR","METRONOME","TRANSFORM"}){var current=Put(f,0,Mon("PIDGEY",name));var admitted=Begin(current);Check(admitted.State.Queue.Single(a=>a.Slot.Value==0).OriginalChosenMove==Id(name)&&admitted.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==current.Slots[0].Body!.Pokemon.Moves[0].CurrentPp,name+" admission retains chosen move and unspent PP");}
 var perish=Put(f,0,f.Slots[0].Body!with{CalledVolatile=new(){PerishSongTimer=2}});var accepted=Begin(perish);Check(accepted.State.Slots[0].Body!.CalledVolatile?.PerishSongTimer==2,"valid Perish Song retained through admission");
 foreach(var bad in new[]{Put(f,0,f.Slots[0].Body!with{Nightmare=true}),Put(f,0,f.Slots[0].Body!with{YawnTurnsRemaining=3}),Put(f,0,f.Slots[0].Body!with{CalledVolatile=new(){PerishSongTimer=4}}),Put(f,0,f.Slots[0].Body!with{RampageTurnsRemaining=2})})Atomic(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"invalid or deferred state");
});
Check(groups>0&&checks>0&&oracleCases>0,"nonempty suite");var result=new{groups,checks,failures,completedTurns=turns,oracleCases,substituteIntegration,runtime=Environment.Version.ToString(),results};File.WriteAllText(args[2],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true})+"\n");using var parsed=JsonDocument.Parse(File.ReadAllText(args[2]));Console.WriteLine(Json(result));return failures==0?0:1;
