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
string[] chargeMoves=["FLY","BOUNCE","DIG","DIVE","SOLAR_BEAM","RAZOR_WIND","SKY_ATTACK","SKULL_BASH"];
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
var snapshots=new List<object>();
foreach(string move in chargeMoves)foreach(uint seed in new uint[]{0,31,127}){
 var actor=Mon("CHARIZARD",60,move,"DEFENSE_CURL");var target=Mon("SNORLAX",100,"DEFENSE_CURL");
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(seed));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 var first=WildBattleRules.ResolveMove(catalog,start.State!,0);if(!first.Succeeded)throw new Exception("first:"+first.Fault);
 var release=WildBattleRules.ResolveMove(catalog,first.State!,0);if(!release.Succeeded)throw new Exception("release:"+release.Fault);
 var normal=WildBattleRules.ResolveMove(catalog,release.State!,1);if(!normal.Succeeded)throw new Exception("normal:"+normal.Fault);
 var data=Json(new{first,release,normal});snapshots.Add(new{move,seed,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant(),bytes=Encoding.UTF8.GetByteCount(data),draws=first.Trace.Length+release.Trace.Length+normal.Trace.Length});
}
if(args.Contains("--capture")){
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasChargeRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unmodified baseline");
 File.WriteAllText(args[1],Json(new{baseline="current dirty Domain/Content captured before extraction, main HEAD 16a45ea",snapshots})+"\n");return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
Test("immutable complete singles charge release and normal-next-turn parity",()=>{
 Check(Environment.Version.Major==10,"hosted runtime10");
 using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline.json")));
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==24&&old.Length==snapshots.Count,"nonempty suite");
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

Test("quartet charges, releases at current priority, then takes an ordinary next turn",()=>{
 var f=Frame([Mon("CHARIZARD",40,"FLY","DEFENSE_CURL"),Mon("BLASTOISE",80,"SKULL_BASH","DEFENSE_CURL"),Mon("SCYTHER",40,"RAZOR_WIND","DEFENSE_CURL") with{SpeedStage=12},Mon("SANDSLASH",60,"DIG","DEFENSE_CURL")]);
 var first=Turn(f,null,[3,0,1,0]);
 Check(first.State.Slots.All(c=>c.Registers.Turn?.Lock is{Kind:WildBattleCalledLockKind.Charge}),"four independent retained locks");
 Check(!first.Events.OfType<SourceHpChangedEvent>().Any(),"no charge-turn damage");
 Check(first.Events.OfType<SourcePpCommandEvent>().Count()==4&&first.Events.OfType<SourcePpCommandEvent>().All(e=>e.Before-e.After==1),"four once-only PP costs");
 Check(first.State.Slots[1].Body!.DefenseStage==7,"Skull Bash defense during charge");
 var second=Turn(first.State);
 Check(second.State.Slots.All(c=>c.Registers.Turn is null),"all four releases clear locks");
 Check(second.Events.OfType<SourcePpCommandEvent>().All(e=>e.Before==e.After),"release PP commands cost zero");
 Check(second.Events.OfType<SourceMoveMissedEvent>().Any(e=>e.Move==Id("RAZOR_WIND")&&e.Target.Value==3),"Razor Wind second foe remains underground at its action");
 Check(second.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Id("RAZOR_WIND")&&e.Target.Value==1),"Razor Wind independently reaches other foe");
 var third=Turn(second.State,[1,1,1,1]);
 Check(third.Events.OfType<SourceMoveUsedEvent>().Count(e=>e.Move==Id("DEFENSE_CURL"))==4&&third.State.Turn==f.Turn+3,"normal next turn with four real actions");
});
Test("all eight charge recipes preserve the real PP slot through exhaustion and Protect",()=>{
 foreach(var name in chargeMoves){
  var f=Frame([Mon("CHARIZARD",40,name,"DEFENSE_CURL"),Mon("SNORLAX",100,"PROTECT"),Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("SNORLAX",60,"DEFENSE_CURL")]);
  var m=f.Slots[0].Body!;f=Put(f,0,m with{Pokemon=m.Pokemon with{Moves=m.Pokemon.Moves.SetItem(0,m.Pokemon.Moves[0] with{CurrentPp=1})}});
  var charged=Turn(f);
  Check(charged.State.Slots[0].Registers.Turn?.Lock?.Move==Id(name)&&charged.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==0,name+" charges through Protect with final PP");
  var released=Turn(charged.State);
  Check(released.State.Slots[0].Registers.Turn is null&&released.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==0,name+" release clears even protected/missed");
  Check(!released.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-pp"),name+" continuation accepts zero PP");
 }
});
Test("SolarBeam weather branches, second PP on new sun, suppression and damage",()=>{
 SourceBattleFrame Base()=>Frame([Mon("CHARIZARD",40,"SOLAR_BEAM"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",70,"DEFENSE_CURL"),Mon("SNORLAX",60,"DEFENSE_CURL")]);
 var dry=Act(Base(),0,1);Check(dry.State.Slots[0].Registers.Turn is not null&&dry.Trace.IsEmpty,"ordinary charge no RNG");
 var immediate=Act(Base() with{Field=new(){SunTurnsRemaining=5}},0,1);
 Check(immediate.State.Slots[0].Registers.Turn is null&&immediate.Events.OfType<SourceHpChangedEvent>().Any(),"sun immediate");
 Check(immediate.Events.OfType<SourcePpCommandEvent>().Count()==2&&immediate.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==9,"sun PP then suppressed release command");
 var sunnyRelease=Act(dry.State with{Field=new(){SunTurnsRemaining=5}},0,1);
 Check(sunnyRelease.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==8,"new sun literal source second PP");
 var ordinary=Act(dry.State with{Rng=new(31)},0,1);
 foreach(var weather in new[]{new SourceFieldRegisters{RainTurnsRemaining=3},new SourceFieldRegisters{SandstormTurnsRemaining=3},new SourceFieldRegisters{HailTurnsRemaining=3}}){
  var wet=Act(dry.State with{Field=weather,Rng=new(31)},0,1);
  Check(wet.Events.OfType<SourceHpChangedEvent>().Single().Damage<ordinary.Events.OfType<SourceHpChangedEvent>().Single().Damage,"weather halves base damage before +2");
 }
 var cloud=Put(Base(),2,Mon("GOLDUCK",70,"DEFENSE_CURL"));
 Check(cloud.Slots[2].Body!.Pokemon.AbilityId=="ABILITY_DAMP","fixture default ability");
 var gold=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_GOLDUCK"),70,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(1)),new(0)).Pokemon;
 cloud=Put(cloud,2,new(gold with{Moves=cloud.Slots[2].Body!.Pokemon.Moves}));
 var suppressed=Act(cloud with{Field=new(){SunTurnsRemaining=5}},0,1);Check(suppressed.State.Slots[0].Registers.Turn is not null,"Cloud Nine forces charge");
 var paralyzed=Put(Base() with{Field=new(){SunTurnsRemaining=5},Rng=new(0)},0,Base().Slots[0].Body! with{Status=WildBattleStatus.Paralysis});
 var blocked=Act(paralyzed,0,1);Check(blocked.Events.OfType<SourceActionBlockedEvent>().Any()&&blocked.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==9&&blocked.State.Slots[0].Registers.Turn is null,"sun script spends PP before full paralysis then clears charge");
});
Test("bounded incapacity table clears charge and semi state without another PP cost",()=>{
 var f=Frame([Mon("SLAKING",60,"FLY","DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("SNORLAX",70,"DEFENSE_CURL")]);
 var charged=Act(f,0,1).State;
 foreach(string kind in new[]{"flinch","disabled","paralysis","confusion","infatuation","truant","freeze"}){
  var m=charged.Slots[0].Body!;
  m=kind switch{"flinch"=>m with{Flinched=true},"disabled"=>m with{DisabledMove=Id("FLY"),DisableTurnsRemaining=2},"paralysis"=>m with{Status=WildBattleStatus.Paralysis},"confusion"=>m with{ConfusionTurnsRemaining=3},"truant"=>m with{TruantLoafing=true},"freeze"=>m with{Status=WildBattleStatus.Freeze},_=>m};
  var input=Put(charged with{Rng=new(kind=="freeze"?1u:0u)},0,m);
  if(kind=="infatuation"){var c=input.Slots[0];input=input with{Slots=input.Slots.SetItem(0,c with{Registers=c.Registers with{InfatuationSource=new(1)}})};}
  var result=Act(input,0,1);
  Check((result.State.Slots[0].Registers.Turn is null)==(kind!="freeze"),kind+" exact lock lifetime");
  Check(result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==charged.Slots[0].Body!.Pokemon.Moves[0].CurrentPp&&!result.Events.OfType<SourcePpCommandEvent>().Any(),kind+" no extra PP");
 }
 var sleep=Put(charged,0,charged.Slots[0].Body! with{Status=3});var slept=Turn(sleep);
 Check(slept.State.Slots[0].Registers.Turn is null,"sleep cleanup cancels at end turn");
 var diving=Act(Put(f,0,Mon("CHARIZARD",60,"DIVE")),0,1).State;
 var hidden=Put(diving with{Field=new(){SandstormTurnsRemaining=3}},0,diving.Slots[0].Body! with{Status=3});
 var sand=Turn(hidden);
 var cancelled=sand.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge"&&e.Reason=="sleep");
 Check(sand.Events.OfType<SourceWeatherDamageEvent>().Any(e=>e.Side.Value==0&&e.Sequence>cancelled.Sequence),"sleep clears underwater before sandstorm residual");
});
Test("semi-invulnerable flags reject ordinary and always-hit moves; Surf/Whirlpool exceptions",()=>{
 foreach(var name in new[]{"FLY","BOUNCE","DIG","DIVE"})foreach(var attack in new[]{"POUND","SWIFT","MEAN_LOOK","SURF","WHIRLPOOL"}){
  var f=Frame([Mon("CHARIZARD",40,name),Mon("SNORLAX",100,attack),Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("SNORLAX",70,"DEFENSE_CURL")]);
  var charged=Act(f,0,1).State;var hit=Act(charged,1,0);
  bool exception=name=="DIVE"&&attack is "SURF" or "WHIRLPOOL";
  Check(hit.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==0)==exception,name+"/"+attack+" hit exception");
  if(!exception&&attack!="SURF")Check(!hit.Trace.Any(t=>t.Purpose.EndsWith(".accuracy")),"semi miss consumes no accuracy RNG");
 }
 foreach(var name in new[]{"GUST","TWISTER","THUNDER","SKY_UPPERCUT","EARTHQUAKE","MAGNITUDE","SURF","WHIRLPOOL"}){
  var m=catalog.Moves[Id(name)];var semi=name is "EARTHQUAKE" or "MAGNITUDE"?WildBattleSemiInvulnerable.Underground:name is "SURF" or "WHIRLPOOL"?WildBattleSemiInvulnerable.Underwater:WildBattleSemiInvulnerable.Air;
  Check(!SourceChargeRules.Misses(semi,m),"source exception "+name);Check(SourceChargeRules.DamageMultiplier(semi,m)==(name is "THUNDER" or "SKY_UPPERCUT"?1:2),"source multiplier "+name);
 }
});
Test("release retargets vacant selected position and current occupant, while departure clears own lock",()=>{
 var f=Frame([Mon("CHARIZARD",40,"DIG"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("SNORLAX",70,"DEFENSE_CURL")]);
 var charged=Turn(f).State;
 var absent=charged with{SourceAbsentMask=2};var retarget=Turn(absent);
 Check(retarget.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Actor.Value==0&&e.Target.Value==3),"retained target1 falls back to slot3");
 var departed=SourceBattleIdentityRules.ClearDepartureReferences(charged,new(0),SourceDepartureKind.Faint);
 Check(departed.Slots[0].Registers.Turn is null,"departure drops charge and derived semi bit");
 var refused=SourceFourActionTurns.BeginActions(catalog,charged,charged.Revision,Choices(charged).Select(c=>c.Actor.Slot.Value==0?c with{MoveSlot=1}:c).ToImmutableArray());
 Rejected(charged,refused,"cannot select another move while locked");
 foreach(var name in new[]{"METRONOME","TRANSFORM"})Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(name)]),"authenticated caller and copy recipe admitted "+name);
});

Test("immutable original C charge vectors and exact command metadata",()=>{
 using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
 Check(source.RootElement.GetProperty("vectors").GetArrayLength()==26,"26 independent C command vectors");
 foreach(var name in chargeMoves){var row=source.RootElement.GetProperty("moves").GetProperty(name);var move=catalog.Moves[Id(name)];
  Check(move.Power==int.Parse(row.GetProperty("power").GetString()!)&&move.Accuracy==int.Parse(row.GetProperty("accuracy").GetString()!)&&move.Pp==int.Parse(row.GetProperty("pp").GetString()!)&&move.Priority==0&&move.Target==row.GetProperty("target").GetString(),"authenticated exact move "+name);
 }
 string oldPath=Path.Combine(args[0],"tests/Game.CalledTurnLocks.Tests/source-vectors.json");
 Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(oldPath))).ToLowerInvariant()=="112ad8fc0184416f4037668f441b0e2cfc301735f845fd6e8d769272dc0af0fc","preexisting original C vectors immutable");
 using var original=JsonDocument.Parse(File.ReadAllText(oldPath));int vectors=0;
 PokemonInstance SourceMon(string name,int level){var m=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+name),level,42,IvPolicy.Fixed(0),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;return m with{Moves=new[]{"GROWL","METRONOME"}.Select(n=>new PokemonMoveSlot(Id(n),catalog.Moves[Id(n)].Pp,catalog.Moves[Id(n)].Pp)).ToImmutableArray()};}
 foreach(var v in original.RootElement.GetProperty("actions").EnumerateArray()){
  string name=v.GetProperty("move").GetString()!;uint seed=v.GetProperty("seed").GetUInt32();if(!chargeMoves.Contains(name)||seed is not(0 or 7))continue;
  bool continuation=v.GetProperty("continuation").GetBoolean();
  var state=new WildBattleState(new(SourceMon("CLEFABLE",20)){FirstTurnCounter=1},new(SourceMon("SNORLAX",100)){FirstTurnCounter=1},0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
  if(continuation)state=state with{Player=state.Player with{CalledTurnState=new(){Lock=new(WildBattleCalledLockKind.Charge,Id(name),1,Id("METRONOME"),WildBattleSide.Opponent,1)}}};
  var result=continuation?WildBattleRules.ResolveCalledMoveContinuation(catalog,state,WildBattleSide.Player):WildBattleRules.ResolveCalledMoveAction(catalog,state,new(WildBattleSide.Player,1,Id(name)));
  Check(result.Succeeded,result.Fault??"original vector");
  Check(result.State!.Rng.Seed==v.GetProperty("finalSeed").GetUInt32()&&result.State.Rng.Cursor==v.GetProperty("draws").GetUInt64(),name+" exact source RNG");
  Check(result.State.Player.Pokemon.Moves[1].CurrentPp==v.GetProperty("ppAfter").GetInt32(),name+" source PP");
  Check(result.Battle.Events.OfType<WildBattleHpChangedEvent>().Where(e=>e.Actor==WildBattleSide.Player).Sum(e=>e.Damage)==v.GetProperty("damage").GetInt32(),name+" exact source damage");vectors++;
 }
 Check(vectors==28,"bounded original source first/release vectors");
});
Test("release priority follows retained learned position and Pressure only costs at setup",()=>{
 var f=Frame([Mon("CHARIZARD",40,"FLY","DEFENSE_CURL"),Mon("ARTICUNO",80,"DEFENSE_CURL"),Mon("SNORLAX",70,"DEFENSE_CURL"),Mon("ARTICUNO",60,"DEFENSE_CURL")]);
 var charged=Act(f,0,1).State;Check(charged.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==13,"selected Pressure charge costs2");
 var wide=Act(Put(f,0,Mon("CHARIZARD",40,"RAZOR_WIND")),0,1);Check(wide.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==7,"RazorWind two Pressure foes costs3");
 var c=charged.Slots[0];var m=c.Body!;charged=Put(charged,0,m with{Pokemon=m.Pokemon with{Moves=m.Pokemon.Moves.SetItem(0,new(Id("QUICK_ATTACK"),30,30))}});
 c=charged.Slots[0];charged=charged with{Slots=charged.Slots.SetItem(0,c with{Registers=c.Registers with{Turn=c.Registers.Turn! with{Lock=c.Registers.Turn.Lock! with{PpSlotMove=Id("QUICK_ATTACK")}}}})};
 var release=Turn(charged);Check(release.Events.OfType<SourceOrderEvent>().First().Order[0].Value==0,"new learned +1 priority");
 Check(release.Events.OfType<SourcePpCommandEvent>().Any(e=>e.Actor.Value==0&&e.CurrentMove==Id("FLY")&&e.Before==30&&e.After==30),"locked Fly execution, replacement PP unchanged");
});
Test("no-target release clears its charge and forced departure binds immediately",()=>{
 var f=Frame([Mon("CHARIZARD",80,"DIG"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SNORLAX",70,"DEFENSE_CURL"),Mon("SNORLAX",60,"DEFENSE_CURL")]);
 var charged=Turn(f).State;var begun=Begin(charged);Check(begun.State.Queue[0].Slot.Value==0,"release fixture acts first");
 var absent=begun.State with{SourceAbsentMask=10};var failed=Ok(SourceFourActionTurns.Advance(catalog,absent,begun.Continuation!.Value));
 Check(failed.State.Slots[0].Registers.Turn is null&&failed.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="deferred-ko-no-target"),"vacant recipients consume release");
 Check(failed.Events.OfType<SourcePpCommandEvent>().Single() is var pp&&pp.Before==pp.After,"no target adds no PP cost");
 var switching=charged with{Parties=charged.Parties.SetItem(1,charged.Parties[1] with{Members=charged.Parties[1].Members.Add(Member(Mon("BLASTOISE",80,"DEFENSE_CURL")))})};
 var choices=Choices(switching).Select(x=>x.Actor.Slot.Value==1?new SourceActionSelection(x.Actor,SourceActionKind.Switch,Incoming:new(BattleSide.Opponent,2)):x).ToImmutableArray();
 var sw=Ok(SourceFourActionTurns.BeginActions(catalog,switching,switching.Revision,choices));
 sw=Ok(SourceFourActionTurns.Advance(catalog,sw.State,sw.Continuation!.Value));
 Check(sw.Events.OfType<SourceCalledPartySwitchedEvent>().Any()&&sw.State.Slots[1].Member?.Index==2,"voluntary target entry happens immediately");
 sw=Ok(SourceFourActionTurns.Advance(catalog,sw.State,sw.Continuation!.Value));
 Check(sw.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Actor.Value==0&&e.Target.Value==1)&&sw.State.Slots[1].Member?.Index==2,"release hits replacement at retained position");
 f=Frame([Mon("CHARIZARD",40,"SKULL_BASH"),Mon("SNORLAX",100,"ROAR"),Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("SNORLAX",60,"DEFENSE_CURL")]);
 f=f with{Parties=f.Parties.SetItem(0,f.Parties[0] with{Members=f.Parties[0].Members.Add(Member(Mon("BLASTOISE",50,"DEFENSE_CURL")))})};
 var r=Begin(f);var previous=r.State.Slots[0].Occupant;
 while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions){r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));if(r.Events.OfType<SourceCalledPartySwitchedEvent>().Any())break;}
 Check(r.Events.OfType<SourceCalledPartySwitchedEvent>().Any(e=>e.Kind==WildBattleCalledSwitchKind.Forced)&&r.State.Slots[0].Occupant!=previous&&r.State.Slots[0].Registers.Turn is null,"Roar immediately clears old charge on entrant");
});
File.WriteAllText(args[1],Json(new{groups,checks,failures,results})+"\n");return failures==0?0:1;
