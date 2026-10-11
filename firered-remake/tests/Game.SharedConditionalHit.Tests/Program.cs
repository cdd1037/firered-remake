using System.Collections.Immutable;
using System.Reflection;
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
string Hash<T>(T x)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(x))));
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0u)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
var original=PretRocketHideoutPokemon.Load(args[0]).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(args[0],PretCeladonApproachPokemon.Load(args[0]).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,args[0],args[0]).Catalog,args[0]).Catalog,args[0]).Catalog;
var legacy=PretMetronomeMoveCatalog.Load(args[0]).Extend(PretFuchsiaRoutePokemon.Load(old,args[0]).Catalog);
var names=new[]{"FACADE","SMELLING_SALT","REVENGE","BRICK_BREAK"};
var snapshots=new List<object>();
foreach(bool canonical in new[]{false,true})foreach(string move in names)foreach(int scenario in Enumerable.Range(0,12))foreach(bool reverse in new[]{false,true}){
 uint seed=scenario==6?0u:31u;
 var actor=Mon("CLEFAIRY",50,"METRONOME");var target=Mon(scenario==5?"GASTLY":"SNORLAX",100,"DEFENSE_CURL");
 var side=reverse?WildBattleSide.Opponent:WildBattleSide.Player; var other=reverse?WildBattleSide.Player:WildBattleSide.Opponent;
 if(scenario==1)actor=actor with{Status=WildBattleStatus.Burn};
 if(scenario==2)actor=actor with{Status=WildBattleStatus.Poison};
 if(scenario==3)target=target with{Status=WildBattleStatus.Paralysis};
 if(scenario==4)target=target with{Status=WildBattleStatus.Paralysis,CalledVolatile=new(){SubstituteHp=1}};
 if(scenario==6)target=Hp(target with{Enduring=true},1);
 if(scenario==7)target=target with{Protected=true};
 if(scenario==8)actor=actor with{DamagedByOpponentThisTurn=true,CalledDamage=new(){Turn=0,PhysicalDamage=10,PhysicalSource=other}};
 if(scenario==9)actor=actor with{DamagedByOpponentThisTurn=true,CalledDamage=new(){Turn=0,SpecialDamage=10,SpecialSource=other}};
 if(scenario==10){actor=actor with{AccuracyStage=0};target=target with{EvasionStage=12};}
 if(scenario==11)actor=actor with{DamagedByOpponentThisTurn=true};
 var state=new WildBattleState(reverse?target:actor,reverse?actor:target,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
 if(move=="BRICK_BREAK")state=state with{PlayerReflectTurnsRemaining=reverse?3:0,OpponentReflectTurnsRemaining=reverse?0:3,CalledField=new(){PlayerLightScreenTurnsRemaining=reverse?4:0,OpponentLightScreenTurnsRemaining=reverse?0:4}};
 var result=WildBattleRules.ResolveCalledMoveAction(canonical?catalog:legacy,state,new(side,0,Id(move)));
 if(!result.Succeeded)throw new Exception($"baseline {canonical}/{move}/{scenario}/{reverse}: "+result.Fault);
 snapshots.Add(new{canonical,move,scenario,reverse,seed,sha256=Hash(result),result});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse to overwrite immutable baseline");
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasConditionalHitRecipe",BindingFlags.Static|BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unchanged 516c492 product");
 File.WriteAllText(args[1],Json(new{baseCommit="516c492a2c78862e1100983edba22d867b618b5f",snapshots})+"\n");Console.WriteLine($"Captured {snapshots.Count} complete pre-edit results");return 0;
}
File.WriteAllText(args[1]+".singles-after.json",Json(snapshots));

// Independent integer calculation for these fixtures only: neutral stages, no items,
// Clefairy Normal or Snorlax Fighting into Normal. Source burn precedes +2.
int FixtureDamage(WildBattleCombatant user,WildBattleCombatant target,int power,bool critical,int percent,int multiplier,bool stab,bool weakness){
 int value=user.Pokemon.Stats.Attack*power*(2*user.Pokemon.Level/5+2)/target.Pokemon.Stats.Defense/50;
 if(user.Status==WildBattleStatus.Burn)value/=2;value=Math.Max(1,value)+2;if(critical)value*=2;value*=multiplier;
 if(stab)value=value*15/10;if(weakness)value*=2;return Math.Max(1,value*percent/100);
}
int groups=0,checks=0,failures=0,turns=0;var reports=new List<object>();var evidence=new List<object>();
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
void Test(string name,Action body){groups++;try{body();reports.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;reports.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+" "+e);}}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(int actor,string move,uint seed=31){
 var mons=Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"SPLASH") with{SpeedStage=i}).ToArray();mons[actor]=Mon("CLEFAIRY",50,move) with{SpeedStage=12};
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray(),
 Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var cell=f.Slots[slot];var key=cell.Member!.Value;var party=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,cell with{Body=m}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(m))})};}
SourceKernelResult Action(SourceBattleFrame f,int actor,int target){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target)));Check(r.Succeeded,"slot action: "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid slot action");return r;}
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,"four action: "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid four state");return r;}
SourceFourActionResult Full(SourceBattleFrame f,int actor,int target,Action<SourceBattleFrame,SourceFourActionResult>? observe=null,Dictionary<int,int>? selections=null){
 var choices=f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{var move=catalog.Moves[c.Body!.Pokemon.Moves[0].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:selections?.GetValueOrDefault(c.Slot.Value,c.Slot.Value==actor?target:c.Slot.Value^1)??(c.Slot.Value==actor?target:c.Slot.Value^1)));}).ToImmutableArray();
 var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices));int steps=0;var parts=new List<object>();
 while(r.State.FourTurn is not null){Check(++steps<50,"bounded full turn");var before=r.State;var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(before))!;
 var next=SourceFourActionTurns.Advance(catalog,before,r.Continuation!.Value);var replay=SourceFourActionTurns.Advance(catalog,copy,r.Continuation.Value);
 Check(Json(next)==Json(replay),"every continuation identical after JSON roundtrip");r=Ok(next);observe?.Invoke(before,r);parts.Add(new{before,next=r});}
 Check(r.CompletedCleanTurn,"completed real four-slot turn");turns++;evidence.Add(new{input=f,parts});return r;
}
Test("frozen complete singles results",()=>{
 using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-before-516.json")));var baseline=doc.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(baseline.Length==192&&snapshots.Count==192,"nonempty complete corpus");int same=0,changed=0;
 for(int i=0;i<baseline.Length;i++){var now=JsonSerializer.SerializeToElement(snapshots[i]);bool correction=baseline[i].GetProperty("canonical").GetBoolean()&&baseline[i].GetProperty("move").GetString()=="REVENGE"&&baseline[i].GetProperty("scenario").GetInt32()==11;
 if(!correction){Check(baseline[i].GetProperty("sha256").GetString()==now.GetProperty("sha256").GetString(),"unchanged full result "+i);same++;continue;}
 var node=System.Text.Json.Nodes.JsonNode.Parse(now.GetProperty("result").GetRawText())!;var oldNode=System.Text.Json.Nodes.JsonNode.Parse(baseline[i].GetProperty("result").GetRawText())!;
 string target=baseline[i].GetProperty("reverse").GetBoolean()?"Player":"Opponent";
 foreach(var path in new[]{new[]{"State"},new[]{"Battle","State"}}){System.Text.Json.Nodes.JsonNode n=node,o=oldNode;foreach(var part in path){n=n[part]!;o=o[part]!;}Check(n[target]!["Pokemon"]!["CurrentHp"]!.GetValue<int>()>o[target]!["Pokemon"]!["CurrentHp"]!.GetValue<int>(),"coarse stale flag no longer doubles Revenge");n[target]!["Pokemon"]!["CurrentHp"]=o[target]!["Pokemon"]!["CurrentHp"]!.DeepClone();n[target]!["CalledDamage"]!["PhysicalDamage"]=o[target]!["CalledDamage"]!["PhysicalDamage"]!.DeepClone();}
 var ev=node["Battle"]!["Events"]!.AsArray();var oldEv=oldNode["Battle"]!["Events"]!.AsArray();for(int j=0;j<ev.Count;j++)if(ev[j]?["kind"]?.GetValue<string>()=="hp-changed"){ev[j]!["HpAfter"]=oldEv[j]!["HpAfter"]!.DeepClone();ev[j]!["Damage"]=oldEv[j]!["Damage"]!.DeepClone();}
 Check(Hash(node)==baseline[i].GetProperty("sha256").GetString(),"only Revenge damage, target HP and receipt correction");changed++;}
 Check(same==190&&changed==2,"96 historical exact; 94 canonical exact; two bounded Revenge corrections");
});
Test("all moves and actor positions finish real turns",()=>{
 foreach(string move in names)foreach(int actor in Enumerable.Range(0,4)){
 var f=Frame(actor,move);int target=actor^1;bool saw=false;Full(f,actor,target,(before,r)=>{var used=r.Events.OfType<SourceMoveUsedEvent>().SingleOrDefault(e=>e.Actor.Value==actor&&e.Move==Id(move));if(used is null)return;saw=true;Check(used.Target.Value==target,"exact selected slot");Check(r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(move)].Pp-1,"single chosen PP");Check(r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Actor.Value==actor&&e.Target.Value==target),"actual selected body damage");Check(r.State.Slots[target].Registers.History?.LastTakenFrom[actor]==Id(move),"per-actor mirror history");});Check(saw,"recipe was executed");}
});
Test("Facade conditions retain Gen III burn penalty",()=>{
 foreach(uint status in new[]{0u,WildBattleStatus.Burn,WildBattleStatus.Poison,WildBattleStatus.Paralysis,WildBattleSourceFieldEffects.ToxicPoison}){
 var f=Frame(2,"FACADE");f=Put(f,2,f.Slots[2].Body! with{Status=status});bool hit=false;
 Full(f,2,3,(before,r)=>{var hp=r.Events.OfType<SourceHpChangedEvent>().SingleOrDefault(e=>e.Actor.Value==2&&e.Move==Id("FACADE"));if(hp is null)return;hit=true;
 int expected=FixtureDamage(before.Slots[2].Body!,before.Slots[3].Body!,70,hp.Critical,hp.RandomPercent,status==0?1:2,true,false);Check(hp.Damage==expected,"condition multiplier with original burn truncation");});Check(hit,"status allowed acting under chosen seed");}
});
Test("Smelling Salt cure, substitute, immunity, zero loss and contact order",()=>{
 foreach(int scenario in Enumerable.Range(0,6)){
 var f=Frame(2,"SMELLING_SALT",0);var foe=scenario==2?Mon("GASTLY",100,"SPLASH"):f.Slots[3].Body!;foe=foe with{Status=WildBattleStatus.Paralysis};
 if(scenario==1)foe=foe with{CalledVolatile=new(){SubstituteHp=1}};if(scenario==3)foe=Hp(foe with{Enduring=true},1);if(scenario==4)foe=foe with{HeldItem="ITEM_FOCUS_BAND"};if(scenario==5)foe=Mon("PIKACHU",100,"SPLASH") with{Status=WildBattleStatus.Paralysis};
 f=Put(f,3,foe);bool saw=false;Full(f,2,3,(before,r)=>{if(!r.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==2))return;saw=true;bool cures=scenario is not (1 or 2);Check((r.State.Slots[3].Body!.Status==0)==cures,"cure respects original Substitute/immunity");
 Check(r.Trace.Count(t=>t.Purpose.EndsWith(".secondary-effect"))==(scenario is 1 or 2?1:0),"certain cure RNG short circuit");
 if(scenario==1){Check(r.State.Slots[3].Body!.CalledVolatile is null,"doll actually broke");Check(!r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==3),"no spillthrough");}
 if(scenario==3){Check(r.State.Slots[3].Body!.Pokemon.CurrentHp==1,"zero actual HP loss retains cure");Check(r.Events.OfType<SourceCriticalHitEvent>().Count()==(r.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Actor.Value==2).Critical?1:0),"zero loss critical text follows actual roll");}
 if(scenario==4)Check(r.Trace.Count(t=>t.Purpose.EndsWith(".focus-band"))==1,"band command runs before cure");
 if(scenario==5){var cure=r.Events.OfType<SourceStatusChangedEvent>().Single(e=>e.Side.Value==3);var contact=r.Trace.FirstOrDefault(t=>t.Purpose.EndsWith(".static"));Check(contact is not null,"actual Static contact command");Check(cure.Reason=="smelling-salt","cure precedes contact");}
 });Check(saw,"Smelling Salt action observed");}
});
Test("Brick Break screen order, protection, immunity and ally selection",()=>{
 foreach(int scenario in Enumerable.Range(0,5)){
 var f=Frame(2,"BRICK_BREAK");f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{ReflectTurnsRemaining=4,LightScreenTurnsRemaining=3})};int target=scenario==4?0:3;
 if(scenario==1)f=Put(f,3,Mon("SNORLAX",100,"PROTECT"));if(scenario==2)f=Put(f,3,Mon("GASTLY",100,"SPLASH"));if(scenario==3)f=Put(f,2,f.Slots[2].Body! with{AccuracyStage=0});if(scenario==3)f=Put(f,3,f.Slots[3].Body! with{EvasionStage=12});
 bool saw=false;Full(f,2,target,(before,r)=>{if(!r.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==2))return;saw=true;bool blocked=scenario is 1 or 3;Check((r.State.Sides[1].ReflectTurnsRemaining==0)==!blocked,"reflect source command gate");Check((r.State.Sides[1].LightScreenTurnsRemaining==0)==!blocked,"light screen source command gate");
 var breaks=r.Events.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Reason=="brick-break").ToArray();Check(breaks.Length==(blocked?0:2),"both source timer events");if(!blocked){long pp=r.Events.OfType<SourcePpCommandEvent>().Single().Sequence;Check(breaks[0].Sequence>pp,"remove after PP");var hit=r.Events.OfType<SourceHpChangedEvent>().FirstOrDefault(e=>e.Actor.Value==2);if(hit is not null)Check(breaks[^1].Sequence<hit.Sequence,"screens removed before damage");}
 if(scenario==2)Check(r.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect)&&r.State.Slots[3].Body!.Pokemon.CurrentHp==before.Slots[3].Body!.Pokemon.CurrentHp,"Ghost immunity reapplied after screen removal");
 if(scenario==4)Check(r.State.Slots[0].Body!.Pokemon.CurrentHp<before.Slots[0].Body!.Pokemon.CurrentHp,"ally receives hit while opposing side loses screens");});Check(saw,"Brick Break executed");}
});
Test("Revenge real same-turn category/source receipts",()=>{
 foreach(int scenario in Enumerable.Range(0,7)){
 var f=Frame(2,"REVENGE");f=Put(f,2,Mon("SNORLAX",100,"REVENGE") with{SpeedStage=0});
 f=Put(f,1,Mon("CLEFAIRY",20,scenario==1?"EMBER":"POUND") with{SpeedStage=12});
 if(scenario>=2&&scenario<=5)f=Put(f,3,Mon("CLEFAIRY",20,scenario<=3?"EMBER":"POUND") with{SpeedStage=10});
 if(scenario==6)f=Put(f,2,f.Slots[2].Body! with{CalledVolatile=new(){SubstituteHp=100}});
 int target=scenario is 3 or 5?3:1;bool boost=scenario!=4&&scenario!=6;bool saw=false;
 Full(f,2,target,(before,r)=>{var hp=r.Events.OfType<SourceHpChangedEvent>().SingleOrDefault(e=>e.Actor.Value==2&&e.Move==Id("REVENGE"));if(hp is null)return;saw=true;var receipt=before.Slots[2].Registers.Damage;
 Check((receipt is not null)==(scenario!=6),"actual datahpupdate receipt excludes Substitute");
 if(scenario is 2 or 3)Check(receipt!.PhysicalSource!.Value.Value==1&&receipt.SpecialSource!.Value.Value==3,"independent physical and special sources");
 if(scenario is 4 or 5)Check(receipt!.PhysicalSource!.Value.Value==3,"later physical replaces its own category");
 var user=before.Slots[2].Body! with{DamagedByOpponentThisTurn=boost};int expected=FixtureDamage(user,before.Slots[target].Body!,60,hp.Critical,hp.RandomPercent,boost?2:1,false,true);
 Check(hp.Damage==expected,"Revenge only doubles matching real category source");},new(){[1]=2,[3]=2});Check(saw,"real Revenge executed");}
});
Test("Revenge old-turn and residual loss cannot boost",()=>{
 var f=Frame(2,"REVENGE");f=Put(f,2,Mon("SNORLAX",100,"REVENGE") with{Status=WildBattleStatus.Poison});f=Put(f,1,Mon("SNORLAX",100,"POUND"));
 var previous=Full(f,2,1,null,new(){[1]=2});Check(previous.State.Turn==1&&previous.State.Slots[2].Registers.Damage is null,"completed old turn cleared receipt after real hit and poison");
 f=Put(previous.State,1,previous.State.Slots[1].Body! with{Pokemon=previous.State.Slots[1].Body!.Pokemon with{Moves=Mon("SNORLAX",100,"SPLASH").Pokemon.Moves}});bool saw=false;Full(f,2,1,(before,r)=>{var hp=r.Events.OfType<SourceHpChangedEvent>().SingleOrDefault(e=>e.Actor.Value==2&&e.Move==Id("REVENGE"));if(hp is null)return;saw=true;Check(before.Slots[2].Registers.Damage is null,"residual poison creates no move receipt");Check(!before.Slots[2].Body!.DamagedByOpponentThisTurn,"old coarse flag cleaned");});Check(saw,"Revenge after actual previous-turn damage");
});
Test("zero HP, Endure, Wonder Guard and held-item action boundaries",()=>{
 foreach(string move in names){var f=Frame(2,move,7);f=Put(f,3,Hp(f.Slots[3].Body! with{Enduring=true},1));var r=Action(f,2,3);Check(r.State.Slots[3].Body!.Pokemon.CurrentHp==1,"Endure zero loss "+move);Check(r.Events.OfType<SourceCriticalHitEvent>().Count()==1,"critical once "+move);Check(!r.Events.OfType<SourceHeldItemHealEvent>().Any(),"no false Shell Bell");}
 var k=Frame(2,"SMELLING_SALT");k=Put(k,3,Hp(k.Slots[3].Body! with{Status=WildBattleStatus.Paralysis},1));var ko=Action(k,2,3);Check(ko.State.Slots[3].Body!.Pokemon.CurrentHp==0,"actual KO");Check(!ko.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Reason=="smelling-salt"),"fainted target does not cure");
 foreach(string move in names){var f=Frame(2,move);f=Put(f,3,Mon("SHEDINJA",50,"SPLASH"));var r=Action(f,2,3);Check(r.State.Slots[3].Body!.Pokemon.CurrentHp==1&&r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_WONDER_GUARD"),"shared Wonder Guard "+move);Check(!r.Events.OfType<SourceCriticalHitEvent>().Any(),"no critical on guarded hit");}
});
File.WriteAllText(args[1],Json(new{passed=failures==0,groups,checks,turns,baseline=192,reports})+"\n");File.WriteAllText(args[1]+".four-turns.json",Json(evidence)+"\n");
Console.WriteLine($"{groups} groups, {checks} checks, {turns} full turns, {failures} failures");return failures==0?0:1;
