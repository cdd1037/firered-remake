using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
if(args.Length<2)throw new ArgumentException("CONTENT_ROOT OUTPUT [--capture]");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
string Json<T>(T x)=>JsonSerializer.Serialize(x);
MoveId Id(string s)=>new("pret:MOVE_"+s);
string[] family=["HYPER_BEAM","FRENZY_PLANT","BLAST_BURN","HYDRO_CANNON","THRASH","OUTRAGE","PETAL_DANCE"];
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
var snapshots=new List<object>();
foreach(string move in family)foreach(uint seed in new uint[]{0,31,127}){
 var actor=Mon("CHARIZARD",40,move,"DEFENSE_CURL");var target=Mon("SNORLAX",100,"DEFENSE_CURL");
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(seed));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 var turns=new List<object>();var state=start.State!;
 for(int turn=0;turn<5;turn++){
  int slot=turn==0||state.Player.CalledTurnState?.Lock is not null?0:1;
  var result=WildBattleRules.ResolveMove(catalog,state,slot);if(!result.Succeeded)throw new Exception("turn:"+move+":"+result.Fault);
  turns.Add(result);state=result.State!;
 }
 var data=Json(turns);snapshots.Add(new{move,seed,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant(),bytes=Encoding.UTF8.GetByteCount(data)});
}
if(args.Contains("--capture")){
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasRechargeRampageRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unmodified baseline");
 File.WriteAllText(args[1],Json(new{baseline="88543cb9ee09fd1a56a5567bc1aaa7bc0aeea900",snapshots})+"\n");return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
Test("frozen complete singles state events RNG through five full turns",()=>{
 Check(Environment.Version.Major==10,"hosted runtime10");
 using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline.json")));
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==21&&old.Length==snapshots.Count,"nonempty suite");
 for(int i=0;i<old.Length;i++)Check(JsonSerializer.SerializeToElement(snapshots[i]).GetProperty("sha256").GetString()==old[i].GetProperty("sha256").GetString(),"full state/event/RNG parity "+Json(snapshots[i]));
});
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied = null, uint seed = 31) {
    var m = supplied ?? [Mon("PIDGEY", 30, "MINIMIZE"), Mon("SNORLAX", 100, "DEFENSE_CURL"), Mon("SPEAROW", 20, "DEFENSE_CURL"), Mon("RATTATA", 20, "MINIMIZE")];
    return new() { Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn, SourceMechanicsRulesVersion = SourceBattleKernel.Version,
        BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(seed),
        Parties = [new(BattleSide.Player, [Member(m[0]), Member(m[2])]), new(BattleSide.Opponent, [Member(m[1]), Member(m[3])])],
        Slots = Enumerable.Range(0,4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, m[i])).ToImmutableArray() };
}
SourceBattleFrame Put(SourceBattleFrame f, int i, WildBattleCombatant m) { var c = f.Slots[i]; var key = c.Member!.Value; var p = f.Party(key.Side); return f with { Slots = f.Slots.SetItem(i,c with { Body=m }), Parties=f.Parties.SetItem((int)key.Side,p with { Members=p.Members.SetItem(key.Index,Member(m)) }) }; }
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f, int[]? positions = null, int[]? targets = null) => f.Slots.Where(c => f.CanAct(c.Slot)).Select(c => {
    int pos = positions?[c.Slot.Value] ?? c.Registers.Turn?.Lock?.PpSlot ?? 0;
    var move = catalog.Moves[c.Registers.Turn?.Lock?.Move ?? c.Body!.Pokemon.Moves[pos].Move];
    var target = c.Registers.Turn?.Lock?.Target ?? new BattlerSlot(targets?[c.Slot.Value] ?? (move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1));
    return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,target);
}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r) { Check(r.Succeeded,r.Fault ?? "accepted"); Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output"); return r; }
SourceFourActionResult Begin(SourceBattleFrame f,int[]? p=null,int[]? t=null) => Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,p,t)));
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Turn(SourceBattleFrame f,int[]? p=null,int[]? t=null) {
    var r=Begin(f,p,t); var events=r.Events.ToList(); var trace=r.Trace.ToList();
    while(r.State.FourTurn is not null) { Check(r.State.FourTurn.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume,"no unexpected external boundary");
        var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!; var actual=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value); var replay=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);
        Check(Json(actual)==Json(replay),"exact pause JSON state/events/RNG replay"); r=Ok(actual); events.AddRange(r.Events);trace.AddRange(r.Trace);
    } Check(r.CompletedCleanTurn,"clean full turn");  return(r.State,events,trace);
}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int pos=0) { var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,pos,new(target))); Check(r.Succeeded,r.Fault??"action"); return r; }
void Rejected(SourceBattleFrame f, SourceFourActionResult r,string why) => Check(!r.Succeeded && ReferenceEquals(f,r.State) && r.Trace.IsEmpty && r.Events.IsEmpty,why+" atomic: "+r.Fault);


SourceBattleFrame WithLock(SourceBattleFrame f,int slot,string name,int remaining,bool unable=false) {
 var cell=f.Slots[slot];var move=catalog.Moves[Id(name)];
 var locked=new SourceTurnLock(move.Effect=="EFFECT_RECHARGE"?WildBattleCalledLockKind.Recharge:WildBattleCalledLockKind.Rampage,Id(name),0,cell.Body!.Pokemon.Moves[0].Move,new(slot^1),remaining,0,null,unable);
 return f with{Slots=f.Slots.SetItem(slot,cell with{Registers=cell.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,locked,0,null)}})};
}
SourceBattleFrame Basis(string move,int actor=0,uint seed=31) {
 var mons=Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"DEFENSE_CURL","MINIMIZE")).ToArray();mons[actor]=Mon("CHARIZARD",40,move,"DEFENSE_CURL");return Frame(mons,seed);
}
Test("all seven recipes run on both sides and all four actual positions",()=>{
 foreach(var name in family)for(int actor=0;actor<4;actor++){
  var f=Basis(name,actor);var result=Act(f,actor,actor^1);
  var locked=result.State.Slots[actor].Registers.Turn?.Lock;
  Check(locked?.Move==Id(name)&&locked.PpSlot==0&&locked.Target.Value==(actor^1),name+" stored exact lock at "+actor);
  Check(result.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Actor.Value==actor&&e.Target.Value==(actor^1)),"actual slot hit "+name);
  Check(result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==f.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp-1,"once first PP");
  Check(result.State.Slots.Where(c=>c.Slot.Value!=actor&&c.Slot.Value!=(actor^1)).All(c=>Json(c)==Json(f.Slots[c.Slot.Value])),"other pair untouched");
  if(name is "THRASH" or "OUTRAGE" or "PETAL_DANCE"){
   Check(result.Trace.Select(t=>t.Purpose.Split('.').Last()).SequenceEqual(new[]{"accuracy","critical","damage","secondary-effect","rampage-duration"}),"duration after damage and secondary");
   var continuation=Act(result.State,actor,actor^1);
   Check(!continuation.Events.OfType<SourcePpCommandEvent>().Any()&&continuation.Trace.Length==4,"hit continuation omits PP and duration");
  }else{
   Check(result.Trace.Length==3&&locked!.TurnsRemaining==2,"CERTAIN recharge no chance draw");
   var recharge=Act(result.State,actor,actor^1);
   Check(recharge.State.Slots[actor].Registers.Turn is null&&recharge.Trace.IsEmpty&&!recharge.Events.OfType<SourcePpCommandEvent>().Any(),"recharge consumes action without PP/RNG");
  }
 }
});
Test("rampage zero-PP continuation miss PP clamp Protect and no-effect interruption",()=>{
 var f=Basis("THRASH");var locked=Act(f,0,1).State;
 var m=locked.Slots[0].Body!;locked=Put(locked,0,m with{Pokemon=m.Pokemon with{Moves=m.Pokemon.Moves.SetItem(0,m.Pokemon.Moves[0] with{CurrentPp=0})}});
 var hit=Act(locked,0,1);Check(!hit.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-pp")&&!hit.Events.OfType<SourcePpCommandEvent>().Any(),"locked final PP still hits");
 var missInput=Put(locked with{Rng=new(31)},0,locked.Slots[0].Body! with{AccuracyStage=0});
 var miss=Act(missInput,0,1);Check(miss.Events.OfType<SourceMoveMissedEvent>().Any()&&miss.Events.OfType<SourcePpCommandEvent>().Single().After==0&&miss.State.Slots[0].Registers.Turn?.Lock is not null,"accuracy miss spends clamped PP but retains rampage");
 var protectedInput=Put(locked,1,locked.Slots[1].Body! with{Protected=true});var protect=Act(protectedInput,0,1);
 Check(protect.State.Slots[0].Registers.Turn is null&&protect.Events.OfType<SourcePpCommandEvent>().Count()==1&&protect.Trace.IsEmpty,"Protect cancels before PP skip and accuracy");
 var immune=Act(Put(locked,1,Mon("GENGAR",100,"DEFENSE_CURL")),0,1);
 Check(immune.State.Slots[0].Registers.Turn?.Lock is{UnableToUseMove:true}&&!immune.Events.OfType<SourcePpCommandEvent>().Any(),"ghost flags retained lock for source residual");
 var ended=Turn(immune.State);Check(ended.State.Slots[0].Registers.Turn is null&&!ended.Events.OfType<SourceConfusionChangedEvent>().Any(e=>e.Side.Value==0),"no-effect ending has no fatigue confusion");
});
Test("sleep freeze paralysis flinch confusion infatuation disabled and recharge ordering",()=>{
 foreach(string kind in new[]{"sleep","freeze","paralysis","flinch","confusion","infatuation","disabled","truant"}){
  var input=WithLock(Basis("THRASH",seed:kind=="freeze"?1u:0u),0,"THRASH",2);var m=kind=="truant"?Mon("SLAKING",40,"THRASH","DEFENSE_CURL") with{TruantLoafing=true}:input.Slots[0].Body!;
  m=kind switch{"sleep"=>m with{Status=3},"freeze"=>m with{Status=WildBattleStatus.Freeze},"paralysis"=>m with{Status=WildBattleStatus.Paralysis},"flinch"=>m with{Flinched=true},"confusion"=>m with{ConfusionTurnsRemaining=3},"disabled"=>m with{DisabledMove=Id("THRASH"),DisableTurnsRemaining=2},_=>m};input=Put(input,0,m);
  if(kind=="infatuation"){var c=input.Slots[0];input=input with{Slots=input.Slots.SetItem(0,c with{Registers=c.Registers with{InfatuationSource=new(1)}})};}
  var r=Act(input,0,1);Check((r.State.Slots[0].Registers.Turn?.Lock is not null)==(kind is "sleep" or "freeze"),kind+" exact immediate cancellation");
  Check(!r.Events.OfType<SourcePpCommandEvent>().Any(),kind+" no PP command");
  if(kind=="sleep"){var turn=Turn(r.State);Check(turn.State.Slots[0].Registers.Turn is null&&!turn.Events.OfType<SourceConfusionChangedEvent>().Any(e=>e.Side.Value==0),"sleep clears before Thrash residual");}
 }
 var recharge=WithLock(Basis("HYPER_BEAM",seed:0),0,"HYPER_BEAM",1);var m0=recharge.Slots[0].Body!;
 var sleeping=Act(Put(recharge,0,m0 with{Status=3,Flinched=true}),0,1);Check(sleeping.State.Slots[0].Registers.Turn?.Lock is not null,"sleep precedes recharge");
 var ready=Act(Put(recharge,0,m0 with{Status=WildBattleStatus.Paralysis,Flinched=true}),0,1);Check(ready.Trace.IsEmpty&&ready.State.Slots[0].Body!.Flinched&&ready.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="must-recharge"),"recharge precedes flinch and paralysis");
 var timedOut=Turn(Put(recharge,0,m0 with{Status=3}));Check(timedOut.State.Slots[0].Registers.Turn is null,"recharge expires at ready even when sleep stopped its canceller");
});
Test("complete quartet ramps reselect random target each turn and resolve fatigue",()=>{
 var f=Frame(Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,i%2==0?"THRASH":"OUTRAGE","DEFENSE_CURL") with{DefenseStage=12,SpecialDefenseStageDelta=6}).ToArray());
 var first=Turn(f);Check(first.State.Slots.All(c=>c.Registers.Turn?.Lock?.Kind==WildBattleCalledLockKind.Rampage),"four independent rampages");
 Check(first.Trace.Count(t=>t.Purpose.Contains("target.learned.random-flank"))==4,"one action-time random flank per initial action");
 var second=Turn(first.State);Check(second.Trace.Count(t=>t.Purpose.Contains("target.learned.random-flank"))==4,"continuations draw random flank again, no called rejection loop");
 Check(!second.Events.OfType<SourcePpCommandEvent>().Any(),"four continued hits have no PP command");
 var state=second.State;var confusion=second.Events.OfType<SourceConfusionChangedEvent>().Count(e=>e.Reason=="move");
 if(state.Slots.Any(c=>c.Registers.Turn?.Lock is not null)){
  var positions=state.Slots.Select(c=>c.Registers.Turn?.Lock is null?1:0).ToArray();var third=Turn(state,positions);state=third.State;confusion+=third.Events.OfType<SourceConfusionChangedEvent>().Count(e=>e.Reason=="move");
 }
 Check(state.Slots.All(c=>c.Registers.Turn?.Lock is null)&&confusion==4,"each independent duration ends in one confusion");
 var normal=Turn(state,[1,1,1,1]);Check(normal.State.Turn>first.State.Turn,"normal subsequent turn accepted");
});
Test("fatigue skips duration for Own Tempo and existing confusion; misses still expire",()=>{
 foreach(string kind in new[]{"ordinary","own-tempo","confused","miss"}){
  var f=WithLock(Basis("OUTRAGE",seed:31),0,"OUTRAGE",1);
  if(kind=="own-tempo"){
   var own=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_SLOWPOKE"),40,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(1)),new(0)).Pokemon;
   f=Put(f,0,new(own with{Moves=f.Slots[0].Body!.Pokemon.Moves}));
  }
  if(kind=="confused")f=Put(f,0,f.Slots[0].Body! with{ConfusionTurnsRemaining=5});
  if(kind=="miss")f=Put(f,0,f.Slots[0].Body! with{AccuracyStage=0});
  var r=Turn(f);Check(r.State.Slots[0].Registers.Turn is null,"expired "+kind);
  if(kind=="own-tempo")Check(WildBattleEffective.Ability(f.Slots[0].Body!)=="ABILITY_OWN_TEMPO"&&!r.Trace.Any(t=>t.Purpose=="source-battle.slot-0.confusion-duration"),"Own Tempo prevents fatigue without RNG");
  if(kind is "ordinary" or "miss")Check(r.Trace.Any(t=>t.Purpose=="source-battle.slot-0.confusion-duration"),"fatigue draw "+kind);
  if(kind=="confused")Check(!r.Trace.Any(t=>t.Purpose=="source-battle.slot-0.confusion-duration"),"no second confusion duration");
 }
});
Test("all four recharge actions retain once-only PP through full blocked turn",()=>{
 var names=family.Take(4).ToArray();var f=Frame(Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,names[i],"DEFENSE_CURL") with{AccuracyStage=12,DefenseStage=12,SpecialDefenseStageDelta=6}).ToArray(),31);
 var first=Turn(f);Check(first.State.Slots.All(c=>c.Registers.Turn?.Lock is{Kind:WildBattleCalledLockKind.Recharge,TurnsRemaining:1}),"ready boundary decrements all four timers");
 var second=Turn(first.State);Check(second.State.Slots.All(c=>c.Registers.Turn is null)&&second.Events.OfType<SourceMoveFailedEvent>().Count(e=>e.Reason=="must-recharge")==4,"whole recharge turn");
 Check(!second.Events.OfType<SourcePpCommandEvent>().Any()&&!second.Trace.Any(t=>t.Purpose.EndsWith(".accuracy")),"no recharge PP or hit RNG");
 var normal=Turn(second.State,[1,1,1,1]);Check(normal.Events.OfType<SourceMoveUsedEvent>().Count(e=>e.Move==Id("DEFENSE_CURL"))==4,"four normal next actions");
});
Test("locked slot selection gates and unsupported families remain atomic",()=>{
 var f=WithLock(Basis("THRASH"),0,"THRASH",2);
 Rejected(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f).Select(c=>c.Actor.Slot.Value==0?c with{MoveSlot=1}:c).ToImmutableArray()),"cannot choose another PP owner");
 var bad=WithLock(f,0,"THRASH",4);Rejected(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"out-of-source duration");
 foreach(string name in new[]{"UPROAR","BIDE","METRONOME","TRANSFORM"})Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(name)]),"still gated "+name);
});
Test("actual original C command vectors match frame PP locks and complete RNG",()=>{
 using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
 var rows=source.RootElement.GetProperty("vectors");Check(rows.GetArrayLength()==35,"35 nonempty pinned C vectors");int actions=0,targets=0;
 foreach(var row in rows.EnumerateArray()){
  if(row.GetProperty("kind").GetString()=="action"){
   int numeric=row.GetProperty("move").GetInt32();string name=family.Single(n=>catalog.Moves[Id(n)].SourceMoveId==numeric);uint seed=row.GetProperty("seed").GetUInt32();bool repeated=row.GetProperty("repeated").GetInt32()!=0;
   var f=Basis(name,seed:seed);var body=f.Slots[0].Body!;f=Put(f,0,body with{Pokemon=body.Pokemon with{Moves=body.Pokemon.Moves.SetItem(0,body.Pokemon.Moves[0] with{CurrentPp=5})}});
   if(repeated)f=WithLock(f,0,name,2);
   var r=Act(f,0,1);var locked=r.State.Slots[0].Registers.Turn?.Lock;
   Check(r.State.Rng.Seed==row.GetProperty("finalSeed").GetUInt32()&&r.Trace.Length==row.GetProperty("draws").GetInt32(),name+" C exact RNG");
   Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==row.GetProperty("pp").GetInt32()&&r.Events.OfType<SourcePpCommandEvent>().Count()==row.GetProperty("ppWrites").GetInt32(),name+" C PP commands");
   Check((locked?.TurnsRemaining??0)==Math.Max(row.GetProperty("remaining").GetInt32(),row.GetProperty("recharge").GetInt32()),name+" C timer");actions++;
  }else if(row.GetProperty("kind").GetString()=="target"){
   int actor=row.GetProperty("actor").GetInt32();var f=Basis("THRASH",actor,31) with{SourceAbsentMask=row.GetProperty("absent").GetByte()};
   var r=SourceMoveTargeting.ResolveLearned(f,new(actor),new(actor^1),catalog.Moves[Id("THRASH")]);
   Check(r.Succeeded&&r.Target?.Value==row.GetProperty("target").GetInt32()&&r.Rng.Seed==row.GetProperty("finalSeed").GetUInt32()&&r.Trace.Length==1,"original HandleAction random target "+actor);targets++;
  }
 }
 Check(actions==20&&targets==8,"all executable action and targeting golden rows used");
});
Test("KO removes departing rampage and entry never inherits it; retained enemy locks continue",()=>{
 var mons=new[]{Mon("CHARIZARD",40,"THRASH","DEFENSE_CURL"),Mon("SNORLAX",100,"HYPER_BEAM","DEFENSE_CURL") with{AccuracyStage=12},Mon("SNORLAX",100,"OUTRAGE","DEFENSE_CURL") with{DefenseStage=12},Mon("SNORLAX",100,"DEFENSE_CURL","MINIMIZE")};
 var f=Frame(mons);f=WithLock(f,0,"THRASH",2);f=WithLock(f,2,"OUTRAGE",2);
 f=Put(f,0,f.Slots[0].Body! with{Pokemon=f.Slots[0].Body!.Pokemon with{CurrentHp=1}});
 var party=f.Party(BattleSide.Player);f=f with{Parties=f.Parties.SetItem(0,party with{Members=party.Members.Add(Member(Mon("SNORLAX",100,"DEFENSE_CURL","MINIMIZE")))})};
 var r=Begin(f,null,[1,0,1,3]);var all=new List<SourceBattleEvent>();bool replaced=false;
 for(int guard=0;r.State.FourTurn is not null&&guard<60;guard++){
  if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){
   Check(r.State.PendingSwitches[0].Slot.Value==0&&r.State.Slots[0].Registers.Turn is null,"dead actor lock cleaned before replacement");
   r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2)));replaced=true;
  }else{Check(r.State.FourTurn.Phase!=SourceFourActionPhase.RewardsPending,"player KO does not earn XP");r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));}
  all.AddRange(r.Events);
 }
 Check(replaced&&r.CompletedCleanTurn&&r.State.Slots[0].Member?.Index==2&&r.State.Slots[0].EntrySerial==2,"completed full real replacement cycle");
 Check(r.State.Slots[0].Registers.Turn is null&&!all.OfType<SourceAttackStringEvent>().Any(e=>e.Actor.Value==0&&e.Sequence>all.OfType<SourceCalledPartySwitchedEvent>().Single().Sequence),"entrant has no inherited lock or second action");
 Check(r.State.Slots[1].Registers.Turn?.Lock?.Kind==WildBattleCalledLockKind.Recharge&&r.State.Slots[2].Registers.Turn?.Lock?.Kind==WildBattleCalledLockKind.Rampage,"unrelated locks survive other slot entry");
 var next=Turn(r.State,[0,0,0,0]);Check(next.State.Slots[1].Registers.Turn is null&&next.State.Slots[2].Registers.Turn is null,"next complete turn consumes recharge and exhausts retained rampage");
});
Test("actual C fatigue residual vectors match the shared singles residual owner",()=>{
 using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));int used=0;
 foreach(var row in source.RootElement.GetProperty("vectors").EnumerateArray().Where(r=>r.GetProperty("kind").GetString()=="residual")){
  int mode=row.GetProperty("case").GetInt32();var actor=Mon("CHARIZARD",40,"THRASH","DEFENSE_CURL");
  if(mode==2){var own=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_SLOWPOKE"),40,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(1)),new(0)).Pokemon;actor=new(own with{Moves=actor.Pokemon.Moves});Check(actor.Pokemon.AbilityId=="ABILITY_OWN_TEMPO","source ability fixture");}
  var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,Mon("SNORLAX",100,"DEFENSE_CURL"),new(31));Check(start.Succeeded,start.Fault??"start");
  var state=start.State!;state=state with{Rng=new(31),Player=state.Player with{ConfusionTurnsRemaining=mode==3?3:0,CalledTurnState=new(){Lock=new(WildBattleCalledLockKind.Rampage,Id("THRASH"),0,Id("THRASH"),WildBattleSide.Opponent,mode==0?2:1){UnableToUseMove=mode>=4}}}};
  var r=WildBattleRules.ResolveCalledTurnLockEndTurn(catalog,state);Check(r.Succeeded,r.Fault??"source residual");
  Check((r.State!.Player.CalledTurnState?.Lock?.TurnsRemaining??0)==row.GetProperty("remaining").GetInt32()&&r.State.Player.ConfusionTurnsRemaining==row.GetProperty("confusion").GetInt32(),"original residual lock/confusion "+mode);
  Check(r.State.Rng.Seed==row.GetProperty("finalSeed").GetUInt32()&&r.Trace.Length==row.GetProperty("draws").GetInt32(),"original residual full RNG "+mode);used++;
 }
 Check(used==6,"all exact C residual rows compared");
});
Test("canonical singles continuation executes paralysis script cancellation immediately",()=>{
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("CHARIZARD",40,"THRASH","DEFENSE_CURL")],0,Mon("SNORLAX",100,"DEFENSE_CURL"),new(0));Check(start.Succeeded,start.Fault??"start");
 var state=start.State!;state=state with{Rng=new(0),Player=state.Player with{Status=WildBattleStatus.Paralysis,CalledTurnState=new(){Lock=new(WildBattleCalledLockKind.Rampage,Id("THRASH"),0,Id("THRASH"),WildBattleSide.Opponent,2)}}};
 if(state.CalledParty is { } party)state=state with{CalledParty=party with{Player=party.Player.SetItem(0,party.Player[0] with{Status=WildBattleStatus.Paralysis})}};
 var r=WildBattleRules.ResolveCalledMoveContinuation(catalog,state,WildBattleSide.Player);Check(r.Succeeded,r.Fault??"continuation");
 Check(r.State!.Player.CalledTurnState is null&&r.Battle.Trace.Length==1,"shared script cancels immediately with only paralysis draw");
 Check(r.Battle.Events.OfType<WildBattleMtMoonVolatileEvent>().Any(e=>e.Volatile=="called-rampage"&&e.Reason=="paralysis"),"explicit source correction recorded");
});
Test("retained second PP slot and Pressure obey hit-versus-miss source commands",()=>{
 foreach(string name in new[]{"OUTRAGE","HYDRO_CANNON"}){
  var f=Frame([Mon("CHARIZARD",40,"DEFENSE_CURL",name),Mon("DUSCLOPS",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")]);
  Check(f.Slots[1].Body!.Pokemon.AbilityId=="ABILITY_PRESSURE","authentic Pressure");
  var first=Act(f,0,1,1);var pp=first.Events.OfType<SourcePpCommandEvent>().Single();
  Check(pp.PpSlot==1&&pp.Before-pp.After==2&&first.State.Slots[0].Registers.Turn?.Lock?.PpSlot==1,"real slot1 pressure cost "+name);
  Check(first.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==f.Slots[0].Body!.Pokemon.Moves[0].CurrentPp,"slot0 untouched");
  if(name=="OUTRAGE"){
   var hit=Act(first.State,0,1,1);Check(!hit.Events.OfType<SourcePpCommandEvent>().Any(),"Pressure cannot charge successful forced continuation");
   var m=first.State.Slots[0].Body!;var missed=Act(Put(first.State with{Rng=new(31)},0,m with{AccuracyStage=0}),0,1,1);
   Check(missed.Events.OfType<SourcePpCommandEvent>().Single() is{PpSlot:1,CommandCost:2}&&missed.State.Slots[0].Registers.Turn?.Lock?.PpSlot==1,"miss charges actual retained slot with pressure");
  }
 }
});
if(args.Contains("--combined"))Test("combined sleep-target-mark integration preserves sure-hit and Yawn cancels Rampage",()=>{
 var f=WithLock(Basis("HYPER_BEAM"),0,"HYPER_BEAM",1);var c=f.Slots[0];f=Put(f,0,c.Body! with{Status=3});
 f=f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Registers=c.Registers with{Turn=c.Registers.Turn! with{SureHitTurnsRemaining=2,SureHitSource=new(1)}}})};
 var timed=Turn(f);Check(timed.State.Slots[0].Registers.Turn is{Lock:null,SureHitTurnsRemaining:1},"recharge timer preserves independent sure-hit");
 var yawn=WithLock(Basis("THRASH"),0,"THRASH",3);yawn=Put(yawn,0,yawn.Slots[0].Body! with{YawnTurnsRemaining=1});
 var slept=Turn(yawn);Check(WildBattleStatus.IsSleeping(slept.State.Slots[0].Body!.Status)&&slept.State.Slots[0].Registers.Turn is null,"Yawn cancels rampage after Thrash counter");
});
File.WriteAllText(args[1],Json(new{runtime=Environment.Version.ToString(),groups,checks,failures,results})+"\n");return failures==0?0:1;
