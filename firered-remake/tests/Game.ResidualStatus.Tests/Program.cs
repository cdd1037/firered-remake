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
SourceBattleFrame Seed(SourceBattleFrame f,int target,int? actor){var c=f.Slots[target];return f with{Slots=f.Slots.SetItem(target,c with{Registers=c.Registers with{LeechSeedSource=actor is {} a?new(a):null}})};}
SourceBattleFrame Hp(SourceBattleFrame f,int slot,int hp)=>Put(f,slot,f.Slots[slot].Body! with{Pokemon=f.Slots[slot].Body!.Pokemon with{CurrentHp=hp}});
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int slot=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,slot,new(target)));Check(r.Succeeded,r.Fault??"action");Check(SourceBattleIdentityRules.Validate(r.State)==null,"action identity");return r;}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"turn");Check(SourceBattleIdentityRules.Validate(r.State)==null,"turn identity");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? slots=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int pos=slots?[c.Slot.Value]??0;var m=catalog.Moves[c.Body!.Pokemon.Moves[pos].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,new(m.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:targets?[c.Slot.Value]??(c.Slot.Value^1)));}).ToImmutableArray();
SourceFourActionResult Begin(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? choices=null)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices??Choices(f)));
SourceTurnContinuation Token(SourceBattleFrame f)=>new(f.Revision,f.EventSequence,f.NextAction,f.FourTurn!.Phase);
SourceFourActionResult Replay(SourceFourActionResult r,Func<SourceBattleFrame,SourceFourActionResult> step){var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var a=step(r.State);var b=step(restored);Check(Json(a)==Json(b),"serialized exact state/events/RNG continuation");return Good(a);}
SourceFourActionResult Next(SourceFourActionResult r)=>Replay(r,f=>SourceFourActionTurns.Advance(catalog,f,Token(f)));
SourceFourActionResult EndBoundary(SourceBattleFrame f){var r=Begin(f);while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn.Phase==SourceFourActionPhase.EndTurn,"actual endturn boundary");return r;}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var e=r.Events.ToList();for(int i=0;i<30&&r.State.FourTurn!=null&&r.State.Outcome==WildBattleOutcome.Ongoing;i++){
 r=r.State.FourTurn.Phase switch{
  SourceFourActionPhase.RewardsPending=>Replay(r,f=>SourceFourRewards.Settle(catalog,f,Token(f))),
  SourceFourActionPhase.ReplacementsPending=>Replay(r,f=>{var p=f.PendingSwitches[0];return SourceFourActionTurns.ChooseReplacement(catalog,f,Token(f),p.Slot,new(p.Slot.Side,p.LegalPartyIndices[0]));}),
  _=>Next(r)};e.AddRange(r.Events);
 }Check(r.CompletedCleanTurn,"one complete turn");turns++;return(r.State,e);}
SourceActionSelection Switch(SourceBattleFrame f,int slot,int incoming)=>new(f.Slots[slot].Occupant!.Value,SourceActionKind.Switch,Incoming:new((BattleSide)(slot&1),incoming));
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic "+why+": "+r.Fault);

Test("31 frozen singles fingerprints and bounded admission",()=>{
 Check(Environment.Version.Major==10,"runtime10");Check(source.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"source pin");
 Check(LegacyParity.Run(args[0],true)==File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../..","legacy-baseline.json")).Trim(),"full singles state/events/RNG fingerprints unchanged");
 foreach(var m in new[]{"LEECH_SEED","INGRAIN"})Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(m)]),m+" admitted");
 foreach(var m in new[]{"NIGHTMARE","CURSE","YAWN","UPROAR","METRONOME","TRANSFORM"})Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(m)]),m+" stays gated");
});
Test("verbatim C seed/root application vectors match actual selected slot commands",()=>{
 foreach(var v in source.RootElement.GetProperty("groups").GetProperty("seedApplication").GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();
  if(x[5]!=0 || (x[6]&4)!=0&&x[2]==(x[6]&3))continue;
  var f=Frame();f=Put(f,x[1],Mon("PIDGEY","LEECH_SEED")with{AccuracyStage=12});var b=Mon(x[3]==12||x[4]==12?"BULBASAUR":"PIDGEY");
  f=Put(f,x[2],b with{Rooted=(x[6]&1024)!=0});
  if((x[6]&4)!=0)f=Seed(f,x[2],x[6]&3);var a=Act(f,x[1],x[2]);var c=a.State.Slots[x[2]];
  int actual=(c.Body!.Rooted?1024:0)+(c.Registers.LeechSeedSource is {} seed?4|seed.Value:0);
  Check(actual==y[0],"source setseeded state");Check(a.Trace.Length==1,"accuracy draw before Grass/reapplication");
  if(y[2]!=0)Check(a.Events.OfType<SourceMoveFailedEvent>().Single().Reason==(y[2]==1?"already-seeded":"grass-type-immunity"),"source failure ordering");oracleCases++;
 }
 foreach(var v in source.RootElement.GetProperty("groups").GetProperty("ingrainApplication").GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();
  // Independent unrelated status3 bits are outside this family; root flag is unchanged C input.
  var f=Put(Frame(),x[1],Mon("PIDGEY","INGRAIN")with{Rooted=(x[2]&1024)!=0});var a=Act(f,x[1],x[1]);
  Check(a.State.Slots[x[1]].Body!.Rooted==((y[0]&1024)!=0),"root set");Check(a.Trace.IsEmpty&&a.Events.OfType<SourceMoveFailedEvent>().Count()==y[1],"source duplicate and no RNG");oracleCases++;
 }
});
Test("unchanged C residual arithmetic, source positions, absence and clipped double faint",()=>{
 var prepared=EndBoundary(Frame(players:[Mon("PIDGEY")],opponents:[Mon("RATTATA")])).State;
 var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.FrameEndTurnContext")!;
 var baseType=type.BaseType!;
 (SourceBattleFrame State,SourceBattleEvent[] Events) Command(SourceBattleFrame f,int slot,string method){
  object c=Activator.CreateInstance(type,flags|BindingFlags.Public,null,[f],null)!;
  if(!f.IsSourceAbsent(new(slot)))baseType.GetMethod(method,flags)!.Invoke(c,[new BattlerSlot(slot)]);
  return((SourceBattleFrame)type.GetProperty("Frame",flags)!.GetValue(c)!,((ImmutableArray<SourceBattleEvent>.Builder)type.GetField("Events",flags)!.GetValue(c)!).ToArray());
 }
 foreach(var name in new[]{"leechResidual","leechResidualSlots"})foreach(var v in source.RootElement.GetProperty("groups").GetProperty(name).GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();
  if(x[10]!=0||x[11]!=0)continue; // Substitute remains gated in four-slot admission.
  var f=prepared;int d=x[1],r=x[2];var donor=Mon(x[7]!=0?"GULPIN":"PIDGEY");var receiver=Mon("PIDGEY");Check(x[7]==0||donor.Pokemon.AbilityId=="ABILITY_LIQUID_OOZE","real Liquid Ooze fixture");
  donor=donor with{Pokemon=donor.Pokemon with{Stats=donor.Pokemon.Stats with{MaxHp=x[3]},CurrentHp=x[4]}};
  receiver=receiver with{Pokemon=receiver.Pokemon with{Stats=receiver.Pokemon.Stats with{MaxHp=x[5]},CurrentHp=x[6]}};
  f=Seed(Put(Put(f,d,donor),r,receiver),d,x[9]!=0?r:null) with{SourceAbsentMask=(byte)x[8]};var a=Command(f,d,"LeechSeedResidual");
  Check(a.State.Slots[d].Body!.Pokemon.CurrentHp==y[5]&&a.State.Slots[r].Body!.Pokemon.CurrentHp==y[6],"C donor/receiver HP "+name+" "+Json(x));
  Check(a.Events.OfType<SourceLeechSeedDrainEvent>().Count()==y[0],"C effect eligibility");
  if(y[0]!=0){var drain=a.Events.OfType<SourceLeechSeedDrainEvent>().Single();Check(drain.Damage==y[3]&&drain.ReceivingSide.Value==r,"C gHpDealt and actual receiver");
   var expected=new[]{d,r}.Where(s=>a.State.Slots[s].Body!.Pokemon.CurrentHp==0&&!f.IsSourceAbsent(new(s)));Check(a.Events.OfType<SourceFaintedEvent>().Select(e=>e.Side.Value).SequenceEqual(expected),"source donor then receiver tryfaint absence guard");}
  oracleCases++;
 }
 foreach(var v in source.RootElement.GetProperty("groups").GetProperty("ingrainResidual").GetProperty("rows").EnumerateArray()){
  var x=v.GetProperty("input").EnumerateArray().Select(z=>z.GetInt32()).ToArray();var y=v.GetProperty("output").EnumerateArray().Select(z=>z.GetInt32()).ToArray();if(x[6]!=0)continue;
  var f=prepared;var b=f.Slots[x[1]].Body!;f=Put(f,x[1],b with{Rooted=x[4]!=0,Pokemon=b.Pokemon with{Stats=b.Pokemon.Stats with{MaxHp=x[2]},CurrentHp=x[3]}})with{SourceAbsentMask=(byte)x[5]};var a=Command(f,x[1],"IngrainResidual");
  Check(a.State.Slots[x[1]].Body!.Pokemon.CurrentHp==y[2]&&a.Events.OfType<SourceIngrainHealEvent>().Count()==y[0],"C Ingrain clamped heal and eligibility");oracleCases++;
 }
});
Test("actual ally Pressure, Safeguard, Protect and reapplication-before-Grass order",()=>{
 var f=Frame([Mon("PIDGEY","LEECH_SEED")with{AccuracyStage=12},Mon("SNORLAX"),Mon("DUSCLOPS"),Mon("SNORLAX")]);var a=Act(f,0,2);
 Check(a.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==2&&a.State.Slots[2].Registers.LeechSeedSource?.Value==0,"ally Pressure actual receiver");
 f=Seed(Put(f,2,Mon("BULBASAUR")),2,0);a=Act(f,0,2);Check(a.Trace.Length==1&&a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="already-seeded","duplicate before Grass after accuracy");
 f=Put(Frame(),0,Mon("PIDGEY","LEECH_SEED")with{AccuracyStage=12});f=f with{Sides=f.Sides.SetItem(1,f.Sides[1]with{SafeguardTurnsRemaining=4})};a=Act(f,0,3);Check(a.State.Slots[3].Registers.LeechSeedSource?.Value==0,"Safeguard does not stop Seed");
 f=Put(f,3,f.Slots[3].Body!with{Protected=true});a=Act(f,0,3);Check(a.Trace.IsEmpty&&a.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="protected","Protect before random draw");
});
Test("Leech Seed source accuracy honors temporary and permanent Sand Veil",()=>{
 bool hit=false,miss=false;
 for(uint seed=0;seed<16;seed++){
  var f=Frame([Mon("PIDGEY","LEECH_SEED"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SANDSHREW")],seed:seed);
  var timed=Act(f with{Field=new(){SandstormTurnsRemaining=3}},0,3);
  var permanent=Act(f with{Field=new(){PermanentWeather=new(SourcePermanentWeatherKind.Sandstorm)}},0,3);
  bool expected=timed.Trace.Single().Raw%100+1<=72;
  Check((timed.State.Slots[3].Registers.LeechSeedSource is not null)==expected&&(permanent.State.Slots[3].Registers.LeechSeedSource is not null)==expected,"source 90 percent times Sand Veil 80 percent");
  Check(Json(timed.Trace)==Json(permanent.Trace),"same source RNG for either weather lifetime");hit|=expected;miss|=!expected;
 }Check(hit&&miss,"both accuracy branches");
});
Test("two actual complete turns preserve independent same-species seeds and source residual order",()=>{
 var f=Frame([Mon("PIDGEY","LEECH_SEED,DEFENSE_CURL")with{AccuracyStage=12},Mon("SNORLAX","INGRAIN,DEFENSE_CURL"),Mon("PIDGEY","LEECH_SEED,DEFENSE_CURL")with{AccuracyStage=12},Mon("SNORLAX","INGRAIN,DEFENSE_CURL")]);
 f=Hp(Hp(f,1,100),3,100);var first=Finish(Begin(f));
 Check(first.State.Slots[1].Registers.LeechSeedSource?.Value==0&&first.State.Slots[3].Registers.LeechSeedSource?.Value==2,"distinct actual positions");
 foreach(int i in new[]{1,3})Check(first.Events.OfType<SourceIngrainHealEvent>().Single(e=>e.Side.Value==i).Sequence<first.Events.OfType<SourceLeechSeedDrainEvent>().Single(e=>e.DrainedSide.Value==i).Sequence,"Ingrain before Seed");
 var second=Finish(Begin(first.State,Choices(first.State,[1,1,1,1])));Check(second.Events.OfType<SourceLeechSeedDrainEvent>().Count()==2&&second.Events.OfType<SourceIngrainHealEvent>().Count()==2,"one residual per position on following turn");
});
Test("source voluntary departure preserves seeds and heals incoming occupant immediately",()=>{
 var f=Frame([Mon("PIDGEY","LEECH_SEED,DEFENSE_CURL")with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")],[Mon("RATTATA")]);f=Act(f,0,3).State;
 var p=f.Party(BattleSide.Player);f=f with{Parties=f.Parties.SetItem(0,p with{Members=p.Members.SetItem(2,p.Members[2]with{Pokemon=p.Members[2].Pokemon with{CurrentHp=1}})})};
 var r=Begin(f,Choices(f,[1,0,0,0]).SetItem(0,Switch(f,0,2)));r=Next(r);Check(r.State.Slots[0].EntrySerial==2&&r.State.Slots[3].Registers.LeechSeedSource?.Value==0,"immediate source switch keeps position link");var done=Finish(r);
 Check(done.Events.OfType<SourceLeechSeedDrainEvent>().Single().ReceiverHpBefore==1&&done.State.Slots[0].Body!.Pokemon.CurrentHp>1,"incoming receives transfer same turn");
});
Test("target voluntary switch and Roar clear seeds; rooted blocks both with unchanged costs policy",()=>{
 var f=Frame([Mon("PIDGEY","LEECH_SEED,ROAR,DEFENSE_CURL",70)with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")],opponents:[Mon("RATTATA")]);f=Act(f,0,3).State;
 var r=Begin(f,Choices(f,[2,0,0,0]).SetItem(3,Switch(f,3,2)));r=Next(r);Check(r.State.Slots[3].EntrySerial==2&&r.State.Slots[3].Registers.LeechSeedSource is null,"voluntary target immediate clear");Finish(r);
 var forced=Finish(Begin(f,Choices(f,[1,0,0,0],[3,0,3,2])));Check(forced.State.Slots[3].EntrySerial==2&&forced.State.Slots[3].Registers.LeechSeedSource is null,"Roar immediate target clear");Check(!forced.Events.OfType<SourceLeechSeedDrainEvent>().Any(),"no inherited residual");
 f=Put(f,3,f.Slots[3].Body!with{Rooted=true});Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,[2,0,0,0]).SetItem(3,Switch(f,3,2))),"rooted voluntary switch");
 var blocked=Finish(Begin(f,Choices(f,[1,0,0,0],[3,0,3,2])));Check(blocked.State.Slots[3].EntrySerial==1&&blocked.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="rooted"),"Roar fails at rooted source command");
});
Test("Baton Pass transfers rooted target and original source slot",()=>{
 var f=Frame([Mon("PIDGEY","LEECH_SEED,DEFENSE_CURL")with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX","INGRAIN,BATON_PASS")],opponents:[Mon("RATTATA")]);f=Act(f,0,3).State;f=Act(f,3,3).State;
 var r=Begin(f,Choices(f,[1,0,0,1]));while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn.Phase==SourceFourActionPhase.ActionSwitchPending,"rooted Baton allowed");
 r=Replay(r,x=>SourceFourActionTurns.ChooseActionSwitch(catalog,x,Token(x),new(3),new(BattleSide.Opponent,2)));
 Check(r.State.Slots[3].Body!.Rooted&&r.State.Slots[3].Registers.LeechSeedSource?.Value==0&&r.State.Slots[3].EntrySerial==2,"target roots and original source slot transferred");Finish(r);
});
Test("Liquid Ooze double KO pauses once for actual reward then both replacements and resumes without replay",()=>{
 var f=Frame([Mon("PIDGEY","LEECH_SEED,DEFENSE_CURL")with{AccuracyStage=12},Mon("SNORLAX"),Mon("SPEAROW"),Mon("GULPIN")],[Mon("RATTATA")],[Mon("PIDGEY")]);f=Act(f,0,3).State;f=Put(f,0,Mon("PIDGEY"));f=Hp(Hp(f,0,1),3,1);
 var r=Next(EndBoundary(f));Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending,"reward boundary");Check(r.State.FourTurn.EndTurn!.Stage==WildBattleSourceEndTurnStage.FutureAttacks,"normal residual cursor frozen");
 Check(r.Events.OfType<SourceFaintedEvent>().Select(e=>e.Side.Value).SequenceEqual(new[]{3,0}),"donor then receiver double faint");Check(r.State.DeferredKoVacancyMask==9&&r.State.PendingSwitches.IsEmpty&&r.State.Turn==0,"both KO vacancies deferred until rewards");
 Check(r.State.Slots[3].Registers.LeechSeedSource is null&&r.Events.OfType<SourceLeechSeedDrainEvent>().Single().Damage==1,"clipped transfer and target cleanup");
 var done=Finish(r);Check(done.State.Slots[0].EntrySerial==2&&done.State.Slots[3].EntrySerial==2&&done.State.Turn==1,"both actual reserve entries and one Ready");
 Check(done.Events.OfType<SourceOpponentRewardCompletedEvent>().Count()==1&&done.Events.OfType<SourceLeechSeedDrainEvent>().Count()==1,"one actual reward and no repeated residual");
 Check(done.Events.OfType<SourceRewardAppliedEvent>().Any()&&done.State.Rewards!.CompletedOpponentMask==2,"actual EXP credit for donor identity");
});
Test("source faint retains outgoing seeds until its replacement receives next turn",()=>{
 var f=Frame([Mon("PIDGEY"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")],[Mon("RATTATA")]);f=Seed(f,3,0);f=Hp(f,0,1);f=Put(f,0,f.Slots[0].Body!with{Status=WildBattleStatus.Poison});
 var r=Next(EndBoundary(f));Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending,"source residual KO deferred");Check(r.State.Slots[3].Registers.LeechSeedSource?.Value==0,"outgoing source position survives faint");Check(!r.Events.OfType<SourceLeechSeedDrainEvent>().Any(),"later donor skips fainted source");
 var done=Finish(r);var next=Finish(Begin(done.State));Check(next.Events.OfType<SourceLeechSeedDrainEvent>().Single().ReceivingSide.Value==0,"next turn current replacement receives");
});
Test("source-absent live receiver loses Ooze HP without absent faint callback",()=>{
 var f=Frame([Mon("PIDGEY"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("GULPIN")]);f=Seed(f,3,0);f=Hp(f,0,1)with{SourceAbsentMask=1,TurnStartAbsentMask=1};var done=Finish(Begin(f));
 Check(done.State.Slots[0].Body!.Pokemon.CurrentHp==0&&done.Events.OfType<SourceLeechSeedDrainEvent>().Count()==1,"absent receiver HP-only transfer");Check(!done.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==0),"tryfaint source absence suppresses callback");
});
Test("malformed and deferred residual families reject atomically before RNG and PP",()=>{
 var f=Frame();foreach(var bad in new[]{Seed(f,0,0),Put(f,0,f.Slots[0].Body!with{Nightmare=true,Status=3}),Put(f,0,f.Slots[0].Body!with{Cursed=true}),Put(f,0,f.Slots[0].Body!with{YawnTurnsRemaining=2})})
  Atomic(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"unsupported/invalid residual state");
});
Check(groups>0&&checks>0&&oracleCases>0,"nonempty suite");var result=new{groups,checks,failures,completedTurns=turns,oracleCases,runtime=Environment.Version.ToString(),results};File.WriteAllText(args[2],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true})+"\n");using var parsed=JsonDocument.Parse(File.ReadAllText(args[2]));Console.WriteLine(Json(result));return failures==0?0:1;
