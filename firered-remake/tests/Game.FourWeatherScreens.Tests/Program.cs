using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using Game.Domain.Rules;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if (args.Length != 3) throw new ArgumentException("CONTENT_ROOT PINNED_SOURCE EVIDENCE");
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
string tm = File.ReadAllText(Path.Combine(args[1], "src/data/pokemon/tmhm_learnsets.h"));
int checks = 0, groups = 0, failures = 0, turns = 0, vectors = 0;
var summaries = new List<object>();
void Check(bool ok, string label) { checks++; if (!ok) throw new InvalidDataException(label); }
void Test(string label, Action action) { groups++; try { action(); summaries.Add(new { label, passed = true }); Console.WriteLine("PASS " + label); } catch(Exception e) { failures++; summaries.Add(new { label, passed = false, error = e.ToString() }); Console.WriteLine("FAIL " + label + " " + e); } }
MoveId Id(string move) => new("pret:MOVE_" + move);
WildBattleCombatant Mon(string species, string move, uint personality = 0, int level = 50) {
 var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
 string block = Regex.Match(tm, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
 Check(catalog.Species[p.Species].LevelUpMoves.Any(m => m.Move == Id(move) && m.Level <= level) || Regex.IsMatch(block, @"TM\d\d_" + move + @"\)"), "authentic retained move " + species + "/" + move);
 return new(p with { Moves = [new(Id(move), catalog.Moves[Id(move)].Pp, catalog.Moves[Id(move)].Pp)] });
}
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon,m.HeldItem,WildBattleTransformationRules.PersistentStatus(m));
SourceBattleFrame Frame() {
 WildBattleCombatant[] m = [Mon("DROWZEE", "MEDITATE"), Mon("SLOWPOKE", "AMNESIA"), Mon("POOCHYENA", "HOWL"), Mon("RATICATE", "SCARY_FACE")];
 return new() { Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn, SourceMechanicsRulesVersion = SourceBattleKernel.Version,
 BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(31), Parties = [new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],
 Slots = Enumerable.Range(0,4).Select(i => new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray() };
}
SourceBattleFrame Rebind(SourceBattleFrame f, int slot, WildBattleCombatant body) { var c=f.Slots[slot]; var key=c.Member!.Value; var party=f.Parties[(int)key.Side]; return f with { Slots=f.Slots.SetItem(slot,c with { Body=body }),Parties=f.Parties.SetItem((int)key.Side,party with { Members=party.Members.SetItem(key.Index,Member(body)) }) }; }
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f) => f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(c.Slot.Value==1?2:c.Slot.Value==3?0:c.Slot.Value^1))).ToImmutableArray();
SourceFourActionResult Good(SourceFourActionResult r) { Check(r.Succeeded,r.Fault??"accepted"); Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid state "+SourceBattleIdentityRules.Validate(r.State)); return r; }
SourceFourActionResult Next(SourceFourActionResult r) => Good(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
(SourceFourActionResult Result, ImmutableArray<SourceBattleEvent> Events) Actions(SourceBattleFrame f) {
 var r=Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)));var events=r.Events;
 while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions){r=Next(r);events=events.AddRange(r.Events);}
 Check(r.State.FourTurn.Phase==SourceFourActionPhase.EndTurn,"four-action end boundary");return(r,events);
}
(SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events) Turn(SourceBattleFrame f) { var (r,e)=Actions(f); r=Next(r);Check(r.CompletedCleanTurn,"complete real four-slot turn");turns++;return(r.State,e.AddRange(r.Events)); }
int Timer(SourceBattleFrame f, int side, string effect) => effect switch { "reflect"=>f.Sides[side].ReflectTurnsRemaining,"light-screen"=>f.Sides[side].LightScreenTurnsRemaining,"mist"=>f.Sides[side].MistTurnsRemaining,"safeguard"=>f.Sides[side].SafeguardTurnsRemaining,"rain"=>f.Field.RainTurnsRemaining,"sandstorm"=>f.Field.SandstormTurnsRemaining,"sun"=>f.Field.SunTurnsRemaining,_=>f.Field.HailTurnsRemaining };
SourceFieldRegisters Weather(string weather, int count) => weather switch { "rain"=>new(){RainTurnsRemaining=count},"sandstorm"=>new(){SandstormTurnsRemaining=count},"sun"=>new(){SunTurnsRemaining=count},_=>new(){HailTurnsRemaining=count} };
void Atomic(SourceBattleFrame f) { var r=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f));Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic boundary "+r.Fault); }
DamageResult Damage(SourceBattleFrame frame, BattlerSlot actor, BattlerSlot target, bool critical, int percent) {
 var move=catalog.Moves[frame.Get(actor).Body!.Pokemon.Moves[0].Move];
 var method=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.WildBattle.ForestBattleDamage")!.GetMethod("Calculate",BindingFlags.Static|BindingFlags.NonPublic)!;
 var values=method.GetParameters().Select(p=>p.DefaultValue).ToArray();
 var named=new Dictionary<string,object?> { ["catalog"]=catalog,["user"]=frame.Get(actor).Body,["target"]=frame.Get(target).Body,["moveId"]=move.Id,["critical"]=critical,["randomPercent"]=percent,["sourceDefinition"]=move,["rockTunnelRules"]=true,["mtMoonRules"]=true,["reflect"]=frame.Sides[(int)target.Side].ReflectTurnsRemaining>0,["lightScreen"]=frame.Sides[(int)target.Side].LightScreenTurnsRemaining>0,["sourceDoubleBattle"]=frame.Layout==SourceBattleLayout.PresetDoubles,["sourceDefenderCount"]=SourceMoveTargeting.CountSourcePresentDefenders(frame,target) };
 foreach(var parameter in method.GetParameters())if(named.TryGetValue(parameter.Name!,out var value))values[parameter.Position]=value;
 return (DamageResult)method.Invoke(null,values)!;
}
var setters = new[] { ("ABRA","REFLECT","reflect"), ("ABRA","LIGHT_SCREEN","light-screen"), ("LAPRAS","MIST","mist"), ("LAPRAS","SAFEGUARD","safeguard"), ("SQUIRTLE","RAIN_DANCE","rain"), ("SANDSHREW","SANDSTORM","sandstorm"), ("ODDISH","SUNNY_DAY","sun"), ("SEEL","HAIL","hail") };
Test("eight authentic selected setting moves consume one PP and tick on their setting turn",()=>{
 foreach(var (species,move,effect) in setters)foreach(int slot in new[]{0,1,2,3}) {
  var f=Rebind(Frame(),slot,Mon(species,move));var(a,events)=Actions(f);
  var setting=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile==effect&&e.Reason=="move");
  Check(setting.Side.Value==slot&&setting.Before==0&&setting.After==5,"actual setting owner and five turns");
  var pp=events.OfType<SourcePpCommandEvent>().Single(e=>e.Actor.Value==slot);Check(pp.Before-pp.After==1,"exact one setting PP");
  Check(Timer(a.State,slot&1,effect)==5,"side/field mutation during action");var r=Next(a);Check(r.CompletedCleanTurn&&Timer(r.State,slot&1,effect)==4,"first end-turn countdown");turns++;
  var key=f.Get(new(slot)).Member!.Value;Check(r.State.Party(key.Side).Members[key.Index].Pokemon.Moves[0].CurrentPp==pp.After,"real member PP synchronized");
 }
});
Test("same-side duplicate settings fail after PP and do not refresh; five end turns expire",()=>{
 foreach(var (species,move,effect) in setters) {
  var f=Rebind(Rebind(Frame(),0,Mon(species,move)),2,Mon(species,move));var(t,events)=Turn(f);
  Check(events.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile==effect&&e.Reason=="move")==1,"one setting shared across partners");
  Check(events.OfType<SourceMoveFailedEvent>().Count(e=>e.Reason==(effect=="reflect"?"already-reflecting":"already-"+effect))==1,"duplicate failed");
  for(int i=1;i<5;i++){(t,events)=Turn(t);Check(Timer(t,0,effect)==4-i,"same weather/side setting never refreshes timer");}
  Check(events.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile==effect&&e.Reason=="expired")==1,"one final expiry");
 }
});
Test("both-side protections expire in source field order before weather and battler residuals",()=>{
 var f=Frame() with { Sides=[new(BattleSide.Player){ReflectTurnsRemaining=1,LightScreenTurnsRemaining=1,MistTurnsRemaining=1,SafeguardTurnsRemaining=1},new(BattleSide.Opponent){ReflectTurnsRemaining=1,LightScreenTurnsRemaining=1,MistTurnsRemaining=1,SafeguardTurnsRemaining=1}], Field=new(){SandstormTurnsRemaining=2} };
 foreach(var c in f.Slots)f=Rebind(f,c.Slot.Value,c.Body! with { Status=WildBattleStatus.Poison });
 var (a,_)=Actions(f);var r=Next(a);Check(r.CompletedCleanTurn,"ordered turn complete");turns++;
 var timers=r.Events.OfType<SourceMtMoonVolatileEvent>().ToArray();Check(timers.Select(e=>$"{e.Volatile}:{e.Side.Value}").SequenceEqual(new[]{"reflect:0","reflect:1","light-screen:0","light-screen:1","mist:0","mist:1","safeguard:0","safeguard:1","sandstorm:0"}),"exact field order");
 Check(timers.Last().Sequence<r.Events.OfType<SourceWeatherDamageEvent>().First().Sequence&&r.Events.OfType<SourceWeatherDamageEvent>().Last().Sequence<r.Events.OfType<SourceStatusDamageEvent>().First().Sequence,"field then weather then battlers");
});
Test("permanent rain sandstorm and sun persist beyond five real four-action turns",()=>{
 foreach(var (ability,kind) in new[]{("ABILITY_DRIZZLE",SourcePermanentWeatherKind.Rain),("ABILITY_SAND_STREAM",SourcePermanentWeatherKind.Sandstorm),("ABILITY_DROUGHT",SourcePermanentWeatherKind.Sun)}){
  var f=Frame() with {Field=SourceWeatherRules.ApplyEntry(new(),ability)};
  for(int i=0;i<6;i++){var(t,e)=Turn(f);f=t;Check(f.Field.PermanentWeather?.Kind==kind,"permanent weather retained");Check(!e.OfType<SourceMtMoonVolatileEvent>().Any(x=>x.Volatile is "rain" or "sandstorm" or "sun" or "hail"),"no invented countdown");Check(e.OfType<SourceWeatherDamageEvent>().Count()==(kind==SourcePermanentWeatherKind.Sandstorm?4:0),"real permanent weather damage");}
 }
});
Test("temporary weather moves replace different permanent weather and same weather fails",()=>{
 foreach(var (species,move,effect) in setters.Skip(4))foreach(var kind in Enum.GetValues<SourcePermanentWeatherKind>()){
  var f=Rebind(Frame(),2,Mon(species,move)) with {Field=new(){PermanentWeather=new(kind)}};var(a,e)=Actions(f);
  string current=kind==SourcePermanentWeatherKind.Rain?"rain":kind==SourcePermanentWeatherKind.Sun?"sun":"sandstorm";
  if(effect==current){Check(a.State.Field.PermanentWeather?.Kind==kind&&e.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==2&&x.Reason=="already-"+effect),"same permanent fails without replacement");}
  else Check(a.State.Field.PermanentWeather is null&&Timer(a.State,0,effect)==5,"replacement clears permanent and starts five-turn timer");
  Check(e.OfType<SourcePpCommandEvent>().Single(x=>x.Actor.Value==2).CommandCost==1,"failed and successful replacement cost actual PP");
 }
});
Test("Rain Dish uses permanent rain at its ability phase before poison",()=>{
 var dish=Mon("LOTAD","ABSORB",1);dish=dish with { Status=WildBattleStatus.Poison,Pokemon=dish.Pokemon with { CurrentHp=dish.Pokemon.Stats.MaxHp-30 } };
 var f=Rebind(Frame(),2,dish) with {Field=new(){PermanentWeather=new(SourcePermanentWeatherKind.Rain)}};
 var(a,_)=Actions(f);var r=Next(a);Check(r.CompletedCleanTurn,"Rain Dish turn complete");turns++;
 Check(r.Events.OfType<SourceAbilityRecoveredEvent>().Single().Sequence<r.Events.OfType<SourceStatusDamageEvent>().Single().Sequence,"permanent Rain Dish before poison");
});
Test("permanent weather speed and suppression use all actual slot bodies",()=>{
 foreach(var (species,move,kind) in new[]{("LOTAD","GROWL",SourcePermanentWeatherKind.Rain),("ODDISH","SWEET_SCENT",SourcePermanentWeatherKind.Sun)}){
  // Only order is exercised: these authentic selected moves need not belong to this action subset.
  var f=Rebind(Frame(),2,Mon(species,move));var committed=SourceBattleIdentityRules.CommitSelections(f,f.Revision,Choices(f));Check(committed.Succeeded,"real queue committed");f=committed.State;
  var perm=f with {Field=new(){PermanentWeather=new(kind)}};var temp=f with {Field=Weather(kind==SourcePermanentWeatherKind.Rain?"rain":"sun",3)};
  foreach(var phase in new[]{WildBattleOrderPhase.Actions,WildBattleOrderPhase.EndTurn}){
   var a=SourceBattleTurnOrder.Resolve(catalog,perm,phase);var b=SourceBattleTurnOrder.Resolve(catalog,temp,phase);Check(a.Fault is null&&JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b),"permanent and temporary exact speed order/RNG");
   var dry=SourceBattleTurnOrder.Resolve(catalog,f,phase);Check(!a.Order.SequenceEqual(dry.Order),"actual permanent speed boost changes retained order");
  }
  foreach(var (holder,pid,holderMove) in new[]{("PSYDUCK",1u,"SCREECH"),("RAYQUAZA",0u,"SCARY_FACE")})foreach(bool living in new[]{true,false}){
   var cloud=Mon(holder,holderMove,pid);cloud=cloud with {Pokemon=cloud.Pokemon with {CurrentHp=living?cloud.Pokemon.Stats.MaxHp:0}};
   var suppressed=Rebind(perm,3,cloud) with {SourceAbsentMask=8,TurnStartAbsentMask=8};var dry=suppressed with {Field=new()};
   var a=SourceBattleTurnOrder.Resolve(catalog,suppressed,WildBattleOrderPhase.Actions);var b=SourceBattleTurnOrder.Resolve(catalog,dry,WildBattleOrderPhase.Actions);
   Check(SourceWeatherRules.IsSuppressed(suppressed)==living,"living source-absent holder suppression");if(living)Check(JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b),"suppressed permanent matches dry order");
  }
 }
});
Test("Cloud Nine and Air Lock spectators suppress permanent residual without clearing weather",()=>{
 foreach(var (holder,pid,move) in new[]{("PSYDUCK",1u,"SCREECH"),("RAYQUAZA",0u,"SCARY_FACE")})foreach(bool living in new[]{true,false}){
  var cloud=Mon(holder,move,pid);cloud=cloud with {Pokemon=cloud.Pokemon with {CurrentHp=living?cloud.Pokemon.Stats.MaxHp:0}};
  var f=Rebind(Frame(),3,cloud) with {SourceAbsentMask=8,TurnStartAbsentMask=8,Field=new(){PermanentWeather=new(SourcePermanentWeatherKind.Sandstorm)}};
  var (t,e)=Turn(f);Check(e.OfType<SourceWeatherDamageEvent>().Count()==(living?0:3),"source-absent living suppression, fainted exclusion");Check(t.Field.PermanentWeather?.Kind==SourcePermanentWeatherKind.Sandstorm,"suppression never clears permanent weather");
 }
});
Test("screen and permanent weather state validate after JSON; unsafe lifecycles still reject",()=>{
 var f=Frame() with {Field=new(){PermanentWeather=new(SourcePermanentWeatherKind.Rain)},Sides=[new(BattleSide.Player){ReflectTurnsRemaining=2,MistTurnsRemaining=4},new(BattleSide.Opponent){LightScreenTurnsRemaining=3,SafeguardTurnsRemaining=5}]};
 var restored=JsonSerializer.Deserialize<SourceBattleFrame>(JsonSerializer.Serialize(f))!;Check(SourceFourActionTurns.ValidateCapability(catalog,restored) is null,"roundtrip capability");
 var a=Turn(f);var b=Turn(restored);Check(JsonSerializer.Serialize(a)==JsonSerializer.Serialize(b),"same four-turn result after JSON");
 foreach(var bad in new[]{f with {Field=f.Field with {RainTurnsRemaining=1}},f with {Field=f.Field with {PermanentWeather=new((SourcePermanentWeatherKind)99)}},f with {Field=f.Field with {PermanentWeather=new(SourcePermanentWeatherKind.Rain){RulesVersion="bad"}}},f with {SourceMechanicsRulesVersion="unrecognized"},f with {Sides=f.Sides.SetItem(0,f.Sides[0] with {Spikes=1})},f with {Sides=f.Sides.SetItem(0,f.Sides[0] with {FollowMeTimer=1,FollowMeTarget=new(2)})}})Atomic(bad);
 foreach(var (species,move) in new[]{("CASTFORM","TACKLE"),("PORYGON","TACKLE"),("KYOGRE","CALM_MIND"),("GROUDON","BULK_UP"),("TYRANITAR","LEER")})Atomic(Rebind(Frame(),2,Mon(species,move)));
});
Test("Reflect and Light Screen selected on one partner reduce later real four-slot damage",()=>{
 foreach(var (screen,attack) in new[]{("REFLECT","TACKLE"),("LIGHT_SCREEN","WATER_GUN")}){
  var f=Rebind(Rebind(Frame(),0,Mon("ALAKAZAM",screen)),1,Mon("SQUIRTLE",attack));var(a,e)=Actions(f);
  var set=e.OfType<SourceMtMoonVolatileEvent>().Single(x=>x.Reason=="move"&&x.Side.Value==0);
  var hit=e.OfType<SourceHpChangedEvent>().Single(x=>x.Actor.Value==1);Check(hit.Target.Value==2&&set.Sequence<hit.Sequence,"same-side partner protected after actual setting action");
  var expected=Damage(a.State,new(1),new(2),hit.Critical,hit.RandomPercent);Check(hit.Damage==expected.Damage,"canonical damage uses real side screen/count");
  if(!hit.Critical)Check(expected.Steps.Any(x=>x.Operation==(screen=="REFLECT"?"reflect":"light-screen")),"screen participates in damage");
  var r=Next(a);Check(r.CompletedCleanTurn,"screen/damage turn reaches Ready");turns++;
 }
});
Test("screen strength counts source-present bodies even at zero HP, independently of vacancy",()=>{
 foreach(string screen in new[]{"reflect","light-screen"})foreach(bool absent in new[]{false,true})foreach(bool living in new[]{false,true}){
  var f=Rebind(Frame(),1,Mon("SQUIRTLE",screen=="reflect"?"TACKLE":"WATER_GUN"));var partner=f.Get(new(0)).Body!;
  f=Rebind(f,0,partner with {Pokemon=partner.Pokemon with {CurrentHp=living?partner.Pokemon.Stats.MaxHp:0}}) with {SourceAbsentMask=(byte)(absent?1:0),TurnStartAbsentMask=(byte)(absent?1:0),Sides=[new(BattleSide.Player){ReflectTurnsRemaining=screen=="reflect"?4:0,LightScreenTurnsRemaining=screen=="light-screen"?4:0},new(BattleSide.Opponent)]};
  var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Get(new(1)).Occupant!.Value,f.Revision,0,new(2)));
  Check(r.Succeeded,r.Fault??"count edge action");var hit=r.Events.OfType<SourceHpChangedEvent>().Single();var expected=Damage(f,new(1),new(2),hit.Critical,hit.RandomPercent);
  Check(SourceMoveTargeting.CountSourcePresentDefenders(f,new(2))==(absent?1:2)&&hit.Damage==expected.Damage,"presence-only screen multiplier");
 }
});
Test("Mist and Safeguard protect either side during actual selected attacks",()=>{
 foreach(int protectedSide in new[]{0,1}){
  var f=Frame();int attacker=protectedSide^1,target=protectedSide==0?2:1;
  f=Rebind(f,attacker,Mon("RATICATE","SCARY_FACE"));f=f with {Sides=f.Sides.SetItem(protectedSide,new((BattleSide)protectedSide){MistTurnsRemaining=2})};
  var(r,e)=Actions(f);Check(e.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==attacker&&x.Reason=="mist"),"Mist blocks stat drop on protected side");Check(r.State.Get(new(target)).Body!.SpeedStage==6,"no speed mutation");
  bool blocked=false;for(uint seed=0;seed<20&&!blocked;seed++){
   f=Rebind(Frame(),attacker,Mon("VENONAT","POISON_POWDER"));f=f with {Rng=new(seed),Sides=f.Sides.SetItem(protectedSide,new((BattleSide)protectedSide){SafeguardTurnsRemaining=2})};
   (r,e)=Actions(f);blocked=e.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==attacker&&x.Reason=="safeguard");Check(r.State.Slots.Where(x=>(int)x.Slot.Side==protectedSide).All(x=>x.Body!.Status==0),"no poison through Safeguard");
  }Check(blocked,"Safeguard observed after authentic accuracy gate");
 }
});
Test("944 original C setting commands field-owner timers and screen/count vectors",()=>{
 using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(args[2],"four-field-vectors.json")));
 Check(document.RootElement.GetProperty("sourceCommit").GetString()==catalog.SourceCommit,"exact source commit");
 SourceFieldRegisters Field(int bits,int duration) => bits switch {1=>new(){RainTurnsRemaining=duration},5=>new(){PermanentWeather=new(SourcePermanentWeatherKind.Rain)},8=>new(){SandstormTurnsRemaining=duration},24=>new(){PermanentWeather=new(SourcePermanentWeatherKind.Sandstorm)},32=>new(){SunTurnsRemaining=duration},96=>new(){PermanentWeather=new(SourcePermanentWeatherKind.Sun)},128=>new(){HailTurnsRemaining=duration},_=>new()};
 int Bits(SourceFieldRegisters f)=>f.PermanentWeather?.Kind switch {SourcePermanentWeatherKind.Rain=>5,SourcePermanentWeatherKind.Sandstorm=>24,SourcePermanentWeatherKind.Sun=>96,_=>f.RainTurnsRemaining>0?1:f.SandstormTurnsRemaining>0?8:f.SunTurnsRemaining>0?32:f.HailTurnsRemaining>0?128:0};
 var initial=Frame();var bindings=BindingFlags.NonPublic|BindingFlags.Instance;var type=typeof(SourceFourActionTurns).Assembly.GetType("Game.Domain.Battle.FrameEndTurnContext")!;
 foreach(var row in document.RootElement.GetProperty("vectors").EnumerateArray()){
  var x=row.GetProperty("input").EnumerateArray().Select(n=>n.GetInt32()).ToArray();var y=row.GetProperty("output").EnumerateArray().Select(n=>n.GetInt32()).ToArray();
  if(x[0]==0){
   var(species,move,effect)=setters[x[2]];int slot=x[1];var f=Rebind(initial,slot,Mon(species,move)) with {Field=Field(x[3],x[4])};
   f=f with {Sides=f.Sides.SetItem(slot&1,new((BattleSide)(slot&1)){ReflectTurnsRemaining=x[6],LightScreenTurnsRemaining=x[6],MistTurnsRemaining=x[6],SafeguardTurnsRemaining=x[6]})};
   var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Get(new(slot)).Occupant!.Value,f.Revision,0,new(slot)));Check(r.Succeeded,r.Fault??"oracle setting");Check(r.Trace.IsEmpty,"setting performs no RNG commands");
   Check(Bits(r.State.Field)==y[0],"setting exact source weather bits");Check(r.Events.OfType<SourceMoveFailedEvent>().Any()==(y[6]!=0),"setting exact source failure");
   if(x[2]<4)Check(Timer(r.State,slot&1,effect)==y[2+x[2]],"setting exact side timer");else if(y[6]==0)Check(Timer(r.State,slot&1,effect)==y[1],"setting exact weather duration");
  }else if(x[0]==1){
   SourceSideRegisters Side(int i,int n)=>new((BattleSide)i){ReflectTurnsRemaining=n,LightScreenTurnsRemaining=n,MistTurnsRemaining=n,SafeguardTurnsRemaining=n};
   var f=initial with {Field=Field(x[3],x[4]),Sides=[Side(0,x[1]),Side(1,x[2])]};
   var context=Activator.CreateInstance(type,bindings|BindingFlags.Public,null,[f],null)!;type.BaseType!.GetMethod("FieldEffects",bindings)!.Invoke(context,[catalog,ImmutableArray.Create(new BattlerSlot(0),new(1),new(2),new(3))]);
   var after=(SourceBattleFrame)type.GetProperty("Frame",bindings)!.GetValue(context)!;Check(Bits(after.Field)==y[0],"full source field owner weather expiry");
   Check(after.Sides[0].ReflectTurnsRemaining==y[2]&&after.Sides[1].ReflectTurnsRemaining==y[3]&&after.Sides[0].MistTurnsRemaining==y[4]&&after.Sides[1].SafeguardTurnsRemaining==y[5],"exact both-side timer decrement");
  }else{
   bool special=x[6]!=0;var actor=Mon("SQUIRTLE",special?"WATER_GUN":"TACKLE");var target=initial.Get(new(x[4])).Body!;int power=catalog.Moves[actor.Pokemon.Moves[0].Move].Power;
   actor=actor with {Pokemon=actor.Pokemon with {Stats=actor.Pokemon.Stats with {Attack=x[1]*50,SpecialAttack=x[1]*50}}};target=target with {Pokemon=target.Pokemon with {Stats=target.Pokemon.Stats with {Defense=power*22,SpecialDefense=power*22}}};
   var f=Rebind(Rebind(initial,x[4]^1,actor),x[4],target) with {SourceAbsentMask=(byte)x[5],Sides=[new(BattleSide.Player){ReflectTurnsRemaining=3,LightScreenTurnsRemaining=3},new(BattleSide.Opponent){ReflectTurnsRemaining=3,LightScreenTurnsRemaining=3}]};
   int count=SourceMoveTargeting.CountSourcePresentDefenders(f,new(x[4]));Check(count==y[1],"unchanged source CountAliveMonsInBattle");
   // Scalar arithmetic oracle only: these intentionally edited stats do not claim instance admission.
   if(x[3]==0)f=f with {Layout=SourceBattleLayout.Singles};
   var d=Damage(f,new(x[4]^1),new(x[4]),x[2]==2,100);Check(d.Steps.Single(p=>p.Operation=="divide-50").Value==x[1],"source scalar input preserved");
   string operation=special?"light-screen":"reflect";Check(x[2]==2?!d.Steps.Any(p=>p.Operation==operation):d.Steps.Single(p=>p.Operation==operation).Value==y[0],"unchanged source screen integer arithmetic");
  }vectors++;
 }Check(vectors==944,"nonempty complete original-C vector set");
});
Directory.CreateDirectory(args[2]);
Console.WriteLine($"{checks} checks; {groups} groups; {failures} failures; {turns} real four-slot turns; {vectors} source vectors");
File.WriteAllText(Path.Combine(args[2],"four-weather-screens-result.json"),JsonSerializer.Serialize(new{passed=failures==0,checks,groups,failures,turns,vectors,runtime=Environment.Version.ToString(),results=summaries},new JsonSerializerOptions{WriteIndented=true}));
return failures==0?0:1;
