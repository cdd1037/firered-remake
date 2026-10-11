using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 2) return 2;
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
int checks=0,groups=0,failures=0,oracle=0,turns=0,parity=0;var results=new List<object>();
void Check(bool ok,string label){checks++;if(!ok)throw new InvalidDataException(label);}
void Test(string label,Action action){groups++;try{action();results.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception ex){failures++;results.Add(new{label,passed=false,error=ex.ToString()});Console.WriteLine("FAIL "+label+" "+ex);}}
string Json<T>(T x)=>JsonSerializer.Serialize(x);
MoveId Id(string n)=>new("pret:MOVE_"+n);
WildBattleCombatant Mon(string species="RATTATA",string move="TACKLE",int level=50,uint pid=0){var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;return new(p with{Moves=[new(Id(move),catalog.Moves[Id(move)].Pp,catalog.Moves[Id(move)].Pp)]});}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,WildBattleTransformationRules.PersistentStatus(m));
SourceBattleFrame Frame(WildBattleCombatant[]? mons=null){var m=mons??[Mon(level:100),Mon(level:100),Mon(),Mon(level:100)];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};}
SourceBattleFrame Body(SourceBattleFrame f,int slot,WildBattleCombatant body){var c=f.Slots[slot];var key=c.Member!.Value;var party=f.Parties[(int)key.Side];return f with{Slots=f.Slots.SetItem(slot,c with{Body=body}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(body))})};}
SourceBattleFrame Recharge(SourceBattleFrame f,int slot,int count){if(count==0)return f;var c=f.Slots[slot];var move=c.Body!.Pokemon.Moves[0].Move;return f with{Slots=f.Slots.SetItem(slot,c with{Registers=c.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Recharge,Id("HYPER_BEAM"),0,move,new(slot^1),count,0,null,false),0,null)}})};}
SourceKernelResult Act(SourceBattleFrame f,int actor=2,int target=3)=>SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target)));
SourceKernelResult Accepted(SourceKernelResult r){Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid result");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(c.Slot.Value^1))).ToImmutableArray();
SourceFourActionResult AcceptedTurn(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"turn accepted");return r;}
SourceFourActionResult Begin(SourceBattleFrame f){
 SourceFourActionResult? entry=null;
 if(f.EntryAbilities is null&&f.Slots.Any(c=>SourceFourEntryAbilities.HasEntryAbility(c.Body!.Pokemon.AbilityId)))
  f=(entry=AcceptedTurn(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision))).State;
 var actions=AcceptedTurn(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)));
 return entry is null?actions:actions with{Events=entry.Events.AddRange(actions.Events),Trace=entry.Trace.AddRange(actions.Trace)};
}
SourceFourActionResult Next(SourceFourActionResult r)=>AcceptedTurn(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
(SourceBattleFrame State,SourceBattleEvent[] Events,RandomTrace[] Trace) Full(SourceBattleFrame f){var r=Begin(f);var events=new List<SourceBattleEvent>(r.Events);var trace=new List<RandomTrace>(r.Trace);for(int i=0;i<8&&r.State.FourTurn is not null;i++){r=Next(r);events.AddRange(r.Events);trace.AddRange(r.Trace);}Check(r.CompletedCleanTurn,"real four-action turn reached Ready");turns++;return(r.State,events.ToArray(),trace.ToArray());}
Test("1728 original C ordered cancellation vectors on actual slot 2",()=>{
 using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(args[1],"source-cancellers.json")));
 foreach(var row in doc.RootElement.GetProperty("rows").EnumerateArray()){
  int[] x=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();string species=x[2]==48?"DODUO":x[2]==54?"SLAKOTH":"RATTATA";
  var actor=Mon(species,pid:species=="DODUO"?1u:0u) with{Status=(uint)x[0],ConfusionTurnsRemaining=x[1],TruantLoafing=x[3]!=0,Flinched=x[5]!=0};
  var f=Recharge(Body(Frame(),2,actor),2,x[4]) with{Rng=new((uint)x[6])};var r=Accepted(Act(f));var after=r.State.Slots[2].Body!;
  Check(after.Status==(uint)x[7]&&after.ConfusionTurnsRemaining==x[8],"source counters "+Json(x));
  Check((r.State.Slots[2].Registers.Turn?.Lock?.TurnsRemaining??0)==x[9]&&after.Flinched==(x[10]!=0),"source recharge/flinch "+Json(x));
  int branch=r.Events.OfType<SourceActionBlockedEvent>().FirstOrDefault()?.Status switch{>=1 and <=7=>2,32=>3,64=>10,_=>0};
  if(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="truant"))branch=5;
  if(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="must-recharge"))branch=6;
  if(r.Events.OfType<SourceFlinchEvent>().Any(e=>!e.Applied))branch=7;
  if(r.Events.OfType<SourceConfusionSelfHitEvent>().Any())branch=8;
  Check(branch==x[11],"source winning canceller "+Json(x));
  int prefix=r.Trace.TakeWhile(t=>!t.Purpose.EndsWith(".accuracy")&&!t.Purpose.EndsWith(".critical")&&!t.Purpose.EndsWith(".damage")).Count();
  Check(prefix==x[12],"source canceller RNG draw count "+Json(x));
  if(branch!=0)Check(r.Trace.Length==x[12]&&!r.Events.OfType<SourcePpCommandEvent>().Any()&&after.Pokemon.Moves[0].CurrentPp==actor.Pokemon.Moves[0].CurrentPp,"blocked PP and exact RNG");
  oracle++;
 }
 Check(oracle==1728,"nonempty exhaustive source matrix");
});
Test("same-turn sleep, confusion, paralysis and flinch affect slower actual occupant",()=>{
 foreach(string move in new[]{"SLEEP_POWDER","SWAGGER","THUNDER_SHOCK","BITE"}){
  bool found=false;
  for(uint seed=0;seed<150&&!found;seed++){
   var species=move switch{"SLEEP_POWDER"=>"VENOMOTH","SWAGGER"=>"MIGHTYENA","THUNDER_SHOCK"=>"PIKACHU",_=>"GOLBAT"};
   var m=new[]{Mon(species,move,50),Mon("SLOWPOKE","AMNESIA",100),Mon("DROWZEE","MEDITATE",100),Mon("SLOWPOKE","AMNESIA",100)};
   m[0]=m[0] with{SpeedStage=12};m[1]=m[1] with{SpeedStage=0};
   var r=Full(Frame(m) with{Rng=new(seed)});
   found=move switch{"SLEEP_POWDER"=>r.Events.OfType<SourceActionBlockedEvent>().Any(e=>e.Side.Value==1&&WildBattleStatus.IsSleeping(e.Status)),"SWAGGER"=>r.Events.OfType<SourceConfusionChangedEvent>().Any(e=>e.Side.Value==1&&e.Reason=="countdown"),"THUNDER_SHOCK"=>r.Events.OfType<SourceActionBlockedEvent>().Any(e=>e.Side.Value==1&&e.Status==WildBattleStatus.Paralysis),_=>r.Events.OfType<SourceFlinchEvent>().Any(e=>e.Side.Value==1&&!e.Applied)};
   if(found)Check(!r.State.Slots[1].Body!.Flinched,"shared cleanup clears flinch");
  }
  Check(found,move+" actual faster-to-slower acquisition");
 }
});
Test("wake thaw Truant and blocked recharge expire through shared end-turn",()=>{
 foreach(uint status in new uint[]{1,2,5,WildBattleStatus.Freeze,WildBattleStatus.Paralysis}){
  var actor=Mon("SLAKOTH","TACKLE",30) with{Status=status,TruantLoafing=true,ConfusionTurnsRemaining=3,Flinched=true};
  var f=Recharge(Body(Frame([Mon("DROWZEE","MEDITATE",100),Mon("SLOWPOKE","AMNESIA",100),actor,Mon("SLOWPOKE","AMNESIA",100)]),2,actor),2,1);
  var r=Full(f);Check(!r.State.Slots[2].Body!.TruantLoafing,"M05 toggles despite earlier cancellation");Check(r.State.Slots[2].Registers.Turn is null,"recharge timer expires when an earlier canceller wins");Check(r.State.Slots[2].Body!.ConfusionTurnsRemaining==3,"earlier blocker preserves confusion counter");
 }
 var bird=Mon("DODUO","TACKLE",30,1) with{Status=2};var a=Accepted(Act(Body(Frame(),2,bird)));Check(a.State.Slots[2].Body!.Status==0&&a.Events.OfType<SourcePpCommandEvent>().Any(),"Early Bird wakes and attacks same attempt");
});
Test("self KO keeps real identity and queues only that departing occupant",()=>{
 var mon=Mon("SLAKOTH","TACKLE",30) with{ConfusionTurnsRemaining=3};mon=mon with{Pokemon=mon.Pokemon with{CurrentHp=1},SpeedStage=12};
 var f=Body(Frame([Mon("DROWZEE","MEDITATE",100),Mon("SLOWPOKE","AMNESIA",100),mon,Mon("SLOWPOKE","AMNESIA",100)]),2,mon);
 bool found=false;for(uint seed=0;seed<20&&!found;seed++){var r=Begin(f with{Rng=new(seed)});while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions){r=Next(r);if(r.Events.OfType<SourceConfusionSelfHitEvent>().Any()) {found=true;Check(r.State.Slots[2].Body!.Pokemon.CurrentHp==0&&r.State.Party(BattleSide.Player).Members[1].Pokemon.CurrentHp==0,"exact active and party identity");Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==f.Slots[0].Body!.Pokemon.CurrentHp,"equal-side ally unchanged");Check(r.State.FourTurn!.Defeats.Single().Occupant==f.Slots[2].Occupant,"defeat records old occupant");Check(r.State.Queue.Single(a=>a.Slot.Value==2).Kind==SourceActionKind.Cancelled,"self KO own queue cancelled");break;}}}Check(found,"self KO seed found");
});
Test("all six real stats preserved; stale and exhaustion faults roll back cancellation",()=>{
 var body=Mon() with{Status=3,AttackStage=9,DefenseStage=4,SpeedStage=8,SpecialAttackStageDelta=2,SpecialDefenseStageDelta=-2};var f=Body(Frame(),2,body);var r=Accepted(Act(f));Check(r.State.Slots[2].Body!.Pokemon.Stats==body.Pokemon.Stats&&r.State.Slots[2].Body!.AttackStage==9&&r.State.Slots[2].Body!.SpecialAttackStageDelta==2,"authoritative stats survive sleep");
 var stale=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value with{EntrySerial=2},f.Revision,0,new(3)));Check(!stale.Succeeded&&ReferenceEquals(stale.State,f)&&stale.Events.IsEmpty&&stale.Trace.IsEmpty,"stale exact rollback");
 foreach(var bad in new[]{f with{Revision=long.MaxValue},f with{EventSequence=long.MaxValue},Body(f,2,body with{Status=WildBattleStatus.Freeze}) with{Rng=new(1,ulong.MaxValue)}}){var a=Act(bad);Check(!a.Succeeded&&ReferenceEquals(a.State,bad)&&a.Events.IsEmpty&&a.Trace.IsEmpty,"exhaustion rollback "+a.Fault);}
 var begun=Begin(Frame());var token=begun.Continuation!.Value;var next=Next(begun);var rejected=SourceFourActionTurns.Advance(catalog,next.State,token);Check(!rejected.Succeeded&&ReferenceEquals(rejected.State,next.State)&&rejected.Events.IsEmpty&&rejected.Trace.IsEmpty,"stale retained continuation");
});
Test("canonical singles share exact state events and RNG for cancellation and wake",()=>{
 foreach(string species in new[]{"RATTATA","DODUO","SLAKOTH"})foreach(uint status in new uint[]{0,1,2,5,WildBattleStatus.Freeze,WildBattleStatus.Paralysis})foreach(int confusion in new[]{0,1,3})for(uint seed=0;seed<8;seed++){
  var actor=Mon(species,pid:species=="DODUO"?1u:0u) with{Status=status,ConfusionTurnsRemaining=confusion,TruantLoafing=species=="SLAKOTH"};var target=Mon(level:100);
  var state=new WildBattleState(actor,target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,Terrain=WildBattleTerrain.Plain,CalledParty=new([Member(actor)],[Member(target)],0,0,WildBattleCalledBattleKind.Wild)};
  var expected=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));Check(expected.Succeeded,"singles expected "+expected.Fault);
  var imported=SinglesBattleAdapter.Import(catalog,state);Check(imported.Succeeded,"singles imported "+imported.Fault);
  var actual=Accepted(SourceBattleKernel.ResolveAction(catalog,imported.Frame!,new(imported.Frame!.Slots[0].Occupant!.Value,imported.Frame.Revision,0,new(1))));var exported=SinglesBattleAdapter.Export(actual.State);Check(exported.Succeeded,"exported");
  Check(Json(expected.Battle.Trace)==Json(actual.Trace),"single RNG "+species+" "+status+" "+confusion+" "+seed);
  Check(Json(expected.Battle.Events)==Json(actual.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray()),"single events "+species+" "+status+" "+confusion+" "+seed);
  Check(Json(expected.State)==Json(exported.State),"single state "+species+" "+status+" "+confusion+" "+seed+"\n"+Json(expected.State)+"\n"+Json(exported.State));parity++;
 }
});
Test("bounded gates keep unported locks and immunity cleanup closed",()=>{
 var f=Frame();
 var sleepImmune=Mon("DROWZEE") with{Status=3};
 var ownTempo=Mon("SLOWPOKE",pid:1) with{ConfusionTurnsRemaining=3};
 foreach(var bad in new[]{Body(f,2,Mon() with{ConfusionTurnsRemaining=6}),Body(f,2,sleepImmune),Body(f,2,ownTempo),Recharge(f,2,3),Body(f,2,Mon() with{DisabledMove=Id("TACKLE"),DisableTurnsRemaining=2})}){
  var r=Act(bad);Check(!r.Succeeded&&ReferenceEquals(r.State,bad)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"unsupported bounded state atomic "+r.Fault);
 }
 f=Body(Body(f,2,Mon("CHARMANDER","EMBER")),3,Mon() with{Status=WildBattleStatus.Freeze});var fire=Act(f);Check(fire.Fault=="source_kernel_fire_thaw_move_end_not_migrated"&&ReferenceEquals(fire.State,f),"fire move-end thaw remains explicit");
});
Test("no PP still runs earlier cancellers and recharge clearing matches singles",()=>{
 foreach(bool recharge in new[]{false,true})foreach(bool flinched in new[]{false,true})foreach(uint status in new uint[]{0,1,3,WildBattleStatus.Freeze,WildBattleStatus.Paralysis})for(uint seed=0;seed<5;seed++){
  var actor=Mon() with{Status=status,Flinched=flinched,ConfusionTurnsRemaining=2};actor=actor with{Pokemon=actor.Pokemon with{Moves=actor.Pokemon.Moves.SetItem(0,actor.Pokemon.Moves[0] with{CurrentPp=0})}};
  if(recharge)actor=actor with{CalledTurnState=new(){Lock=new(WildBattleCalledLockKind.Recharge,Id("HYPER_BEAM"),0,Id("TACKLE"),WildBattleSide.Opponent,1)}};
  var target=Mon(level:100);var state=new WildBattleState(actor,target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,Terrain=WildBattleTerrain.Plain,CalledParty=new([Member(actor)],[Member(target)],0,0,WildBattleCalledBattleKind.Wild)};
  var expected=recharge?WildBattleRules.ResolveCalledMoveContinuation(catalog,state,WildBattleSide.Player):WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));Check(expected.Succeeded,"no PP singles "+expected.Fault);
  var imported=SinglesBattleAdapter.Import(catalog,state);Check(imported.Succeeded,"import recharge/no PP "+imported.Fault);
  var actual=Accepted(SourceBattleKernel.ResolveAction(catalog,imported.Frame!,new(imported.Frame!.Slots[0].Occupant!.Value,imported.Frame.Revision,0,new(1))));var exported=SinglesBattleAdapter.Export(actual.State);
  Check(Json(actual.Trace)==Json(expected.Battle.Trace)&&Json(actual.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray())==Json(expected.Battle.Events)&&Json(exported.State)==Json(expected.State),"exact recharge/flinch/no PP cancellation parity");parity++;
 }
});
Test("flinch needs retained actor order and cannot retroactively cancel an earlier actor",()=>{
 bool applied=false;for(uint seed=0;seed<80&&!applied;seed++){
  var slow=Mon("GOLBAT","BITE",30) with{SpeedStage=0};var fast=Mon("SLOWPOKE","AMNESIA",100) with{SpeedStage=12};
  var r=Full(Frame([slow,fast,Mon("DROWZEE","MEDITATE",100),Mon("SLOWPOKE","AMNESIA",100)]) with{Rng=new(seed)});
  Check(!r.Events.OfType<SourceFlinchEvent>().Any(e=>e.Side.Value==1),"already-acted target never flinches");
  applied=r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Actor.Value==0);
 }Check(applied,"ordinary damaging late action verified");
 var f=Body(Frame(),2,Mon("GOLBAT","BITE",30));var standalone=Act(f);Check(standalone.Fault=="source_kernel_flinch_order_not_migrated"&&ReferenceEquals(standalone.State,f),"standalone unordered flinch recipe rejected");
});
Test("M06 original selected move does not retarget a forced recharge action",()=>{
 var actor=Mon("RATTATA","TACKLE",30);var f=Recharge(Frame([Mon("DROWZEE","MEDITATE",100),Mon("SLOWPOKE","AMNESIA",100),actor,Mon("SLOWPOKE","AMNESIA",100)]),2,1);
 var r=Full(f);
 Check(r.Events.OfType<SourceMoveFailedEvent>().Count(e=>e.Actor.Value==2&&e.Reason=="must-recharge")==1,"forced recharge blocks exactly once");
 Check(!r.Trace.Any(t=>t.Purpose.Contains(".target.")),"forced source move branch precedes changed selected slot branch; no GetMoveTarget draw");
 Check(r.State.Party(BattleSide.Player).Members[1].Pokemon.Moves[0].CurrentPp==actor.Pokemon.Moves[0].CurrentPp,"recharge retains actual selected PP");
});
Console.WriteLine(JsonSerializer.Serialize(new{suite="shared-action-cancellers",groups,checks,failures,oracle,turns,parity,runtime=Environment.Version.ToString(),results}));
return failures==0&&groups>=6&&oracle==1728&&parity>0?0:1;
