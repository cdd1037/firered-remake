using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if(args.Length!=3)throw new ArgumentException("PRODUCT_ROOT SOURCE_VECTORS_JSON OUTPUT_JSON");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
using var source=JsonDocument.Parse(File.ReadAllBytes(args[1]));
int groups=0,checks=0,failures=0,turns=0;var results=new List<object>();
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
void Test(string label,Action work){groups++;try{work();results.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception e){failures++;results.Add(new{label,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+label+" "+e);}}
string Json<T>(T value)=>JsonSerializer.Serialize(value);
MoveId Id(string name)=>new("pret:MOVE_"+name);
WildBattleCombatant Mon(string species,string moves="DEFENSE_CURL",int level=50){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});}
WildBattleCalledPartyMember Member(WildBattleCombatant b)=>new(b.Pokemon,b.HeldItem,b.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? mons=null,WildBattleCombatant[]? players=null,WildBattleCombatant[]? opponents=null,uint seed=31){
 var m=mons??[Mon("PIDGEY","CONFUSE_RAY,DEFENSE_CURL"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")];
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,new[]{Member(m[0]),Member(m[2])}.Concat((players??[]).Select(Member)).ToImmutableArray()),new(BattleSide.Opponent,new[]{Member(m[1]),Member(m[3])}.Concat((opponents??[]).Select(Member)).ToImmutableArray())],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};}
SourceBattleFrame Put(SourceBattleFrame f,int i,WildBattleCombatant b){var c=f.Slots[i];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=b}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(b))})};}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int slot=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,slot,new(target)));Check(r.Succeeded,r.Fault??"action");Check(SourceBattleIdentityRules.Validate(r.State)==null,"action identity");return r;}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"turn");Check(SourceBattleIdentityRules.Validate(r.State)==null,"turn identity");return r;}
SourceBattleFrame Entry(SourceBattleFrame f)=>Good(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision)).State;
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? slots=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int pos=slots?[c.Slot.Value]??0;var m=catalog.Moves[c.Body!.Pokemon.Moves[pos].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,new(m.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:targets?[c.Slot.Value]??(c.Slot.Value^1)));}).ToImmutableArray();
SourceFourActionResult Begin(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? choices=null)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices??Choices(f)));
SourceFourActionResult Next(SourceFourActionResult r){var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var a=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var b=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);Check(Json(a)==Json(b),"serialized exact state/events/RNG continuation");return Good(a);}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var e=r.Events.ToList();for(int i=0;i<20&&r.State.FourTurn!=null;i++){Check(r.State.FourTurn.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume,"no unexpected external boundary "+r.State.FourTurn.Phase);r=Next(r);e.AddRange(r.Events);}Check(r.CompletedCleanTurn,"one complete turn");turns++;return(r.State,e);}
SourceActionSelection Switch(SourceBattleFrame f,int slot,int incoming)=>new(f.Slots[slot].Occupant!.Value,SourceActionKind.Switch,Incoming:new((BattleSide)(slot&1),incoming));
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic "+why+": "+r.Fault);

Test("exact affected-family singles baseline, runtime and source pin",()=>{
 Check(Environment.Version.Major==10,"runtime10");Check(source.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"source pin");
 Check(LegacyParity.Run(args[0],true)==File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../..","legacy-baseline.json")).Trim(),"36 full singles state/events/RNG fingerprints unchanged");
 foreach(var name in new[]{"CONFUSE_RAY","SUPERSONIC","SWEET_KISS","MEAN_LOOK","SPIDER_WEB","BLOCK","WRAP","BIND","FIRE_SPIN","WHIRLPOOL","CLAMP","SAND_TOMB"})Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(name)]),name+" admitted");
 Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id("TEETER_DANCE")]),"authenticated Teeter Dance recipe admitted");
});
Test("verbatim source confusion and Wrap command vectors retain RNG order and actual source position",()=>{
 foreach(var v in source.RootElement.GetProperty("vectors").EnumerateArray()){
  bool wrap=v.GetProperty("kind").GetString()=="wrap";var f=Frame([Mon("PIDGEY",wrap?"BIND":"CONFUSE_RAY",5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("PIDGEY",wrap?"BIND":"CONFUSE_RAY",5)with{AccuracyStage=12},Mon("SNORLAX")],seed:v.GetProperty("seed").GetUInt32());
  var a=Act(f,2,3);var b=a.State.Slots[3].Body!;Check((wrap?b.BindTurnsRemaining:b.ConfusionTurnsRemaining)==v.GetProperty("turns").GetInt32(),"duration vector");Check(a.Trace.Length==v.GetProperty("draws").GetInt32()&&a.State.Rng.Seed==v.GetProperty("finalSeed").GetUInt32(),"exact source RNG");
  if(wrap)Check(a.State.Slots[3].Registers.WrappedBy?.Value==2,"source2 target3");Check(a.State.Slots[0]==f.Slots[0]&&a.State.Slots[1]==f.Slots[1],"same species independent positions");
 }
});
Test("selected ally Pressure, Own Tempo, existing confusion, Safeguard and protection keep PP and draw ordering",()=>{
 var f=Frame([Mon("PIDGEY","CONFUSE_RAY"),Mon("SNORLAX"),Mon("DUSCLOPS"),Mon("SNORLAX")]);var a=Act(f,0,2);Check(a.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==2&&a.State.Slots[2].Body!.ConfusionTurnsRemaining>0,"ally Pressure actual target");
 f=Put(f,2,Mon("SMEARGLE"));a=Act(f,0,2);Check(a.Trace.IsEmpty&&a.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability=="ABILITY_OWN_TEMPO","Own Tempo before accuracy");
 f=Put(f,2,Mon("SNORLAX")with{ConfusionTurnsRemaining=3});a=Act(f,0,2);Check(a.Trace.IsEmpty&&a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="already-confused","existing confusion before accuracy");
 f=Put(f,2,Mon("SNORLAX"));f=f with{Sides=f.Sides.SetItem(0,f.Sides[0] with{SafeguardTurnsRemaining=3})};a=Act(f,0,2);Check(a.Trace.Length==1&&a.Trace[0].Purpose.EndsWith(".accuracy")&&a.State.Slots[2].Body!.ConfusionTurnsRemaining==0,"Safeguard after accuracy");
 f=Frame();f=Put(f,3,f.Slots[3].Body! with{Protected=true});a=Act(f,0,3);Check(a.Trace.IsEmpty&&a.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="protected"),"Protect no accuracy/duration");
 f=Put(Frame(),0,Mon("PIDGEY","CONFUSE_RAY")with{Flinched=true});a=Act(f,0,3);Check(a.Trace.IsEmpty&&!a.Events.OfType<SourcePpCommandEvent>().Any(),"flinch cancels before PP");
});
Test("Supersonic misses, Soundproof and zero PP terminate at their source command boundaries",()=>{
 var f=Frame([Mon("PIDGEY","SUPERSONIC"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")],seed:2);var a=Act(f,0,3);Check(a.Trace.Length==1&&a.Events.OfType<SourceMoveMissedEvent>().Count()==1&&a.State.Slots[3].Body!.ConfusionTurnsRemaining==0,"55 percent accuracy miss without duration");
 f=Put(f,3,Mon("MR_MIME"));a=Act(f,0,3);Check(a.Trace.IsEmpty&&a.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability=="ABILITY_SOUNDPROOF"&&a.Events.OfType<SourcePpCommandEvent>().Count()==1,"Soundproof with one PP command");
 f=Put(f,3,Mon("SNORLAX"));var actor=f.Slots[0].Body!;f=Put(f,0,actor with{Pokemon=actor.Pokemon with{Moves=[actor.Pokemon.Moves[0] with{CurrentPp=0}]}});a=Act(f,0,3);Check(a.Trace.IsEmpty&&!a.Events.OfType<SourcePpCommandEvent>().Any()&&a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="no-pp","no PP no effect draw");
});
Test("Mean Look family targets actual Ghost or ally slot without accuracy RNG; duplicate and Protect are failures",()=>{
 foreach(var move in new[]{"MEAN_LOOK","SPIDER_WEB","BLOCK"}){
 var f=Frame([Mon("PIDGEY",move),Mon("SNORLAX"),Mon("SPEAROW"),Mon("GASTLY")]);var a=Act(f,0,3);Check(a.Trace.IsEmpty&&a.State.Slots[3].Registers.MeanLookSource?.Value==0,"Ghost target no type immunity");
 var again=Act(a.State,0,3);Check(again.Trace.IsEmpty&&again.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="already-trapped","duplicate no draw");
 var ally=Act(f,0,2);Check(ally.State.Slots[2].Registers.MeanLookSource?.Value==0,"ally actual position");
 var protectedFrame=Put(f,3,f.Slots[3].Body! with{Protected=true});var blocked=Act(protectedFrame,0,3);Check(blocked.Trace.IsEmpty&&blocked.State.Slots[3].Registers.MeanLookSource is null,"Protect blocks without draw");}
});
Test("all six damaging bind moves retain exact move, immune target does not roll duration, bound target does not reroll",()=>{
 foreach(var move in new[]{"WRAP","BIND","FIRE_SPIN","WHIRLPOOL","CLAMP","SAND_TOMB"}){
 var f=Frame([Mon("PIDGEY",move,5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")]);var a=Act(f,0,3);Check(a.State.Slots[3].Body!.BindingMove==Id(move)&&a.State.Slots[3].Registers.WrappedBy?.Value==0,"move/source retained "+move);
 var repeat=Act(a.State,0,3);Check(!repeat.Trace.Any(t=>t.Purpose.EndsWith(".bind-duration"))&&repeat.State.Slots[3].Body!.BindTurnsRemaining==a.State.Slots[3].Body!.BindTurnsRemaining,"no refresh or duration draw "+move);}
 var immune=Frame([Mon("PIDGEY","WRAP",5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("GASTLY")]);var blocked=Act(immune,0,3);Check(blocked.State.Slots[3].Registers.WrappedBy is null&&!blocked.Trace.Any(t=>t.Purpose.EndsWith(".bind-duration")),"Ghost immunity no duration");
});
Test("two full four-action turns independently bind both same-species positions and residuals tick once",()=>{
 var f=Frame([Mon("PIDGEY","WRAP,DEFENSE_CURL",5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW","BIND,DEFENSE_CURL",5)with{AccuracyStage=12},Mon("SNORLAX")]);
 var r=Finish(Begin(f));Check(r.Events.OfType<SourceBindingDamageEvent>().Select(x=>x.Side.Value).Order().SequenceEqual(new[]{1,3}),"one residual per target");Check(r.State.Slots[1].Registers.WrappedBy?.Value==0&&r.State.Slots[3].Registers.WrappedBy?.Value==2,"independent source links");
 int one=r.State.Slots[1].Body!.BindTurnsRemaining,three=r.State.Slots[3].Body!.BindTurnsRemaining;
 var next=Finish(Begin(r.State,Choices(r.State,[1,0,1,0])));Check(next.State.Slots[1].Body!.BindTurnsRemaining==one-1&&next.State.Slots[3].Body!.BindTurnsRemaining==three-1,"one tick next turn");
});
Test("real Follow Me target resolution redirects both selected volatile families to the retained holder",()=>{
 var f=Frame([Mon("PIDGEY","MEAN_LOOK",5),Mon("SNORLAX"),Mon("SPEAROW","BIND",5)with{AccuracyStage=12},Mon("SNORLAX","FOLLOW_ME")]);var r=Finish(Begin(f,Choices(f,targets:[1,0,1,2])));
 Check(r.State.Slots[3].Registers.MeanLookSource?.Value==0&&r.State.Slots[3].Registers.WrappedBy?.Value==2,"Follow Me redirects both independent source positions");Check(r.State.Slots[1].Registers.MeanLookSource is null&&r.State.Slots[1].Registers.WrappedBy is null,"original selected position untouched");
});
Test("expiry skips damage and clears binding source using all six original residual vectors",()=>{
 foreach(var v in source.RootElement.GetProperty("residual").EnumerateArray()){
 var f=Act(Frame([Mon("PIDGEY","BIND,DEFENSE_CURL",5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")]),0,3).State;int before=v.GetProperty("before").GetInt32();f=Put(f,3,f.Slots[3].Body! with{BindTurnsRemaining=before});int hp=f.Slots[3].Body!.Pokemon.CurrentHp;
 var r=Finish(Begin(f,Choices(f,[1,0,0,0])));Check(r.State.Slots[3].Body!.BindTurnsRemaining==v.GetProperty("after").GetInt32(),"source decrement");
 Check(r.State.Slots[3].Body!.Pokemon.CurrentHp==hp-(before==1?0:Math.Max(1,f.Slots[3].Body!.Pokemon.Stats.MaxHp/16)),"source sixteenth or expiry");Check((r.State.Slots[3].Registers.WrappedBy is null)==(before==1),"source reference expires");}
});
Test("bound voluntary switch is atomic; source switch clears only its own outgoing target link",()=>{
 var f=Frame([Mon("PIDGEY","WRAP,DEFENSE_CURL",5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW","BIND,DEFENSE_CURL",5)with{AccuracyStage=12},Mon("SNORLAX")],[Mon("RATTATA")],[Mon("RATTATA")]);f=Act(f,0,1).State;f=Act(f,2,3).State;
 Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,[1,0,1,0]).SetItem(1,Switch(f,1,2))),"bound target cannot switch");
 var r=Begin(f,Choices(f,[1,0,1,0]).SetItem(0,Switch(f,0,2)));r=Next(r);Check(r.State.Slots[0].EntrySerial==2&&r.State.Slots[1].Registers.WrappedBy is null&&r.State.Slots[1].Body!.BindTurnsRemaining==0,"departing source clears target1");Check(r.State.Slots[3].Registers.WrappedBy?.Value==2,"partner bind untouched");Finish(r);
});
Test("Baton Pass source preserves outgoing Mean Look and clears outgoing Wrap across actual entry",()=>{
 var f=Frame([Mon("PIDGEY","MEAN_LOOK,BIND,BATON_PASS",5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")],[Mon("GYARADOS")]);f=Act(f,0,1).State;f=Act(f,0,3,1).State;f=Entry(f);
 var r=Begin(f,Choices(f,[2,0,0,0]));while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ActionSwitchPending,"real Baton pause");
 r=Good(SourceFourActionTurns.ChooseActionSwitch(catalog,r.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2)));
 Check(r.State.Slots[0].EntrySerial==2&&r.State.Slots[1].Registers.MeanLookSource?.Value==0,"Mean Look keeps source position across Baton");Check(r.State.Slots[3].Registers.WrappedBy is null&&r.State.Slots[3].Body!.BindTurnsRemaining==0,"Wrap source clears across Baton");Check(r.Events.OfType<SourceAbilityActivatedEvent>().Any(e=>e.Side.Value==0&&e.Ability=="ABILITY_INTIMIDATE"),"real incoming ability");Finish(r);
});
Test("Baton Pass target carries Mean Look and confusion while dropping Wrap",()=>{
 var f=Frame([Mon("PIDGEY","MEAN_LOOK,CONFUSE_RAY,BIND",5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX","BATON_PASS")],opponents:[Mon("RATTATA")]);f=Act(f,0,3).State;f=Act(f,0,3,1).State;f=Act(f,0,3,2).State;
 f=Put(f,0,Mon("PIDGEY"));f=Put(f,3,f.Slots[3].Body! with{ConfusionTurnsRemaining=5});f=f with{Rng=new(1)};
 var r=Begin(f);while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ActionSwitchPending,"confused actor passed canceller into real Baton");int remaining=r.State.Slots[3].Body!.ConfusionTurnsRemaining;
 r=Good(SourceFourActionTurns.ChooseActionSwitch(catalog,r.State,r.Continuation!.Value,new(3),new(BattleSide.Opponent,2)));Check(r.State.Slots[3].Registers.MeanLookSource?.Value==0&&r.State.Slots[3].Body!.ConfusionTurnsRemaining==remaining,"passed retained volatile values");Check(r.State.Slots[3].Registers.WrappedBy is null&&r.State.Slots[3].Body!.BindingMove is null,"target Wrap dropped");Finish(r);
});
Test("Roar replaces the actual bound target immediately and clears its own incoming volatile state",()=>{
 var f=Frame([Mon("PIDGEY","MEAN_LOOK,BIND,ROAR",70)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX",level:50)],opponents:[Mon("GYARADOS")]);f=Act(f,0,3).State;f=Act(f,0,3,1).State;f=Entry(f);
 var r=Finish(Begin(f,Choices(f,[2,0,0,0],[3,0,3,2])));Check(r.State.Slots[3].EntrySerial==2&&r.State.Slots[3].Member!.Value.Index==2,"actual source selected target entered");Check(r.State.Slots[3].Registers.MeanLookSource is null&&r.State.Slots[3].Registers.WrappedBy is null,"forced entry drops target links");Check(!r.Events.OfType<SourceBindingDamageEvent>().Any(e=>e.Side.Value==3),"incoming member does not inherit Wrap residual");
});
Test("bound target faint clears scalar and slot reference atomically before deferred KO replacement",()=>{
 var f=Frame([Mon("PIDGEY","DEFENSE_CURL",20),Mon("SNORLAX","BIND,DEFENSE_CURL",5)with{AccuracyStage=12},Mon("SPEAROW"),Mon("SNORLAX")],[Mon("GYARADOS")]);f=Act(f,1,0).State;f=Put(f,0,f.Slots[0].Body! with{Pokemon=f.Slots[0].Body!.Pokemon with{CurrentHp=1}});f=Entry(f);
 var r=Begin(f,Choices(f,[0,1,0,0]));while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);r=Next(r);
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending&&r.State.IsDeferredKoVacant(new(0)),"Wrap KO waits at endturn");Check(r.Events.OfType<SourceBindingDamageEvent>().Count()==1&&r.Events.OfType<SourceFaintedEvent>().Single().Side.Value==0,"one residual KO");Check(r.State.Slots[0].Registers.WrappedBy is null&&r.State.Slots[0].Body!.BindTurnsRemaining==0,"clean scalar/source before continuation");
 var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var replacement=SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2));var replay=SourceFourActionTurns.ChooseReplacement(catalog,restored,r.Continuation.Value,new(0),new(BattleSide.Player,2));Check(Json(replacement)==Json(replay),"replacement JSON replay");r=Good(replacement);var done=Finish(r);Check(!done.Events.OfType<SourceBindingDamageEvent>().Any(),"resuming residual cursor never repeats Wrap");
});
Test("source faint from direct damage releases its Mean Look and Wrap positions",()=>{
 var f=Frame([Mon("PIDGEY","MEAN_LOOK,BIND",1)with{AccuracyStage=12},Mon("SNORLAX","TACKLE",100),Mon("SPEAROW"),Mon("SNORLAX")]);f=Act(f,0,1).State;f=Act(f,0,3,1).State;
 var a=Act(f,1,0);Check(a.Events.OfType<SourceFaintedEvent>().Single().Side.Value==0,"actual direct KO");Check(a.State.Slots[1].Registers.MeanLookSource is null&&a.State.Slots[3].Registers.WrappedBy is null&&a.State.Slots[3].Body!.BindTurnsRemaining==0,"fainted source reference cleanup");
});
Test("malformed binding and stale source reject before PP or RNG",()=>{
 var f=Act(Frame([Mon("PIDGEY","BIND",5)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")]),0,3).State;
 f=Put(f,3,f.Slots[3].Body! with{BindingMove=Id("TACKLE")});Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)),"wrong binding move");
 var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(1)));Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"kernel malformed state atomic");
});
Check(groups>0&&checks>0,"nonempty suite");var result=new{groups,checks,failures,completedTurns=turns,runtime=Environment.Version.ToString(),results};File.WriteAllText(args[2],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true})+"\n");Console.WriteLine(Json(result));return failures==0?0:1;
