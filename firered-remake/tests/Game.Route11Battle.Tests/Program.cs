// Context reconstruction of the 2026-10-09 Route11 suite. No recovered-binary claims.
using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
if(args.Length!=1)return 2;
string root=Path.GetFullPath(args[0]);
var imported=PretRoute11Pokemon.Load(root);
var historical=PretVermilionGymTrainers.ExtendPokemon(root,PretSsAnnePokemon.Load(root).Catalog);
var catalog=imported.Catalog with{Moves=imported.Catalog.Moves.SetItems(historical.Moves)};
const string version=WildBattleRules.Route11RulesVersion;
int checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
void Test(string name,Action action){try{action();results.Add(new{name,passed=true});}catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.ToString()});}}
MoveId Move(string name)=>new("pret:MOVE_"+name);
PokemonInstance Mon(string name,int level=31,uint pid=255)=>PokemonFactory.Create(catalog,new(new("pret:SPECIES_"+name),level,42,IvPolicy.Fixed(0),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
PokemonInstance Moves(PokemonInstance p,params string[] moves)=>p with{Moves=moves.Select(m=>new PokemonMoveSlot(Move(m),catalog.Moves[Move(m)].Pp,catalog.Moves[Move(m)].Pp)).ToImmutableArray()};
WildBattleState Pair(string move,uint seed=0,string actor="VULPIX",string target="WIGGLYTUFF",string reply="SPLASH",int actorLevel=31,int targetLevel=50,uint actorPid=255,uint targetPid=255)=>new(new(Moves(Mon(actor,actorLevel,actorPid),move)),new(Moves(Mon(target,targetLevel,targetPid),reply)),1,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:version){Terrain=WildBattleTerrain.Cave};
WildBattleState Accepted(WildBattleResult r){Check(r.Succeeded,r.Fault??"transition failed");var error=WildBattleRules.Validate(catalog,r.State);Check(error is null,error??"invalid state");return r.State!;}
void Atomic(WildBattleState s,WildBattleResult r,string reason){Check(!r.Succeeded&&r.Fault==reason,r.Fault??"unexpected acceptance");Check(ReferenceEquals(s,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"non-atomic fault");}
using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tests/Game.Route11Battle.Tests/source-vectors.json")));
var vectors=fixture.RootElement.GetProperty("vectors").EnumerateArray().ToArray();
void Trace(WildBattleResult r,JsonElement v){var expected=v.GetProperty("trace").EnumerateArray().ToArray();Check(r.Trace.Length==expected.Length,"draw count "+string.Join(",",r.Trace.Select(t=>t.Purpose)));for(int i=0;i<expected.Length;i++)Check(r.Trace[i].Purpose==expected[i].GetProperty("purpose").GetString()&&r.Trace[i].Raw==expected[i].GetProperty("raw").GetUInt16()&&r.Trace[i].Result==expected[i].GetProperty("result").GetInt32(),"draw "+i);Check(r.State!.Rng.Seed==v.GetProperty("seedAfter").GetUInt32(),"tail seed");}
Test("explicit scope and unsupported higher-level metadata",()=>{
 Check(catalog.Species.Count==79&&imported.Catalog.Moves.Count==209&&catalog.Moves.Count==210,"catalog cardinality");
 foreach(string m in new[]{"IMPRISON","MUD_SLAP","TRI_ATTACK","WILL_O_WISP","FLASH","RAIN_DANCE","SAND_TOMB","CONFUSE_RAY","FLAMETHROWER"}){Check(WildBattleRules.IsMoveSupported(Move(m),version),m);Check(!WildBattleRules.IsMoveSupported(Move(m),WildBattleRules.VermilionGymRulesVersion),"old profile widened "+m);}
 foreach(string m in new[]{"BELLY_DRUM","FISSURE","GRUDGE"}){Check(catalog.Moves.ContainsKey(Move(m)),"metadata missing");var s=Pair(m);Atomic(s,WildBattleRules.ResolveMove(catalog,s,0),"wild_battle_player_move_effect_unsupported");}
});
Test("every actual default move for six species levels1 through31",()=>{
 foreach(string species in new[]{"DIGLETT","DUGTRIO","MAGNETON","POLIWAG","POLIWHIRL","VULPIX"})for(int level=1;level<=31;level++)foreach(var slot in Mon(species,level).Moves){Check(WildBattleRules.IsMoveSupported(slot.Move,version),$"{species} Lv{level} {slot.Move}");Accepted(WildBattleRules.ResolveMove(catalog,Pair(slot.Move.Value[10..],actor:species,actorLevel:level,targetLevel:100),0));}
});
Test("sixteen independent fixed source branch and RNG vectors",()=>{
 foreach(var v in vectors){string kind=v.GetProperty("kind").GetString()!;uint seed=v.GetProperty("seed").GetUInt32();var s=kind switch{
 "wisp"=>Pair("WILL_O_WISP",seed),"tri"=>Pair("TRI_ATTACK",seed,"MAGNETON"),"mud"=>Pair("MUD_SLAP",seed,"DIGLETT"),"sand"=>Pair("SAND_TOMB",seed,"DUGTRIO"),"confuse"=>Pair("CONFUSE_RAY",seed),"absorb"=>Pair("WATER_GUN",seed,"SQUIRTLE","POLIWAG",targetPid:254),"damp"=>Pair("SELF_DESTRUCT",seed,"GEODUDE","POLIWAG"),"rain"=>Pair("RAIN_DANCE",seed,"POLIWHIRL",actorLevel:27),_=>Pair("SPLASH",seed)};
 if(kind=="imprison")s=s with{Player=s.Player with{Pokemon=Moves(s.Player.Pokemon,"IMPRISON","SPLASH")}};
 if(kind=="frozen")s=s with{Player=s.Player with{Status=WildBattleStatus.Freeze}};
 if(kind=="absorb")s=s with{Opponent=s.Opponent with{Pokemon=s.Opponent.Pokemon with{CurrentHp=1}}};
 var r=WildBattleRules.ResolveMove(catalog,s,0);var after=Accepted(r);Trace(r,v);
 if(kind is "wisp" or "tri")Check(after.Opponent.Status==v.GetProperty("status").GetUInt32(),"target status");
 if(kind=="frozen")Check(after.Player.Status==v.GetProperty("status").GetUInt32(),"actor freeze");
 if(kind=="mud")Check(after.Opponent.AccuracyStage==5,"accuracy stage");
 if(kind=="sand")Check(after.Opponent.BindTurnsRemaining==v.GetProperty("binding").GetInt32(),"binding counter");
 if(kind=="confuse")Check(after.Opponent.ConfusionTurnsRemaining==v.GetProperty("confusion").GetInt32(),"confusion duration");
 if(kind=="rain")Check(after.RainTurnsRemaining==4,"rain creation turn");
 if(kind=="imprison")Check(after.Player.Imprison,"imprison flag");
 if(kind=="absorb")Check(after.Opponent.Pokemon.CurrentHp==1+after.Opponent.Pokemon.Stats.MaxHp/4,"quarter heal");
 if(kind=="damp")Check(after.Player.Pokemon.CurrentHp==s.Player.Pokemon.CurrentHp&&!r.Events.OfType<WildBattleSelfDestructEvent>().Any(),"Damp selfKO");
 }
});
Test("Wisp immunity and existing status before accuracy; asleep cancellation",()=>{
 foreach(var s in new[]{Pair("WILL_O_WISP",target:"VULPIX"),Pair("WILL_O_WISP",target:"GOLDEEN",targetPid:255),Pair("WILL_O_WISP") with{Opponent=Pair("WILL_O_WISP").Opponent with{Status=WildBattleStatus.Poison}},Pair("WILL_O_WISP") with{Opponent=Pair("WILL_O_WISP").Opponent with{Status=WildBattleStatus.Burn}}}){var r=WildBattleRules.ResolveMove(catalog,s,0);var a=Accepted(r);Check(!r.Trace.Any(t=>t.Purpose=="wild-battle.player.accuracy"),"early-check RNG");Check(a.Player.Pokemon.Moves[0].CurrentPp==14&&!a.Opponent.FlashFireBoost,"PP or FlashFire boost");}
 var sleeping=Pair("WILL_O_WISP") with{Player=Pair("WILL_O_WISP").Player with{Status=2}};var blocked=WildBattleRules.ResolveMove(catalog,sleeping,0);Accepted(blocked);Check(!blocked.Events.OfType<WildBattleMoveUsedEvent>().Any(e=>e.Actor==WildBattleSide.Player),"sleep consumed PP");
});
Test("WaterAbsorb fullHP and miss branches have no critical/damage/secondary draws",()=>{
 foreach(string move in new[]{"WATER_GUN","BUBBLE","WATER_PULSE"}){var s=Pair(move,actor:"SQUIRTLE",target:"POLIWAG",targetPid:254);var r=WildBattleRules.ResolveMove(catalog,s,0);var a=Accepted(r);Check(a.Opponent.Pokemon.CurrentHp==s.Opponent.Pokemon.CurrentHp,"fullHP damage");Check(r.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_WATER_ABSORB"),"absorb event");Check(!r.Trace.Any(t=>t.Purpose is "wild-battle.player.critical" or "wild-battle.player.damage" or "wild-battle.player.secondary-effect"),"damage RNG after absorb");}
 bool missed=false;for(uint seed=0;seed<10&&!missed;seed++){var s=Pair("WATER_GUN",seed,"SQUIRTLE","POLIWAG",targetPid:254);s=s with{Player=s.Player with{AccuracyStage=0}};var r=WildBattleRules.ResolveMove(catalog,s,0);Accepted(r);if(!r.Events.OfType<WildBattleMoveMissedEvent>().Any(e=>e.Actor==WildBattleSide.Player))continue;missed=true;Check(!r.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_WATER_ABSORB"),"miss absorbed");}Check(missed,"miss fixture");
});
Test("switching into WaterAbsorb or Damp uses incoming ability before queued enemy move",()=>{
 foreach(bool damp in new[]{false,true}){
  var s=Pair("SPLASH",actor:"VULPIX",target:damp?"GEODUDE":"SQUIRTLE",reply:damp?"SELF_DESTRUCT":"WATER_GUN");
  var p=Moves(Mon("POLIWAG",31,damp?255u:254u),"SPLASH");p=p with{CurrentHp=1};
  var r=WildBattleRules.SwitchPlayer(catalog,s,new(p),true);var a=Accepted(r);
  Check(r.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e=>e.Ability==(damp?"ABILITY_DAMP":"ABILITY_WATER_ABSORB")),"incoming ability missing");
  Check(a.Player.Pokemon.CurrentHp==(damp?1:1+p.Stats.MaxHp/4),"incoming HP");
  Check(!r.Events.OfType<WildBattleSelfDestructEvent>().Any(),"Damp failed on incoming battler");
  Check(!r.Trace.Any(t=>t.Purpose is "wild-battle.opponent.critical" or "wild-battle.opponent.damage"),"incoming ability RNG");
 }
});
Test("Damp scans both active holders after PP before self KO",()=>{
 foreach(bool self in new[]{false,true}){var s=Pair("SELF_DESTRUCT",actor:self?"POLIWAG":"GEODUDE",target:self?"WIGGLYTUFF":"POLIWAG");var r=WildBattleRules.ResolveMove(catalog,s,0);var a=Accepted(r);Check(a.Player.Pokemon.CurrentHp==s.Player.Pokemon.CurrentHp&&a.Player.Pokemon.Moves[0].CurrentPp==4,"Damp HP/PP");Check(!r.Trace.Any(t=>t.Purpose.Contains("critical")||t.Purpose.Contains("accuracy")||t.Purpose.Contains("damage")),"Damp RNG");Check(r.Events.OfType<WildBattleAbilityPreventedEvent>().Single().Target==(self?WildBattleSide.Player:WildBattleSide.Opponent),"Damp holder");}
});
Test("ArenaTrap atomic Run/switch restrictions and source exemptions",()=>{
 var reserve=new WildBattleCombatant(Moves(Mon("PIDGEY"),"SPLASH"));foreach(string actor in new[]{"WIGGLYTUFF","RATTATA","PIDGEY","KOFFING"}){var s=Pair("SPLASH",actor:actor,target:"DIGLETT",actorPid:254);if(actor is "WIGGLYTUFF" or "RATTATA")Atomic(s,WildBattleRules.SwitchPlayer(catalog,s,reserve,true),"wild_battle_switch_arena_trap");else Accepted(WildBattleRules.SwitchPlayer(catalog,s,reserve,true));if(actor=="WIGGLYTUFF")Atomic(s,WildBattleRules.Run(catalog,s),"wild_battle_run_arena_trap");else Accepted(WildBattleRules.Run(catalog,s));}
 var teleport=WildBattleRules.ResolveMove(catalog,Pair("TELEPORT",actor:"ABRA",target:"DIGLETT"),0);Accepted(teleport);Check(teleport.State!.Outcome==WildBattleOutcome.Ongoing&&teleport.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="cannot-escape"),"Teleport escaped");
 var faint=Pair("SPLASH",target:"DIGLETT");faint=faint with{Player=faint.Player with{Pokemon=faint.Player.Pokemon with{CurrentHp=0}},AwaitingReplacement=true};Accepted(WildBattleRules.ReplaceFaintedPlayer(catalog,faint,reserve));Check(WildBattleRules.TryRunAfterFaint(catalog,faint).Succeeded,"fainted escape selection");
});
Test("MudSlap Ground immunity and secondary ability prevention; Flash70",()=>{
 foreach(string target in new[]{"PIDGEY","KOFFING","TENTACOOL","CATERPIE"}){var r=WildBattleRules.ResolveMove(catalog,Pair("MUD_SLAP",actor:"DIGLETT",target:target,targetPid:254),0);var a=Accepted(r);Check(a.Opponent.AccuracyStage==6,"MudSlap immunity "+target);Check(!r.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e=>e.Ability is "ABILITY_KEEN_EYE" or "ABILITY_CLEAR_BODY"),"silent secondary reveal");}
 var flash=WildBattleRules.ResolveMove(catalog,Pair("FLASH",target:"PIDGEY",targetPid:254),0);Accepted(flash);Check(flash.Events.OfType<WildBattleMoveMissedEvent>().Any(e=>e.Actor==WildBattleSide.Player),"source75 roll should miss70");
});
Test("Imprison shared move prerequisite queued cancellation and opponent Struggle",()=>{
 var no=WildBattleRules.ResolveMove(catalog,Pair("IMPRISON"),0);Accepted(no);Check(!no.State!.Player.Imprison&&no.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="no-shared-move"),"modern Imprison semantics");
 var s=Pair("IMPRISON",actor:"DUGTRIO");s=s with{Player=s.Player with{Pokemon=Moves(s.Player.Pokemon,"IMPRISON","SPLASH")}};var r=WildBattleRules.ResolveMove(catalog,s,0);var a=Accepted(r);Check(a.Player.Imprison&&a.Opponent.Pokemon.Moves[0].CurrentPp==40,"queued sealed PP");Check(r.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="imprisoned"),"queued action cancellation");Check(WildBattleRules.RequiresStruggle(a,WildBattleSide.Opponent)&&!WildBattleRules.IsMoveSelectable(a,WildBattleSide.Opponent,0)&&WildBattleRules.IsMoveSelectable(a,WildBattleSide.Opponent,-1),"state-aware move list");var struggle=WildBattleRules.ResolveMove(catalog,a,0);Accepted(struggle);Check(struggle.Events.OfType<WildBattleMoveUsedEvent>().Any(e=>e.Actor==WildBattleSide.Opponent&&e.Move==WildBattleRules.StruggleMove),"opponent Struggle");
});
Test("player Imprison selection clears on switch while rain persists",()=>{
 var s=Pair("SPLASH");s=s with{Opponent=s.Opponent with{Imprison=true},RainTurnsRemaining=3};Atomic(s,WildBattleRules.ResolveMove(catalog,s,0),"wild_battle_player_struggle_required");var r=WildBattleRules.ResolveMove(catalog,s,-1);Accepted(r);Check(r.Events.OfType<WildBattleMoveUsedEvent>().Any(e=>e.Actor==WildBattleSide.Player&&e.Move==WildBattleRules.StruggleMove),"player Struggle");
 s=Pair("SPLASH");s=s with{Player=s.Player with{Imprison=true},RainTurnsRemaining=3};var a=Accepted(WildBattleRules.SwitchPlayer(catalog,s,new(Moves(Mon("VULPIX"),"SPLASH")),true));Check(!a.Player.Imprison&&a.RainTurnsRemaining==2,"switch reset/weather");
});
Test("Fire damage thaws frozen FlashFire; Aspear cures TriAttack freeze",()=>{
 var s=Pair("EMBER",actor:"VULPIX",target:"VULPIX");s=s with{Opponent=s.Opponent with{Status=WildBattleStatus.Freeze}};bool found=false;for(uint seed=0;seed<20&&!found;seed++){var r=WildBattleRules.ResolveMove(catalog,s with{Rng=new(seed)},0);Accepted(r);found=r.Events.OfType<WildBattleStatusChangedEvent>().Any(e=>e.Reason=="fire-hit-thaw");if(found)Check(!r.State!.Opponent.FlashFireBoost&&r.Events.OfType<WildBattleHpChangedEvent>().Any(e=>e.Actor==WildBattleSide.Player&&e.Damage>0),"frozen FlashFire absorption");}Check(found,"fire thaw vector");
 var v=vectors.Single(v=>v.GetProperty("kind").GetString()=="tri"&&v.GetProperty("status").GetUInt32()==32);s=Pair("TRI_ATTACK",v.GetProperty("seed").GetUInt32(),"MAGNETON");s=s with{Opponent=s.Opponent with{HeldItem="ITEM_ASPEAR_BERRY"}};var a=Accepted(WildBattleRules.ResolveMove(catalog,s,0));Check(a.Opponent.Status==0&&a.Opponent.HeldItem=="ITEM_NONE","Aspear");
});
Test("rain five boundaries nonrefresh source rounding SwiftSwim",()=>{
 var s=Accepted(WildBattleRules.ResolveMove(catalog,Pair("RAIN_DANCE",actor:"POLIWHIRL",actorLevel:27),0));Check(s.RainTurnsRemaining==4,"initial rain");for(int n=3;n>=0;n--){var r=WildBattleRules.ResolveMove(catalog,s,0);s=Accepted(r);Check(s.RainTurnsRemaining==n&&r.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="already-raining"),"rain refreshed");}
 foreach(string m in new[]{"WATER_GUN","EMBER","TACKLE"}){var p=Pair(m,actor:"SQUIRTLE");var dry=WildBattleDamage.Calculate(catalog,p.Player,p.Opponent,Move(m),false,100,version);var wet=WildBattleDamage.Calculate(catalog,p.Player,p.Opponent,Move(m),false,100,version,rain:true);int basis=dry.Steps.Single(x=>x.Operation=="physical-minimum-before-plus-two").Value;int expected=m=="WATER_GUN"?basis*15/10:m=="EMBER"?basis/2:basis;Check(wet.Steps.Single(x=>x.Operation=="plus-two").Value==expected+2,"rain integer order");}
 s=Pair("SPLASH",actor:"HORSEA",target:"VULPIX",actorLevel:31,targetLevel:50,actorPid:254) with{RainTurnsRemaining=3};var speed=WildBattleRules.ResolveMove(catalog,s,0);Accepted(speed);Check(speed.Events.OfType<WildBattleOrderEvent>().First(e=>e.Phase==WildBattleOrderPhase.Actions).First==WildBattleSide.Player,"SwiftSwim");
});
Test("source weights and no modern Sturdy or rain SandVeil",()=>{
 Check(Route11BattleData.WeightHectograms(new("pret:SPECIES_DIGLETT"))==8&&Route11BattleData.WeightHectograms(new("pret:SPECIES_MAGNETON"))==600,"weights");var a=Accepted(WildBattleRules.ResolveMove(catalog,Pair("TACKLE",actor:"WIGGLYTUFF",target:"MAGNETON",actorLevel:100,targetLevel:1),0));Check(a.Opponent.Pokemon.CurrentHp==0,"modern Sturdy");var s=Pair("TACKLE",actor:"WIGGLYTUFF",target:"DIGLETT",targetPid:254);var dry=WildBattleRules.ResolveMove(catalog,s,0);var wet=WildBattleRules.ResolveMove(catalog,s with{RainTurnsRemaining=3},0);Accepted(dry);Accepted(wet);Check(dry.Trace.SequenceEqual(wet.Trace),"SandVeil rain RNG");
});
Test("new JSON flags explicit and malformed states atomic",()=>{
 var s=Pair("SPLASH");string json=JsonSerializer.Serialize(s);Check(!json.Contains("Imprison")&&!json.Contains("RainTurnsRemaining"),"legacy JSON");var live=s with{Player=s.Player with{Imprison=true},RainTurnsRemaining=4};var restored=JsonSerializer.Deserialize<WildBattleState>(JsonSerializer.Serialize(live))!;Check(WildBattleRules.Validate(catalog,restored)==null&&restored.Player.Imprison&&restored.RainTurnsRemaining==4,"roundtrip");var malformed=s with{RainTurnsRemaining=6};Atomic(malformed,WildBattleRules.ResolveMove(catalog,malformed,0),"route11_weather_state");Check(WildBattleRules.Validate(historical,s with{RulesVersion=WildBattleRules.VermilionGymRulesVersion,RainTurnsRemaining=1})=="route11_weather_state","old rain admitted");
});
Test("unsupported opponent and mutated supported move atomic",()=>{
 var s=Pair("SPLASH",reply:"GRUDGE");Atomic(s,WildBattleRules.ResolveMove(catalog,s,0),"wild_battle_opponent_move_effect_unsupported");s=Pair("MUD_SLAP",actor:"DIGLETT");var altered=catalog with{Moves=catalog.Moves.SetItem(Move("MUD_SLAP"),catalog.Moves[Move("MUD_SLAP")] with{Power=21})};Atomic(s,WildBattleRules.ResolveMove(altered,s,0),"wild_battle_move_definition_unsupported");
});
Test("Dugtrio31 wild start and79 rewards Pickup keep freeze",()=>{
 var p=new WildBattleCombatant(Moves(Mon("VULPIX",50),"SPLASH"));Accepted(WildBattleRules.Start(catalog,p,new(Mon("DUGTRIO",31)),new(0),version,WildBattleTerrain.Cave));var owned=new PartyPokemon(1,p.Pokemon,"VULPIX","RED",0,2,"MAPSEC_ROUTE_11",31,4,"ITEM_POKE_BALL","ITEM_NONE"){Status=WildBattleStatus.Freeze};foreach(string name in new[]{"DIGLETT","DUGTRIO","MAGNETON","POLIWAG","POLIWHIRL","VULPIX"}){var d=Mon(name) with{CurrentHp=0};var r=WildVictoryRewards.Apply(catalog,owned,d,"MAPSEC_ROUTE_11",forestRules:true,route11Rules:true);Check(r.ExperienceGained==catalog.Species[d.Species].ExperienceYield*31/7&&r.Pokemon.Status==WildBattleStatus.Freeze,"EXP/freeze");}var pickup=VermilionPickupRules.Apply(catalog,[owned],new(123),WildBattleOutcome.Won,route11Rules:true);Check(pickup.Trace.IsEmpty&&pickup.Rng==new RngState(123)&&pickup.Party[0]==owned,"non-Pickup RNG");bool rejected=false;try{VermilionPickupRules.Apply(catalog,[owned],new(123),WildBattleOutcome.Won,ssAnneRules:true);}catch(NotSupportedException){rejected=true;}Check(rejected,"old Pickup79");
});
Test("32 inherited turns byte-identical except explicit rules version",()=>{
 foreach(string move in new[]{"TACKLE","EMBER","WATER_GUN","SPARK","SONIC_BOOM","SHOCK_WAVE","SELF_DESTRUCT","REST"})for(uint seed=0;seed<4;seed++){var s=Pair(move,seed,"WIGGLYTUFF");var old=WildBattleRules.ResolveMove(historical,s with{RulesVersion=WildBattleRules.VermilionGymRulesVersion},0);var now=WildBattleRules.ResolveMove(catalog,s,0);Accepted(now);Check(old.Succeeded,old.Fault??"old failure");Check(JsonSerializer.Serialize(old.Events)==JsonSerializer.Serialize(now.Events),"events "+move);Check(old.Trace.SequenceEqual(now.Trace),"RNG "+move);Check(JsonSerializer.Serialize(old.State! with{RulesVersion=version})==JsonSerializer.Serialize(now.State),"state "+move);}
});
Console.WriteLine(JsonSerializer.Serialize(new{sourceCommit=WildBattleRules.SourceCommit,recovery="context-reconstructed",checks,failures,results},new JsonSerializerOptions{WriteIndented=true}));return failures==0?0:1;
