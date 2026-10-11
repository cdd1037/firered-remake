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
 var m=supplied??[Mon("DROWZEE",50,"MEDITATE"),Mon("SLOWPOKE",50,"AMNESIA"),Mon("POOCHYENA",50,"HOWL"),Mon("RATTATA",50,"SCARY_FACE")];
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),Parties=[new(BattleSide.Player,new[]{Member(m[0]),Member(m[2])}.Concat(Enumerable.Range(0,reserves).Select(i=>Member(Mon("PIDGEY",50,"TACKLE",(uint)i)))).ToImmutableArray()),new(BattleSide.Opponent,new[]{Member(m[1]),Member(m[3])}.Concat(Enumerable.Range(0,reserves).Select(i=>Member(Mon("RATTATA",50,"TAIL_WHIP",(uint)i)))).ToImmutableArray())],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};
}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(targets?[c.Slot.Value]??(c.Slot.Value==3?0:c.Slot.Value^1)))).ToImmutableArray();
SourceFourActionResult Success(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Begin(SourceBattleFrame f,int[]? targets=null)=>Success(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,targets)));
SourceFourActionResult Next(SourceFourActionResult r)=>Success(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
void Rejected(SourceBattleFrame f,SourceFourActionResult r,string label){Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,label+" atomic fault: "+r.Fault);}
SourceBattleFrame RebindBody(SourceBattleFrame f,int slot,WildBattleCombatant body){var c=f.Slots[slot];var key=c.Member!.Value;var party=f.Parties[(int)key.Side];return f with{Slots=f.Slots.SetItem(slot,c with{Body=body}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(body))})};}
SourceFourActionResult EndBoundary(SourceBattleFrame f){var r=Begin(f);while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn.Phase==SourceFourActionPhase.EndTurn,"reached actual four action boundary");return r;}
void RoundTrip(SourceFourActionResult r){var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;Check(SourceBattleIdentityRules.Validate(restored) is null,"restored frame validates");var a=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var b=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);Check(Json(a)==Json(b),"exact state event RNG and fault after JSON resume");}
Test("four actual actions then poison burn and Toxic in retained order",()=>{
 var f=Frame();f=RebindBody(f,0,f.Slots[0].Body! with{Status=WildBattleStatus.Poison});f=RebindBody(f,1,f.Slots[1].Body! with{Status=WildBattleStatus.Burn});f=RebindBody(f,2,f.Slots[2].Body! with{Status=WildBattleSourceFieldEffects.ToxicPoison});
 var r=EndBoundary(f);RoundTrip(r);r=Next(r);Check(r.CompletedCleanTurn,"status turn reaches Ready");cleanTurns++;
 var damage=r.Events.OfType<SourceStatusDamageEvent>().ToArray();Check(damage.Length==3,"three actual residual commands");var order=r.Events.OfType<SourceOrderEvent>().Single().Order;Check(damage.Select(x=>x.Side).SequenceEqual(order.Where(x=>x.Value<3)),"fixed battler order");
 foreach(var e in damage){var max=f.Get(e.Side).Body!.Pokemon.Stats.MaxHp;Check(e.Damage==Math.Max(1,max/(e.Side.Value==2?16:8)),"source integer floor");Check(r.State.Get(e.Side).Body!.Pokemon.CurrentHp==e.HpAfter,"slot mutation");Check(r.State.Party(e.Side.Side).Members[r.State.Get(e.Side).Member!.Value.Index].Status==(e.Side.Value==2?WildBattleSourceFieldEffects.ToxicPoison:r.State.Get(e.Side).Body!.Status),"persistent status excludes battle-only Toxic counter");}
 Check(r.State.Slots[2].Body!.Status==(WildBattleSourceFieldEffects.ToxicPoison|256u),"Toxic counter increments");
});
Test("temporary damaging weather precedes all four battler poison commands",()=>{
 foreach(var weather in new[]{"sandstorm","hail"}){
  var f=Frame();foreach(var c in f.Slots)f=RebindBody(f,c.Slot.Value,c.Body! with{Status=WildBattleStatus.Poison});
  f=f with{Field=weather=="sandstorm"?new(){SandstormTurnsRemaining=2}:new(){HailTurnsRemaining=2}};var r=Next(EndBoundary(f));Check(r.CompletedCleanTurn,"weather and poison turn complete");cleanTurns++;
  var w=r.Events.OfType<SourceWeatherDamageEvent>().ToArray();var p=r.Events.OfType<SourceStatusDamageEvent>().ToArray();Check(w.Length==4&&p.Length==4&&w.Max(x=>x.Sequence)<p.Min(x=>x.Sequence),"field before battlers across all four slots");Check(w.Select(x=>x.Side).SequenceEqual(r.Events.OfType<SourceOrderEvent>().Single().Order),"weather retained order");
  foreach(var e in w)Check(e.Damage==Math.Max(1,f.Get(e.Side).Body!.Pokemon.Stats.MaxHp/16),"weather exact floor");
 }
});
Test("weather expires before damage and Rain Dish, while suppression still ticks timer",()=>{
 foreach(var weather in new[]{"rain","sandstorm","sun","hail"}){
  var f=Frame();f=f with{Field=weather switch{"rain"=>new(){RainTurnsRemaining=1},"sandstorm"=>new(){SandstormTurnsRemaining=1},"sun"=>new(){SunTurnsRemaining=1},_=>new(){HailTurnsRemaining=1}}};var r=Next(EndBoundary(f));Check(r.CompletedCleanTurn&&!r.Events.OfType<SourceWeatherDamageEvent>().Any(),"expiry suppresses damage");Check(r.Events.OfType<SourceMtMoonVolatileEvent>().Single().After==0,"expiry event");cleanTurns++;
 }
 var dish=Mon("LOTAD",50,"ABSORB",1);Check(dish.Pokemon.AbilityId=="ABILITY_RAIN_DISH","real Lotad ability slot");dish=dish with{Pokemon=dish.Pokemon with{CurrentHp=dish.Pokemon.Stats.MaxHp-20}};
 foreach(int turns in new[]{1,2}){var f=RebindBody(Frame(),2,dish) with{Field=new(){RainTurnsRemaining=turns}};var r=Next(EndBoundary(f));Check(r.Events.OfType<SourceAbilityRecoveredEvent>().Count()==(turns==2?1:0),"Rain Dish observes ticked weather");cleanTurns++;}
});
Test("Rain Dish precedes poison on same real holder; Speed Boost occurs at ability phase",()=>{
 var dish=Mon("LOTAD",50,"ABSORB",1);dish=dish with{Status=WildBattleStatus.Poison,Pokemon=dish.Pokemon with{CurrentHp=dish.Pokemon.Stats.MaxHp-20}};
 var boost=Mon("NINJASK",50,"SCRATCH");Check(boost.Pokemon.AbilityId=="ABILITY_SPEED_BOOST","real Ninjask ability");var f=RebindBody(RebindBody(Frame(),0,dish),2,boost) with{Field=new(){RainTurnsRemaining=2}};
 var r=Next(EndBoundary(f));Check(r.CompletedCleanTurn,"ability turn complete");cleanTurns++;Check(r.Events.OfType<SourceAbilityRecoveredEvent>().Single().Sequence<r.Events.OfType<SourceStatusDamageEvent>().Single().Sequence,"heal before poison");Check(r.State.Slots[2].Body!.SpeedStage==7,"Speed Boost plus one");
 foreach(int counter in new[]{0,1,2}){f=RebindBody(Frame(),2,boost with{FirstTurnCounter=counter});r=Next(EndBoundary(f));Check(r.State.Slots[2].Body!.SpeedStage==(counter==2?6:7),"first-turn counter gate");cleanTurns++;}
 f=RebindBody(Frame(),2,boost with{SpeedStage=12});r=Next(EndBoundary(f));Check(!r.Events.OfType<SourceAbilityActivatedEvent>().Any(),"capped Speed Boost does not reveal");cleanTurns++;
});
Test("Shed Skin consumes one modulo draw at ability phase before status residual",()=>{
 var mon=Mon("DRATINI",50,"SLAM");Check(mon.Pokemon.AbilityId=="ABILITY_SHED_SKIN","real Shed Skin");mon=mon with{Status=WildBattleStatus.Poison};bool cured=false,kept=false;
 for(uint seed=0;seed<12;seed++){var f=RebindBody(Frame(),2,mon) with{Rng=new(seed)};var r=EndBoundary(f);RoundTrip(r);r=Next(r);var draw=r.Trace.Single(t=>t.Purpose=="source-battle.slot-2.shed-skin");bool cure=draw.Raw%3==0;Check((r.State.Slots[2].Body!.Status==0)==cure,"source modulo decision");Check(r.Events.OfType<SourceStatusDamageEvent>().Count()==(cure?0:1),"cure before residual");cured|=cure;kept|=!cure;cleanTurns++;}Check(cured&&kept,"both Shed Skin branches covered");
});
Test("actual poison and burn move effects continue into the shared residual scheduler",()=>{
 int poisoned=0,burned=0;
 for(uint seed=0;seed<128 && (poisoned==0||burned==0);seed++){
  if(poisoned==0){var f=RebindBody(Frame(),0,Mon("VENONAT",30,"POISON_POWDER")) with{Rng=new(seed)};var r=EndBoundary(f);if(r.State.Slots[1].Body!.Status==WildBattleStatus.Poison){var hp=r.State.Slots[1].Body!.Pokemon.CurrentHp;r=Next(r);Check(r.CompletedCleanTurn&&r.Events.OfType<SourceStatusDamageEvent>().Single().Status==WildBattleStatus.Poison,"same-turn poison residual");Check(r.State.Slots[1].Body!.Pokemon.CurrentHp<hp,"poison after actual chosen move");poisoned++;cleanTurns++;}}
  if(burned==0){var f=RebindBody(Frame(),0,Mon("CHARMANDER",10,"EMBER")) with{Rng=new(seed)};var r=EndBoundary(f);if(r.State.Slots[1].Body!.Status==WildBattleStatus.Burn){var hp=r.State.Slots[1].Body!.Pokemon.CurrentHp;r=Next(r);Check(r.CompletedCleanTurn&&r.Events.OfType<SourceStatusDamageEvent>().Single().Status==WildBattleStatus.Burn,"same-turn burn residual");Check(r.State.Slots[1].Body!.Pokemon.CurrentHp<hp,"burn after actual chosen move");burned++;cleanTurns++;}}
 }Check(poisoned==1&&burned==1,"both admitted status-creating recipes completed");
});
Test("source absence and living HP are distinct weather queries",()=>{
 var cloud=Mon("PSYDUCK",50,"SCRATCH",1);Check(cloud.Pokemon.AbilityId=="ABILITY_CLOUD_NINE","actual Cloud Nine species/ability slot");
 foreach(bool living in new[]{true,false}){var f=RebindBody(Frame(),3,cloud with{Pokemon=cloud.Pokemon with{CurrentHp=living?cloud.Pokemon.Stats.MaxHp:0}}) with{SourceAbsentMask=8,TurnStartAbsentMask=8,Field=new(){SandstormTurnsRemaining=3}};var r=Next(EndBoundary(f));Check(r.Events.OfType<SourceWeatherDamageEvent>().Count()==(living?0:3),"absence does not suppress CHECK_ON_FIELD, fainted HP does");Check(!r.Events.OfType<SourceWeatherDamageEvent>().Any(e=>e.Side.Value==3),"absent target never weather damaged");Check(r.State.Field.SandstormTurnsRemaining==2,"suppressed weather still expires");cleanTurns++;}
});
Test("residual player KO preserves damage registers until delayed boundary and requests real reserve",()=>{
 var f=Frame(reserves:1);f=RebindBody(f,0,f.Slots[0].Body! with{Status=WildBattleStatus.Poison,Pokemon=f.Slots[0].Body!.Pokemon with{CurrentHp=1}});var r=EndBoundary(f);var c=r.State.Get(new(0));var damage=new SourceDamageRegisters(WildBattleRules.CalledMoveKernelRulesVersion,0,3,new(1),0,null);r=r with{State=r.State with{Slots=r.State.Slots.SetItem(0,c with{Registers=c.Registers with{Damage=damage}})}};
 r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending&&r.State.FourTurn.EndTurn!.Stage==WildBattleSourceEndTurnStage.FutureAttacks,"faint boundary before delayed");Check(r.State.Turn==0&&r.State.PendingSwitches.Single().Slot.Value==0,"no early Ready or incoming actor");Check(r.State.Slots[0].Registers.Damage is not null,"damage registers retained until final delayed cleanup");Check(r.State.FourTurn.Defeats.Length==1&&r.State.DeferredKoVacancyMask==1,"real defeat ledger and vacancy");Check(r.State.Party(BattleSide.Player).Members[0].Status==0&&r.State.Party(BattleSide.Player).Members[0].Pokemon.CurrentHp==0,"permanent faint cleanup");RoundTrip(r);Rejected(r.State,SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value),"entry continuation explicit");
});
Test("opponent residual KO waits for EXP; remaining normal residuals still finish",()=>{
 var f=Frame(reserves:1);f=RebindBody(f,1,f.Slots[1].Body! with{Status=WildBattleStatus.Poison,Pokemon=f.Slots[1].Body!.Pokemon with{CurrentHp=1}});f=RebindBody(f,2,f.Slots[2].Body! with{Status=WildBattleStatus.Burn});var r=Next(EndBoundary(f));Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending,"real reward boundary");Check(r.Events.OfType<SourceStatusDamageEvent>().Count()==2,"remaining normal effects occur before EXP phase");Check(r.State.FourTurn.EndTurn!.Stage==WildBattleSourceEndTurnStage.FutureAttacks&&r.State.PendingSwitches.IsEmpty,"cursor retains exact future boundary, no replacement");Check(!r.Events.OfType<SourceTurnReadyEvent>().Any()&&r.State.Turn==0,"no Ready");RoundTrip(r);
});
Test("weather terminal opponent loss preserves EXP and stops later normal residuals",()=>{
 var f=Frame();foreach(int slot in new[]{1,3})f=RebindBody(f,slot,f.Slots[slot].Body! with{Pokemon=f.Slots[slot].Body!.Pokemon with{CurrentHp=1}});f=f with{Field=new(){SandstormTurnsRemaining=3}};var r=Next(EndBoundary(f));Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&r.State.FourTurn.OutcomeAfterRewards==WildBattleOutcome.Won,"terminal win behind real EXP");Check(r.State.FourTurn.Defeats.Length==2&&r.State.DeferredKoVacancyMask==10,"both real enemy identities");Check(!r.Events.OfType<SourceEndedEvent>().Any()&&!r.Events.OfType<SourceTurnReadyEvent>().Any(),"no premature completion");RoundTrip(r);
});
Test("unmigrated persistent and delayed capabilities reject without spending costs or RNG",()=>{
 var f=Frame();foreach(var bad in new[]{RebindBody(f,0,f.Slots[0].Body! with{Status=WildBattleStatus.Paralysis}),RebindBody(f,0,f.Slots[0].Body! with{HeldItem="ITEM_ORAN_BERRY"}),RebindBody(f,0,f.Slots[0].Body! with{Rooted=true}),f with{Sides=f.Sides.SetItem(0,new(BattleSide.Player){ReflectTurnsRemaining=2})},f with{Field=new(){PermanentWeather=new(SourcePermanentWeatherKind.Rain)}}})Rejected(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"unsupported residual");
 var wish=new WildBattleCalledWish(1,0,f.Slots[0].Body!.Pokemon.Species,42,0);var cell=f.Slots[2];f=f with{Slots=f.Slots.SetItem(2,cell with{Position=cell.Position with{Wish=wish}})};Rejected(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)),"unported position Wish");
});
Test("stale continuations and residual overflow roll back the entire end-turn command",()=>{
 var r=EndBoundary(Frame() with{Field=new(){HailTurnsRemaining=3}});var token=r.Continuation!.Value;var done=Next(r);Rejected(done.State,SourceFourActionTurns.Advance(catalog,done.State,token),"stale turn continuation");
 foreach(var bad in new[]{r.State with{EventSequence=long.MaxValue},r.State with{Revision=long.MaxValue},r.State with{Rng=r.State.Rng with{Cursor=ulong.MaxValue}}}){var t=new SourceTurnContinuation(bad.Revision,bad.EventSequence,bad.NextAction,bad.FourTurn!.Phase);Rejected(bad,SourceFourActionTurns.Advance(catalog,bad,t),"overflow atomic");}
});
Test("malformed residual phase stage order and cursor cannot skip ordinary work",()=>{
 var r=EndBoundary(Frame() with{Field=new(){HailTurnsRemaining=3}});var f=r.State;ImmutableArray<BattlerSlot> order=[new(0),new(1),new(2),new(3)];
 foreach(var stage in Enum.GetValues<WildBattleSourceEndTurnStage>()){
  var forged=f with{FourTurn=f.FourTurn! with{EndTurnOrder=order,EndTurn=new(f.Turn,order,stage,stage==WildBattleSourceEndTurnStage.Complete?4:0,f.Revision,f.EventSequence,WildBattleRules.SourceEndTurnRulesVersion)}};
  Check(SourceBattleIdentityRules.Validate(forged)=="source_frame_four_action_continuation","forged cursor invalid in EndTurn");Rejected(forged,SourceFourActionTurns.Advance(catalog,forged,new(forged.Revision,forged.EventSequence,forged.NextAction,SourceFourActionPhase.EndTurn)),"forged residual cannot bypass weather");
 }
 var competingWeather=f with{Field=new(){RainTurnsRemaining=2,HailTurnsRemaining=3}};Rejected(competingWeather,SourceFourActionTurns.Advance(catalog,competingWeather,r.Continuation!.Value),"competing weather fails at residual boundary");
 var loneOrder=f with{FourTurn=f.FourTurn! with{EndTurnOrder=order}};Check(SourceBattleIdentityRules.Validate(loneOrder) is not null,"order without cursor rejected");
 var ko=Frame(reserves:1);ko=RebindBody(ko,0,ko.Slots[0].Body! with{Status=WildBattleStatus.Poison,Pokemon=ko.Slots[0].Body!.Pokemon with{CurrentHp=1}});var pause=Next(EndBoundary(ko));var saved=pause.State.FourTurn!;
 foreach(var malformed in new[]{saved with{EndTurn=null},saved with{EndTurn=saved.EndTurn! with{Stage=WildBattleSourceEndTurnStage.PerishSong}},saved with{EndTurn=saved.EndTurn! with{NextBattler=1}},saved with{EndTurn=saved.EndTurn! with{MinimumRevision=pause.State.Revision+1}},saved with{EndTurn=saved.EndTurn! with{MinimumEventSequence=pause.State.EventSequence+1}}})Check(SourceBattleIdentityRules.Validate(pause.State with{FourTurn=malformed}) is not null,"inconsistent residual pause rejected");
});
Test("688 independently compiled original C weather function and status case vectors",()=>{
 var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
 var type=typeof(SourceFourActionTurns).Assembly.GetType("Game.Domain.Battle.FrameEndTurnContext")!;
 var baseType=type.BaseType!;var initial=EndBoundary(Frame()).State;
 using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(args[1],"source-residual-vectors.json")));
 foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray()){
  var x=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();var f=initial;var body=f.Get(new(2)).Body!;int mode=x[0];
  body=body with{Pokemon=body.Pokemon with{Stats=body.Pokemon.Stats with{MaxHp=x[2]},CurrentHp=x[3]}};
  if(mode==0){string t=x[4] switch{5=>"TYPE_ROCK",15=>"TYPE_ICE",_=>"TYPE_NORMAL"};body=body with{CalledOverlay=new(){Type1=t,Type2="TYPE_NORMAL"},Pokemon=body.Pokemon with{AbilityId=x[5]==8?"ABILITY_SAND_VEIL":"ABILITY_KEEN_EYE"}};f=RebindBody(f,2,body);var cloud=f.Get(new(3)).Body!;f=RebindBody(f,3,cloud with{Pokemon=cloud.Pokemon with{AbilityId="ABILITY_CLOUD_NINE",CurrentHp=x[6]}});f=f with{SourceAbsentMask=(byte)x[7],Field=x[1]==24?new(){SandstormTurnsRemaining=3}:new(){HailTurnsRemaining=3}};}
  else {body=body with{Status=(uint)x[4]};f=RebindBody(f,2,body);}
  var context=Activator.CreateInstance(type,flags|System.Reflection.BindingFlags.Public,null,[f],null)!;
  if(mode==0)baseType.GetMethod("WeatherDamage",flags)!.Invoke(context,[catalog,new BattlerSlot(2),x[1]==24?"sandstorm":"hail"]);
  else if(x[1]==5)baseType.GetMethod("ToxicDamage",flags)!.Invoke(context,[new BattlerSlot(2)]);
  else baseType.GetMethod("StatusDamage",flags)!.Invoke(context,[new BattlerSlot(2),(uint)x[4]]);
  var result=(SourceBattleFrame)type.GetProperty("Frame",flags)!.GetValue(context)!;var actualEvents=(ImmutableArray<SourceBattleEvent>.Builder)type.GetField("Events",flags)!.GetValue(context)!;
  if(mode==0){var e=actualEvents.OfType<SourceWeatherDamageEvent>().SingleOrDefault();Check((e?.Damage??0)==x[10],"source weather exact damage/absence/HP-query");Check(result.Get(new(2)).Body!.Pokemon.CurrentHp==x[3]-x[10],"weather exact resulting HP");}
  else {var e=actualEvents.OfType<SourceStatusDamageEvent>().SingleOrDefault();Check((e?.Damage??0)==x[11],"source status exact damage");Check(result.Get(new(2)).Body!.Status==(uint)x[10],"source status exact counter");Check((e is null?0:1)==x[12],"source status callback");}
  oracle++;
 }Check(oracle==688,"full nonempty independent oracle");
});
Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"result.json"),Json(new{passed=failures==0,checks,groups,failures,oracle,cleanTurns,runtime=Environment.Version.ToString(),results=summary}));Console.WriteLine($"{checks} checks; {groups} groups; {failures} failures; {oracle} source vectors; {cleanTurns} complete residual turns");return failures==0?0:1;
