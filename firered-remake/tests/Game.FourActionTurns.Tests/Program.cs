using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if(args.Length!=2) return 2;
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog=proof.Catalog;
int checks=0,groups=0,failures=0,oracle=0,cleanTurns=0; var summary=new List<object>();
void Check(bool ok,string label){checks++;if(!ok)throw new InvalidDataException(label);}
void Test(string label,Action action){groups++;try{action();summary.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception ex){failures++;summary.Add(new{label,passed=false,error=ex.ToString()});Console.WriteLine("FAIL "+label+" "+ex);}}
string Json<T>(T value)=>JsonSerializer.Serialize(value);
MoveId Id(string name)=>new("pret:MOVE_"+name);
WildBattleCombatant Mon(string species,int level,string move,uint personality=0){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(personality)),new(0)).Pokemon;
 return new(p with{Moves=[new(Id(move),catalog.Moves[Id(move)].Pp,catalog.Moves[Id(move)].Pp)]});
}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied=null,int reserves=0){
 var m=supplied??[Mon("PIDGEY",50,"TACKLE"),Mon("RATTATA",50,"QUICK_ATTACK"),Mon("SPEAROW",50,"PECK"),Mon("DODUO",50,"PECK")];
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),Parties=[new(BattleSide.Player,new[]{Member(m[0]),Member(m[2])}.Concat(Enumerable.Range(0,reserves).Select(i=>Member(Mon("PIDGEY",50,"TACKLE",(uint)i)))).ToImmutableArray()),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};
}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(targets?[c.Slot.Value]??(c.Slot.Value^1)))).ToImmutableArray();
SourceFourActionResult Success(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Begin(SourceBattleFrame f,int[]? targets=null)=>Success(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,targets)));
SourceFourActionResult Next(SourceFourActionResult r)=>Success(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
void Rejected(SourceBattleFrame f,SourceFourActionResult r,string label){Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,label+" atomic fault: "+r.Fault);}
SourceBattleFrame RebindBody(SourceBattleFrame f,int slot,WildBattleCombatant body){var c=f.Slots[slot];var key=c.Member!.Value;var party=f.Parties[(int)key.Side];return f with{Slots=f.Slots.SetItem(slot,c with{Body=body}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(body))})};}
Test("actual runtime and authenticated 386 species 354 move catalog",()=>{Check(Environment.Version.Major==10,"actual .NET10");Check(catalog.Species.Count==386&&catalog.Moves.Count==354,"authenticated source catalog");});
Test("four real actors complete clean turn with costs targets shared effects and permanent synchronization",()=>{
 var f=Frame();var r=Begin(f);var ev=r.Events.ToBuilder();var tr=r.Trace.ToBuilder();
 for(int i=0;i<4;i++){r=Next(r);ev.AddRange(r.Events);tr.AddRange(r.Trace);}
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.EndTurn,"four actions boundary");
 Check(ev.OfType<SourceMoveUsedEvent>().Select(e=>e.Actor.Value).Distinct().Count()==4,"four distinct real action identities");
 Check(ev.OfType<SourceHpChangedEvent>().Select(e=>e.Target.Value).Distinct().Count()==4,"all four targets receive real damage");
 foreach(var c in r.State.Slots){Check(c.Body!.Pokemon.Moves[0].CurrentPp==f.Slots[c.Slot.Value].Body!.Pokemon.Moves[0].CurrentPp-1,"exactly one PP");Check(c.Body.Pokemon.CurrentHp==r.State.Party(c.Slot.Side).Members[c.Member!.Value.Index].Pokemon.CurrentHp,"actual party HP sync");}
 r=Next(r);Check(r.CompletedCleanTurn&&r.State.Turn==1&&r.State.Queue.IsEmpty&&r.State.FourTurn is null,"genuine clean Ready");
 Check(r.Events.OfType<SourceTurnReadyEvent>().Count()==1&&r.State.Slots.All(c=>c.Registers.Damage is null&&!c.Body!.DamagedByOpponentThisTurn),"shared empty endturn cleanup");cleanTurns++;
});
Test("both sides can intentionally hit their own ally",()=>{
 var r=Begin(Frame(),[2,3,0,1]);var used=new List<SourceMoveUsedEvent>();for(int i=0;i<4;i++){r=Next(r);used.AddRange(r.Events.OfType<SourceMoveUsedEvent>());}Check(used.Count==4&&used.All(e=>e.Actor.Side==e.Target.Side&&e.Actor!=e.Target),"four ally-target actions");r=Next(r);Check(r.CompletedCleanTurn,"ally turn completes");cleanTurns++;
});
Test("every action and endturn pause survives JSON with exact state events and RNG; stale continuation rejected",()=>{
 var r=Begin(Frame());for(int i=0;i<5;i++){
  var token=r.Continuation!.Value;var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;
  var direct=SourceFourActionTurns.Advance(catalog,r.State,token);var saved=SourceFourActionTurns.Advance(catalog,copy,token);Check(Json(direct)==Json(saved),"exact serialized resume "+i);r=Success(direct);
  Rejected(r.State,SourceFourActionTurns.Advance(catalog,r.State,token),"replayed continuation "+i);
 }Check(r.CompletedCleanTurn,"resumed full turn");cleanTurns++;
});
Test("unported phases and marker bypasses fail before selection costs ordering and RNG",()=>{
 var f=Frame();foreach(var bad in new[]{f with{SourceMechanicsRulesVersion=null},f with{SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion},RebindBody(f,0,Mon("PIDGEY",50,"EARTHQUAKE")),RebindBody(f,0,Mon("VENONAT",50,"DISABLE"))})Rejected(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"unsupported capability");
 Rejected(f,SourceFourActionTurns.BeginActions(catalog with{},f,f.Revision,Choices(f)),"catalog clone carries no authority");
 Rejected(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f).RemoveAt(2)),"missing actor");
});
Test("original occupant action is cancelled after real structural departure",()=>{
 var f=Frame(reserves:1);var r=Begin(f);var outgoing=r.State.Slots[0].Occupant!.Value;
 var reserve=new SourceSwitchReservation(outgoing,new(BattleSide.Player,2),WildBattleCalledSwitchKind.Voluntary);
 var reserved=SourceBattleIdentityRules.Reserve(r.State,r.State.Revision,reserve);Check(reserved.Succeeded,"reserve");var body=new WildBattleCombatant(reserved.State.Party(BattleSide.Player).Members[2].Pokemon);
 var bound=SourceBattleIdentityRules.BindReservedEntry(reserved.State,reserved.State.Revision,reserve,SourceEntryBoundary.Action,body,new());Check(bound.Succeeded,"structural entry only; not switch recipe acceptance");
 r=new(bound.State,[],[],null);var events=new List<SourceBattleEvent>();while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions){r=Next(r);events.AddRange(r.Events);}
 Check(!events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==0),"incoming never inherits outgoing selection");Check(r.State.Slots[0].EntrySerial==2,"new occupant token");
});
Test("real early KO leaves vacancy; next selected enemy and ally targets use original fallback",()=>{
 var f=Frame([Mon("PIDGEY",1,"TACKLE"),Mon("RATTATA",100,"QUICK_ATTACK"),Mon("SPEAROW",100,"PECK"),Mon("DODUO",100,"PECK")],1);
 var r=Begin(f,[1,0,0,0]);r=Next(r);Check(r.Events.OfType<SourceFaintedEvent>().Single().Side.Value==0,"real first KO");
 Check(r.State.IsDeferredKoVacant(new(0))&&!r.State.IsSourceAbsent(new(0))&&r.State.PendingSwitches.IsEmpty,"separate deferred vacancy no early replacement");
 Check(r.State.Queue.Single(a=>a.Slot.Value==0).Kind==SourceActionKind.Cancelled,"original actor canceled");
 Check(SourceFourActionTurns.ResolveSelectedTarget(r.State,new(3),new(0),catalog.Moves[Id("PECK")])==new BattlerSlot(2),"enemy vacant target to living partner");
 Check(SourceFourActionTurns.ResolveSelectedTarget(r.State,new(2),new(0),catalog.Moves[Id("PECK")])==new BattlerSlot(3),"ally vacant target to actor opposite enemy");
 var later=new List<SourceBattleEvent>();while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions){r=Next(r);later.AddRange(r.Events);}
 Check(later.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==2),"living teammate still acts");Check(later.OfType<SourceMoveUsedEvent>().Where(e=>e.Actor.Value==3).All(e=>e.Target.Value==2),"source fallback actually used by kernel");
 r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending&&r.State.PendingSwitches.Single().Slot.Value==0,"replacement exposed only after endturn");
 Check(r.State.Turn==0&&!r.CompletedCleanTurn,"KO turn not silently Ready");
 Rejected(r.State,SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value),"explicit replacement choice required");
});
Test("two genuine same-turn KOs preserve reserves and expose only available choices",()=>{
 foreach(int reserveCount in new[]{1,2}){
  var f=Frame([Mon("PIDGEY",1,"TACKLE"),Mon("RATTATA",100,"QUICK_ATTACK"),Mon("SPEAROW",1,"PECK"),Mon("DODUO",100,"PECK")],reserveCount);
  var r=Begin(f,[1,0,3,2]);int faint=0;while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions){r=Next(r);faint+=r.Events.OfType<SourceFaintedEvent>().Count();}
  Check(faint==2&&r.State.DeferredKoVacancyMask==5&&r.State.Outcome==WildBattleOutcome.Ongoing,"two actual KOs with living reserves");
  Check(SourceFourActionTurns.ResolveSelectedTarget(r.State,new(1),new(0),catalog.Moves[Id("TACKLE")]) is null,"both player targets vacant no invented target");
  r=Next(r);Check(r.State.PendingSwitches.Length==reserveCount,"no more choices than actual reserves");Check(r.State.PendingSwitches.Select(p=>p.Slot.Value).SequenceEqual(new[]{0,2}.Take(reserveCount)),"numeric source replacement slot priority");
  Check(r.State.FourTurn!.Defeats.Select(d=>d.Occupant).Distinct().Count()==2,"distinct defeated party identities");
  var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;Check(Json(restored)==Json(r.State)&&SourceBattleIdentityRules.Validate(restored) is null,"KO queue JSON continuation");
 }
});
Test("whole-team exhaustion ends battle while opponent KO preserves EXP boundary",()=>{
 var r=Begin(Frame([Mon("PIDGEY",1,"TACKLE"),Mon("RATTATA",100,"QUICK_ATTACK"),Mon("SPEAROW",1,"PECK"),Mon("DODUO",100,"PECK")]),[1,0,3,2]);
 while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.Outcome==WildBattleOutcome.Lost&&r.State.FourTurn!.Phase==SourceFourActionPhase.BattleEnded,"whole player party lost");
 var f=Frame([Mon("RATTATA",100,"QUICK_ATTACK"),Mon("PIDGEY",1,"TACKLE"),Mon("SPEAROW",50,"PECK"),Mon("DODUO",50,"PECK")]);r=Begin(f);r=Next(r);
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&r.State.FourTurn.Defeats.Single().RequiresExperience,"real enemy KO stops at exact reward boundary");
 Rejected(r.State,SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value),"unported EXP cannot be silently ignored");
});
Test("last enemy defeat cannot publish final outcome before pending experience",()=>{
 var dead=Mon("DODUO",50,"PECK");dead=dead with{Pokemon=dead.Pokemon with{CurrentHp=0}};
 var f=Frame([Mon("RATTATA",100,"QUICK_ATTACK"),Mon("PIDGEY",1,"TACKLE"),Mon("SPEAROW",50,"PECK"),dead]);f=f with{SourceAbsentMask=8,TurnStartAbsentMask=8};
 var r=Begin(f);r=Next(r);Check(r.State.Outcome==WildBattleOutcome.Ongoing&&r.State.FourTurn!.OutcomeAfterRewards==WildBattleOutcome.Won,"outcome retained behind EXP boundary");
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&!r.Events.OfType<SourceEndedEvent>().Any(),"no premature BattleEnded or Ready");
});
Test("no eligible target after two real KOs uses original failure PP commands",()=>{
 var actor=Mon("RATTATA",100,"QUICK_ATTACK");actor=actor with{SpeedStage=7,Pokemon=actor.Pokemon with{CurrentHp=1}};
 var f=Frame([actor,Mon("RATTATA",100,"QUICK_ATTACK"),Mon("PIDGEY",1,"TACKLE"),Mon("DODUO",100,"PECK")],1);
 var r=Begin(f,[2,0,1,0]);r=Next(r);Check(r.Events.OfType<SourceFaintedEvent>().Single().Side.Value==2,"actual ally KO");
 r=Next(r);Check(r.Events.OfType<SourceFaintedEvent>().Single().Side.Value==0,"actual second KO");
 Check(r.State.Outcome==WildBattleOutcome.Ongoing&&r.State.Queue[r.State.NextAction].Slot.Value==3,"later opponent has no target but reserves exist");
 var noTarget=Success(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
 Check(noTarget.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="deferred-ko-no-target"&&noTarget.Trace.IsEmpty,"failure without hit RNG");
 Check(noTarget.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==1&&noTarget.State.Slots[3].Body!.Pokemon.Moves[0].CurrentPp==r.State.Slots[3].Body!.Pokemon.Moves[0].CurrentPp-1,"shared original PP command actually applied");
});
Test("2048 independently compiled original C ordering vectors",()=>{
 foreach(var line in File.ReadLines(Path.Combine(args[1],"source-order-vectors.csv"))){
  var x=line.Split(',').Select(ulong.Parse).ToArray();int mode=(int)x[0];var f=Frame(Enumerable.Range(0,4).Select(_=>Mon("PIDGEY",50,"TACKLE")).ToArray());
  for(int i=0;i<4;i++){
   string move=(mode==2||mode==7)?i==0?"QUICK_ATTACK":i==2?"VITAL_THROW":"TACKLE":mode==3?"QUICK_ATTACK":"TACKLE";
   var body=Mon("PIDGEY",50,move);body=body with{Pokemon=body.Pokemon with{Stats=body.Pokemon.Stats with{Speed=mode==1?new[]{50,80,100,70}[i]:50}}};
   if(mode==4&&i<2)body=body with{HeldItem=i==0?"ITEM_MACHO_BRACE":"ITEM_QUICK_CLAW"};
   if(mode==5){body=body with{SpeedBadgeBoost=(i&1)==0,Status=i==2?WildBattleStatus.Paralysis:0,SpeedStage=i==3?8:6};if(i==1)body=body with{Pokemon=body.Pokemon with{AbilityId="ABILITY_SWIFT_SWIM"}};}
   f=RebindBody(f,i,body);
  }
  f=f with{Rng=new((uint)x[1]),RandomTurnNumber=100,Field=new(){RainTurnsRemaining=mode==5?1:0}};
  var selected=SourceBattleIdentityRules.CommitSelections(f,f.Revision,Choices(f));Check(selected.Succeeded,"oracle committed fixture");f=selected.State;
  if(mode==6)f=f with{Queue=f.Queue.Select(a=>a.Slot.Value==1?a with{Kind=SourceActionKind.Item,MoveSlot=null,Target=null,Item=new ItemId("pret:ITEM_POTION")}:a).ToImmutableArray()};
  // Switch precedence oracle uses a valid actual side-wide reservation, never an invalid queue.
  if(mode==6){var side=f.Party(BattleSide.Player);var reserve=Member(Mon("PIDGEY",50,"TACKLE"));f=f with{Parties=f.Parties.SetItem(0,side with{Members=side.Members.Add(reserve)}),Reservations=[new(f.Slots[2].Occupant!.Value,new(BattleSide.Player,2),WildBattleCalledSwitchKind.Voluntary)],Queue=f.Queue.Select(a=>a.Slot.Value==2?a with{Kind=SourceActionKind.Switch,MoveSlot=null,Target=null,Incoming=new(BattleSide.Player,2)}:a).ToImmutableArray()};}
  var actual=SourceBattleTurnOrder.Resolve(catalog,f,mode==7?WildBattleOrderPhase.EndTurn:WildBattleOrderPhase.Actions);Check(actual.Fault is null,"oracle order "+actual.Fault);
  Check(actual.Order.Select(s=>(ulong)s.Value).SequenceEqual(x.Skip(2).Take(4))&&actual.Rng.Seed==x[6]&&actual.Rng.Cursor==x[7],"original C exact order/seed/draw-count mode="+mode+" seed="+x[1]);oracle++;
 }Check(oracle==2048,"nonempty complete oracle vectors");
});
Test("source CHECK_ON_FIELD includes source-absent Cloud Nine with remaining HP",()=>{
 var f=Frame(Enumerable.Range(0,4).Select(_=>Mon("PIDGEY",50,"TACKLE")).ToArray());
 for(int i=0;i<4;i++){
  var body=f.Slots[i].Body!;body=body with{Pokemon=body.Pokemon with{Stats=body.Pokemon.Stats with{Speed=new[]{50,80,60,30}[i]},AbilityId=i==0?"ABILITY_SWIFT_SWIM":i==3?"ABILITY_CLOUD_NINE":body.Pokemon.AbilityId}};
  f=RebindBody(f,i,body);
 }
 f=f with{Rng=new(31),SourceAbsentMask=8,TurnStartAbsentMask=8,Field=new(){RainTurnsRemaining=1}};
 var selected=SourceBattleIdentityRules.CommitSelections(f,f.Revision,Choices(f));Check(selected.Succeeded,"valid absent retained body ordering fixture");
 var actual=SourceBattleTurnOrder.Resolve(catalog,selected.State,WildBattleOrderPhase.Actions);
 var source=File.ReadAllText(Path.Combine(args[1],"source-absent-weather-vector.csv")).Trim().Split(',').Select(ulong.Parse).ToArray();
 Check(actual.Fault is null,"ordering accepted "+actual.Fault);
 Check(actual.Order.Select(s=>(ulong)s.Value).SequenceEqual(source.Take(4))&&actual.Rng.Seed==source[4]&&actual.Rng.Cursor==source[5],"source-absent living Cloud Nine suppresses Swift Swim; expected "+string.Join(',',source.Take(4))+" actual "+string.Join(',',actual.Order.Select(s=>s.Value)));
});
Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"result.json"),Json(new{passed=failures==0,checks,groups,failures,oracle,cleanTurns,runtime=Environment.Version.ToString(),results=summary,limitations=new[]{"No encounter/AI/campaign admission","Reward continuation requires explicit provenance; tested by Game.FourRewards.Tests","No simultaneous multi-target or recoil double-KO claim","Deferred-KO no-target boundary reuses source failure/PP commands","Only clean field/battler/delayed endturn; unported states preflight rejected"}}));
Console.WriteLine($"{checks} checks; {groups} groups; {failures} failures; {oracle} source vectors; {cleanTurns} complete clean turns");return failures==0?0:1;
