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
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}

var snapshots=new List<object>();
foreach(bool canonical in new[]{true})foreach(string name in new[]{"STOCKPILE","SPIT_UP","SWALLOW","CHARGE"})foreach(int count in new[]{0,1,2,3})foreach(string branch in new[]{"ordinary","full","protected","miss","sleep","zero-pp"}){
 var actor=Mon("SNORLAX",60,name,"DEFENSE_CURL");var target=Mon("SNORLAX",100,"DEFENSE_CURL");
 actor=actor with{CalledVolatile=count==0?null:new(){StockpileCount=count},Pokemon=actor.Pokemon with{CurrentHp=branch=="full"?actor.Pokemon.Stats.MaxHp:1}};
 if(branch=="sleep")actor=actor with{Status=3};
 if(branch=="miss") {actor=actor with{AccuracyStage=0};target=target with{EvasionStage=12};}
 if(branch=="protected")target=target with{Protected=true};
 if(branch=="zero-pp")actor=actor with{Pokemon=actor.Pokemon with{Moves=actor.Pokemon.Moves.SetItem(0,actor.Pokemon.Moves[0] with{CurrentPp=0})}};
 var state=new WildBattleState(actor,target,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
 var result=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));
 if(!result.Succeeded)throw new Exception($"baseline {name}/{branch}: {result.Fault}");
 snapshots.Add(new{canonical,name,count,branch,before=state,result});
}
foreach(string name in new[]{"STOCKPILE","CHARGE"})foreach(uint seed in new uint[]{0,31,127}){
 var actor=Mon("SNORLAX",100,name,"SPIT_UP","SWALLOW","THUNDER_SHOCK");var target=Mon("SNORLAX",100,"DEFENSE_CURL");
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(seed));if(!start.Succeeded)throw new Exception(start.Fault);
 var state=start.State!;var records=new List<object>();
 for(int i=0;i<4;i++){var result=WildBattleRules.ResolveMove(catalog,state,name=="CHARGE"&&i>0?3:i==3?1:0);if(!result.Succeeded)throw new Exception(result.Fault);records.Add(result);state=result.State!;}
 snapshots.Add(new{name,seed,records});
}
if(args.Contains("--capture")){if(File.Exists(args[1]))throw new Exception("Immutable before-results already exist");File.WriteAllText(args[1],Json(new{baseline="a7d1936f9e5f446a36209599dc909749ec96eec0",snapshots})+"\n");return 0;}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
Test("immutable complete singles state/events/invocation/RNG",()=>{
 Check(Environment.Version.Major==10,"hosted runtime10");
 using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-before.json")));
 var prior=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(prior.Length==102&&prior.Length==snapshots.Count,"nonempty before-results");
 for(int i=0;i<prior.Length;i++)Check(prior[i].GetRawText()==Json(snapshots[i]),"complete parity case "+i);
});
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied=null,int reserves=0,uint seed=31){
 var m=supplied??[Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")];
 var player=new[]{Member(m[0]),Member(m[2])}.Concat(Enumerable.Range(0,reserves).Select(_=>Member(Mon("SNORLAX",100,"DEFENSE_CURL")))).ToImmutableArray();
 var foe=new[]{Member(m[1]),Member(m[3])}.Concat(Enumerable.Range(0,reserves).Select(_=>Member(Mon("SNORLAX",100,"DEFENSE_CURL")))).ToImmutableArray();
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
  BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,player),new(BattleSide.Opponent,foe)],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,player.Length).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int i,WildBattleCombatant m){var c=f.Slots[i];var key=c.Member!.Value;var p=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=m}),Parties=f.Parties.SetItem((int)key.Side,p with{Members=p.Members.SetItem(key.Index,Member(m))})};}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{
 int pos=positions?[c.Slot.Value]??c.Registers.Turn?.Lock?.PpSlot??0;var move=catalog.Moves[c.Registers.Turn?.Lock?.Move??c.Body!.Pokemon.Moves[pos].Move];
 var target=c.Registers.Turn?.Lock?.Target??new BattlerSlot(targets?[c.Slot.Value]??(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1));
 return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,target);
}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output:"+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Next(SourceFourActionResult prior){var f=prior.State;var token=prior.Continuation!.Value;
 SourceFourActionResult Resume(SourceBattleFrame current)=>current.FourTurn!.Phase switch{
  SourceFourActionPhase.RewardsPending when current.Rewards!.Pending is{} pending=>SourceFourRewards.Learn(catalog,current,token,new(pending.Recipient,pending.Moves[0].Level,pending.Moves[0].Move,null)),
  SourceFourActionPhase.RewardsPending=>SourceFourRewards.Settle(catalog,current,token),
  SourceFourActionPhase.ReplacementsPending=>SourceFourActionTurns.ChooseReplacement(catalog,current,token,current.PendingSwitches[0].Slot,new(current.PendingSwitches[0].Slot.Side,current.PendingSwitches[0].LegalPartyIndices[0])),
  _=>SourceFourActionTurns.Advance(catalog,current,token)};
 var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(f))!;var actual=Resume(f);var replay=Resume(restored);Check(Json(actual)==Json(replay),"exact JSON-resume state/events/RNG");return Ok(actual);
}
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace,List<SourceFourActionPhase> Pauses) Turn(SourceBattleFrame f,int[]? p=null,int[]? t=null){
 var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,p,t)));var events=r.Events.ToList();var trace=r.Trace.ToList();var phases=new List<SourceFourActionPhase>();int steps=0;
 while(r.State.FourTurn is not null){Check(++steps<80,"bounded turn progress");phases.Add(r.State.FourTurn.Phase);r=Next(r);events.AddRange(r.Events);trace.AddRange(r.Trace);}
 Check(r.CompletedCleanTurn,"complete turn or terminal battle");return(r.State,events,trace,phases);
}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int position=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,position,new(target)));Check(r.Succeeded,r.Fault??"action");return r;}
void Rejected(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Trace.IsEmpty&&r.Events.IsEmpty,why+" atomic:"+r.Fault);
Test("all four recipes retain genuine slot identity; Stockpile cap has no stat boosts",()=>{
 foreach(string name in new[]{"STOCKPILE","SPIT_UP","SWALLOW","CHARGE"})Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(name)]),name+" admitted");
 var f=Frame([Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"STOCKPILE","SPIT_UP","SWALLOW","CHARGE"),Mon("SNORLAX",100,"DEFENSE_CURL")]);
 for(int count=1;count<=3;count++){
  var r=Turn(f);f=r.State;var actor=f.Slots[2].Body!;
  Check(actor.CalledVolatile?.StockpileCount==count&&actor.DefenseStage==6&&actor.SpecialDefenseStageDelta==0,"only counter changes");
  Check(r.Events.OfType<SourceMoveUsedEvent>().Count()==4&&r.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="stockpile").Side.Value==2,"four actions and exact slot2 event");
 }
 var cap=Turn(f);Check(cap.State.Slots[2].Body!.CalledVolatile?.StockpileCount==3&&cap.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Actor.Value==2&&e.Reason=="stockpile-full"),"cap spends ordinary PP and fails");
 Check(cap.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("STOCKPILE")].Pp-4,"cap still PP");
});
Test("Spit Up multiplies base plus-two, Helping Hand, STAB and doubles screens with no crit or variance",()=>{
 int Damage(int count,bool helping,bool screen){
  var f=Frame([Mon("SNORLAX",30,"SPIT_UP") with{CalledVolatile=new(){StockpileCount=count}},Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")]);
  if(helping){var c=f.Slots[0];f=f with{Slots=f.Slots.SetItem(0,c with{Registers=c.Registers with{HelpingHand=true}})};}
  if(screen)f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{ReflectTurnsRemaining=5})};
  var actor=f.Slots[0].Body!;var target=f.Slots[1].Body!;int raw=actor.Pokemon.Stats.Attack*100*(2*actor.Pokemon.Level/5+2)/target.Pokemon.Stats.Defense/50;
  if(screen)raw=2*(raw/3);int expected=(Math.Max(1,raw)+2)*count;if(helping)expected=expected*15/10;expected=expected*15/10;
  var r=Act(f,0,1);var hp=r.Events.OfType<SourceHpChangedEvent>().Single();
  Check(hp.Damage==expected,"ordered base/count/helping/STAB " + count+"/"+helping+"/"+screen+" got "+hp.Damage+" expected "+expected);
  Check(r.State.Slots[0].Body!.CalledVolatile is null&&!hp.Critical&&hp.RandomPercent==100,"consumed with fixed100");
  Check(r.Trace.Select(t=>t.Purpose).SequenceEqual(new[]{"source-battle.slot-0.accuracy","source-battle.slot-0.secondary-effect"}),"exact accuracy+zero-effect draw");return hp.Damage;
 }
 foreach(bool helping in new[]{false,true})foreach(bool screen in new[]{false,true})foreach(int count in new[]{1,2,3})Damage(count,helping,screen);
});
Test("Spit Up miss keeps counter and double PP; Protect, immunity and substitute consume correctly",()=>{
 SourceBattleFrame F()=>Frame([Mon("SNORLAX",30,"SPIT_UP") with{CalledVolatile=new(){StockpileCount=2}},Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")]);
 var f=F();f=Put(f,0,f.Slots[0].Body! with{AccuracyStage=0});f=Put(f,1,f.Slots[1].Body! with{EvasionStage=12});var miss=Act(f,0,1);
 Check(miss.State.Slots[0].Body!.CalledVolatile!.StockpileCount==2&&miss.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==8,"miss retains two and spends2");
 Check(miss.Events.OfType<SourcePpCommandEvent>().Count()==2&&miss.Trace.Length==1,"one accuracy only and two PP commands");
 foreach(string branch in new[]{"protect","empty-protect","ghost","wonder-guard","substitute","endure","focus-band"}){
  f=F();var foe=f.Slots[1].Body!;
  if(branch.Contains("protect"))f=Put(f,1,foe with{Protected=true});
  if(branch=="empty-protect")f=Put(f,0,f.Slots[0].Body! with{CalledVolatile=null});
  if(branch=="ghost")f=Put(f,1,Mon("GENGAR",100,"DEFENSE_CURL"));
  if(branch=="wonder-guard")f=Put(f,1,Mon("SHEDINJA",100,"DEFENSE_CURL"));
  if(branch=="substitute")f=Put(f,1,foe with{CalledVolatile=new(){SubstituteHp=10}});
  if(branch is "endure" or "focus-band")f=Put(f,1,foe with{Pokemon=foe.Pokemon with{CurrentHp=1},Enduring=branch=="endure",HeldItem=branch=="focus-band"?"ITEM_FOCUS_BAND":"ITEM_NONE"});
  var r=Act(f,0,1);Check((r.State.Slots[0].Body!.CalledVolatile?.StockpileCount??0)==0,branch+" consumed or empty");
  if(branch.Contains("protect"))Check(r.Trace.IsEmpty&&r.Events.OfType<SourceMoveFailedEvent>().Single().Reason==(branch=="protect"?"protected":"stockpile-empty"),branch+" no accuracy");
  if(branch is "ghost" or "wonder-guard")Check(r.State.Slots[1].Body!.Pokemon.CurrentHp==f.Slots[1].Body!.Pokemon.CurrentHp,branch+" no loss");
  if(branch=="wonder-guard")Check(r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_WONDER_GUARD"),"shared type command");
  if(branch=="substitute")Check(r.Events.OfType<SourceSubstituteDamagedEvent>().Single().Damage>10&&r.State.Slots[1].Body!.Pokemon.CurrentHp==foe.Pokemon.CurrentHp,"substitute breaks without body loss");
  if(branch=="endure")Check(r.State.Slots[1].Body!.Pokemon.CurrentHp==1&&r.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured,"Endure");
  if(branch=="focus-band")Check(r.Trace.Count(t=>t.Purpose.EndsWith(".focus-band"))==1,"one Focus Band draw");
 }
});
Test("Swallow fractions, full HP consumption and inability preserve storage",()=>{
 foreach(int count in new[]{0,1,2,3})foreach(bool full in new[]{false,true}){
  var actor=Mon("SNORLAX",60,"SWALLOW") with{CalledVolatile=count==0?null:new(){StockpileCount=count}};
  actor=actor with{Pokemon=actor.Pokemon with{CurrentHp=full?actor.Pokemon.Stats.MaxHp:1}};
  var f=Frame([Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),actor,Mon("SNORLAX",100,"DEFENSE_CURL")]);
  var r=Act(f,2,2);var m=r.State.Slots[2].Body!;
  int expected=full?actor.Pokemon.Stats.MaxHp:count==0?1:Math.Min(actor.Pokemon.Stats.MaxHp,1+actor.Pokemon.Stats.MaxHp/(1<<(3-count)));
  Check(m.Pokemon.CurrentHp==expected&&(m.CalledVolatile?.StockpileCount??0)==0&&r.Trace.IsEmpty,"fraction/full/zero "+count+"/"+full);
  Check(r.Events.OfType<SourceRecoveredEvent>().Count()==(count>0&&!full?1:0),"only actual heal receipt");
 }
 foreach(string move in new[]{"STOCKPILE","SPIT_UP","SWALLOW","CHARGE"})foreach(string block in new[]{"sleep","flinch","no-pp"}){
  var actor=Mon("SNORLAX",60,move,"DEFENSE_CURL") with{CalledVolatile=new(){StockpileCount=2}};
  if(block=="sleep")actor=actor with{Status=3};if(block=="flinch")actor=actor with{Flinched=true};
  if(block=="no-pp")actor=actor with{Pokemon=actor.Pokemon with{Moves=actor.Pokemon.Moves.SetItem(0,actor.Pokemon.Moves[0] with{CurrentPp=0})}};
  var f=Frame([actor,Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")]);var r=Act(f,0,move=="SPIT_UP"?1:0);
  Check(r.State.Slots[0].Body!.CalledVolatile!.StockpileCount==2&&r.State.Slots[0].Body!.ChargeTurnsRemaining==0&&!r.Events.OfType<SourcePpCommandEvent>().Any(),move+"/"+block+" stops before effect and PP");
 }
});
Test("Charge lasts the current and next turn; Electric damage doubles and no stats change",()=>{
 var actor=Mon("SNORLAX",30,"CHARGE","THUNDER_SHOCK","TACKLE","DEFENSE_CURL");actor=actor with{Pokemon=actor.Pokemon with{Moves=actor.Pokemon.Moves.SetItem(0,actor.Pokemon.Moves[0] with{CurrentPp=1})}};
 var f=Frame([Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),actor,Mon("SNORLAX",100,"DEFENSE_CURL")]);var first=Turn(f);
 Check(first.State.Slots[2].Body!.ChargeTurnsRemaining==1&&first.State.Slots[2].Body!.SpecialDefenseStageDelta==0&&first.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==0,"setup one final PP and same-turn countdown");
 foreach(int slot in new[]{1,2}){
  var charged=Act(first.State,2,3,slot);var plain=Act(Put(first.State,2,first.State.Slots[2].Body! with{ChargeTurnsRemaining=0}),2,3,slot);
  var a=charged.Events.OfType<SourceHpChangedEvent>().Single();var b=plain.Events.OfType<SourceHpChangedEvent>().Single();
  Check(Json(charged.Trace)==Json(plain.Trace),"same RNG");
  Check(slot==1?a.Damage>=2*b.Damage&&a.Damage<=2*b.Damage+1:a.Damage==b.Damage,"Electric double before variance; Normal unchanged");
 }
 var second=Turn(first.State,[0,0,1,0]);Check(second.State.Slots[2].Body!.ChargeTurnsRemaining==0&&second.State.Turn==2,"expires next turn");
 var third=Turn(second.State,[0,0,1,0]);Check(third.State.Turn==3,"ordinary next turn progresses");
 var asleep=Put(first.State,2,first.State.Slots[2].Body! with{Status=3});var blocked=Turn(asleep,[0,0,1,0]);
 Check(blocked.State.Slots[2].Body!.ChargeTurnsRemaining==0&&!blocked.Events.OfType<SourcePpCommandEvent>().Any(e=>e.Actor.Value==2),"incapacity still expires at residual slot");
 var noPp=Act(first.State,2,2,0);Check(noPp.State.Slots[2].Body!.ChargeTurnsRemaining==1&&noPp.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-pp"),"zero PP cannot refresh Charge");
});
Test("ordinary switch and Baton Pass drop Stockpile and Charge through existing ownership",()=>{
 foreach(bool baton in new[]{false,true}){
  var actor=Mon("SNORLAX",100,baton?"BATON_PASS":"DEFENSE_CURL") with{SpeedStage=12,ChargeTurnsRemaining=2,CalledVolatile=new(){StockpileCount=3,SubstituteHp=10}};
  var f=Frame([actor,Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL")],1);
  var selections=Choices(f);if(!baton)selections=selections.SetItem(0,new(f.Slots[0].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)));
  var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,selections));int steps=0;
  while(r.State.FourTurn is not null){Check(++steps<40,"switch progress");if(r.State.FourTurn.Phase==SourceFourActionPhase.ActionSwitchPending)r=Ok(SourceFourActionTurns.ChooseActionSwitch(catalog,r.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2)));else r=Next(r);}
  var mon=r.State.Slots[0].Body!;Check(r.State.Slots[0].Member!.Value.Index==2&&mon.ChargeTurnsRemaining==0&&(mon.CalledVolatile?.StockpileCount??0)==0,"storage dropped");
  Check((mon.CalledVolatile?.SubstituteHp??0)==(baton?10:0),"existing Baton subset preserved");
 }
});
Test("Metronome and Sleep Talk use the same four-slot recipes and retain caller PP",()=>{
 foreach(string name in new[]{"STOCKPILE","SPIT_UP","SWALLOW","CHARGE"})foreach(bool asleep in new[]{false,true})foreach(bool calledEmpty in new[]{false,true}){
  var actor=Mon("SNORLAX",30,asleep?"SLEEP_TALK":"METRONOME",name) with{CalledVolatile=new(){StockpileCount=1},Status=asleep?3u:0};
  actor=actor with{Pokemon=actor.Pokemon with{CurrentHp=1,Moves=actor.Pokemon.Moves.SetItem(1,actor.Pokemon.Moves[1] with{CurrentPp=calledEmpty?0:1})}};
  uint seed=31;if(!asleep){seed=Enumerable.Range(0,100000).Select(i=>(uint)i).First(i=>WildBattleSourceCallers.Metronome(new(i)).SourceMoveId==catalog.Moves[Id(name)].SourceMoveId);}
  var f=Frame([Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),actor,Mon("SNORLAX",100,"DEFENSE_CURL")],seed:seed);
  var r=Act(f,2,2);var mon=r.State.Slots[2].Body!;
  Check(r.Events.OfType<SourceCallerSelectionEvent>().Any(e=>e.Result==Id(name)),name+" actual source selection");
  Check(mon.Pokemon.Moves[0].CurrentPp==actor.Pokemon.Moves[0].CurrentPp-1&&mon.Pokemon.Moves[1].CurrentPp==(calledEmpty?0:1),"only caller PP spent");
  if(asleep&&calledEmpty){Check(mon.CalledVolatile?.StockpileCount==1&&mon.ChargeTurnsRemaining==0&&mon.Pokemon.CurrentHp==1&&r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-pp"),"Sleep Talk selects zero-PP then original attackcanceler rejects it");continue;}
  Check(name switch{"STOCKPILE"=>mon.CalledVolatile?.StockpileCount==2,"SPIT_UP"=>(mon.CalledVolatile?.StockpileCount??0)==0,"SWALLOW"=>mon.Pokemon.CurrentHp>1&&mon.CalledVolatile is null,_=>mon.ChargeTurnsRemaining==2},name+" shared called effect");
 }
});
Test("invalid counters fail atomically before action costs or RNG",()=>{
 foreach(int bad in new[]{-1,4}){var f=Frame();f=Put(f,0,f.Slots[0].Body! with{CalledVolatile=new(){StockpileCount=bad}});Rejected(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)),"stockpile range");}
 foreach(int bad in new[]{-1,3}){var f=Frame();f=Put(f,0,f.Slots[0].Body! with{ChargeTurnsRemaining=bad});Rejected(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)),"Charge range");}
});

File.WriteAllText(args[1]+".singles-after.json",Json(new{baseline="a7d1936f9e5f446a36209599dc909749ec96eec0",snapshots})+"\n");
File.WriteAllText(args[1],Json(new{runtime=Environment.Version.ToString(),groups,checks,failures,results})+"\n");return failures==0?0:1;
