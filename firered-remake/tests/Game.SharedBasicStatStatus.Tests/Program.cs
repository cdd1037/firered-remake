using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using Game.Domain.Randomness;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
if(args.Length!=3)return 2;
string root=args[0],source=args[1],output=args[2];
string J<T>(T x)=>JsonSerializer.Serialize(x);
string Hash<T>(T x)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(J(x))));
MoveId Id(string x)=>new("pret:MOVE_"+x);
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(root);var catalog=proof.Catalog;
string tm=File.ReadAllText(Path.Combine(source,"src/data/pokemon/tmhm_learnsets.h"));
int groups=0,checks=0,failures=0,turns=0;
void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
void Test(string name,Action run){groups++;try{run();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);}}
WildBattleCombatant Mon(string species="DROWZEE",string moves="MEDITATE",int level=70,uint pid=0){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 var block=Regex.Match(tm,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
 foreach(string move in moves.Split(','))Check(catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(move)&&x.Level<=level)||Regex.IsMatch(block,@"[TH]M\d\d_"+move+@"\)"),"authentic natural/TM move "+species+"/"+move);
 return new(p with{Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});
}
var fixtures=new[]{("PIKACHU","GROWL"),("SQUIRTLE","TAIL_WHIP"),("EKANS","LEER"),("PIDGEY","SAND_ATTACK"),("KOFFING","SMOKESCREEN"),("ALAKAZAM","KINESIS"),("PIKACHU","FLASH"),("METAPOD","HARDEN"),("SQUIRTLE","WITHDRAW"),("PARAS","GROWTH"),("PIKACHU","AGILITY"),("PIKACHU","DOUBLE_TEAM"),("SCYTHER","SWORDS_DANCE"),("GOLBAT","SCREECH"),("CATERPIE","STRING_SHOT"),("ODDISH","SWEET_SCENT"),("RATTATA","FOCUS_ENERGY"),("CLEFAIRY","COSMIC_POWER"),("PIKACHU","THUNDER_WAVE"),("ODDISH","STUN_SPORE"),("EKANS","GLARE"),("ODDISH","TOXIC"),("VULPIX","WILL_O_WISP")};
WildBattleState Pair(WildBattleCombatant a,WildBattleCombatant b,uint seed=31)=>new(a,b,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
var baseline=new List<object>();
foreach(var (species,move) in fixtures)foreach(int variant in Enumerable.Range(0,8)){
 var a=Mon(species,move);var b=Mon("SNORLAX","AMNESIA");
 if(variant==1)a=a with{AttackStage=12,DefenseStage=12,SpeedStage=12,AccuracyStage=12,EvasionStage=12,SpecialAttackStageDelta=6,SpecialDefenseStageDelta=6,FocusEnergy=true};
 if(variant==2)b=b with{AttackStage=0,DefenseStage=0,SpeedStage=0,AccuracyStage=0,EvasionStage=0,SpecialAttackStageDelta=-6,SpecialDefenseStageDelta=-6};
 if(variant==3)b=b with{Protected=true};
 if(variant==4)b=b with{Status=WildBattleStatus.Poison};
 if(variant==5)b=Mon("REGICE","AMNESIA");
 var s=Pair(a,b,(uint)(variant==6?0:31));if(variant==7)s=s with{CalledField=new(){OpponentMistTurnsRemaining=5}};
 var r=WildBattleRules.ResolveSourceMoveAction(catalog,s,new(WildBattleSide.Player,0));Check(r.Succeeded,"baseline admitted "+move+"/"+variant+" "+r.Fault);
 baseline.Add(new{move,variant,hash=Hash(r)});
}
if(output=="--baseline"){Console.WriteLine(J(baseline));return 0;}
Test("canonical singles ability admission stays narrower than four-slot admission",()=>{
 foreach(var row in new[]{("PIKACHU","GROWL","KINGLER","HARDEN"),("PIDGEY","SAND_ATTACK","TORKOAL","AMNESIA")}){var state=Pair(Mon(row.Item1,row.Item2),Mon(row.Item3,row.Item4));var r=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));Check(!r.Succeeded&&r.Battle.Events.IsEmpty&&r.Battle.Trace.IsEmpty,"old canonical ability boundary unchanged "+row.Item3);}
});
Test("frozen singles baseline with source-backed canonical ordering correction",()=>{
 var old=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline-3c78f0a7.json"))).RootElement;
 var current=JsonSerializer.SerializeToElement(baseline);var changes=new List<string>();
 for(int i=0;i<baseline.Count;i++){string move=old[i].GetProperty("move").GetString()!;int variant=old[i].GetProperty("variant").GetInt32();bool equal=old[i].GetProperty("hash").GetString()==current[i].GetProperty("hash").GetString();
 bool corrected=move is "SCREECH" or "SWEET_SCENT"&&variant!=3;Check(equal!=corrected,"only declared source-order correction "+move+"/"+variant);if(!equal)changes.Add(move+"/"+variant);}
 Check(changes.Count==14,"170 immutable cases and 14 preserved red baseline differences");File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"canonical-order-differences.json"),J(changes));
});
Test("natural canonical Screech and Sweet Scent accuracy precedes PP",()=>{
 foreach(var (species,move) in new[]{("GOLBAT","SCREECH"),("ODDISH","SWEET_SCENT")}){var state=Pair(Mon(species,move),Mon("SNORLAX","AMNESIA"));var r=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));Check(r.Succeeded,"natural source action");
 var pp=r.Battle.Events.OfType<WildBattlePpCommandEvent>().Single();var accuracy=r.Battle.Trace.Single(x=>x.Purpose.EndsWith(".accuracy"));Check(pp.RngCursor>state.Rng.Cursor&&pp.RngCursor==r.Battle.State!.Rng.Cursor,"original accuracy before attackstring/PP");Check(accuracy.Bound==100,"real accuracy command");}
});
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? bodies=null,uint seed=31){var m=bodies??[Mon(),Mon(),Mon(),Mon()];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};}
SourceBattleFrame Body(SourceBattleFrame f,int slot,WildBattleCombatant b){var c=f.Slots[slot];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=b}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(b))})};}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"four action accepted");Check(SourceBattleIdentityRules.Validate(r.State)==null,"four identity");return r;}
SourceBattleFrame Entry(SourceBattleFrame f)=>Good(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision)).State;
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? targets=null)=>f.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?c.Slot.Value:targets?[c.Slot.Value]??(c.Slot.Value^1)))).ToImmutableArray();
SourceFourActionResult Begin(SourceBattleFrame f,int[]? targets=null)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,targets)));
SourceFourActionResult Next(SourceFourActionResult r)=>Good(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
(SourceFourActionResult Result,ImmutableArray<SourceBattleEvent> Events,ImmutableArray<RandomTrace> Trace) Finish(SourceFourActionResult r){var e=r.Events.ToBuilder();var t=r.Trace.ToBuilder();for(int i=0;i<24&&r.State.FourTurn!=null;i++){r=Next(r);e.AddRange(r.Events);t.AddRange(r.Trace);}Check(r.CompletedCleanTurn,"actual complete four-action turn");turns++;return(r,e.ToImmutable(),t.ToImmutable());}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target)));Check(r.Succeeded,r.Fault??"kernel accepted");return r;}
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic "+why+" "+r.Fault);
Test("all 23 authenticated moves run inside real four-action turns",()=>{
 foreach(var (species,move) in fixtures){var f=Entry(Frame([Mon(),Mon(),Mon(species,move),Mon()]));var (r,e,t)=Finish(Begin(f));var def=catalog.Moves[Id(move)];
 Check(e.OfType<SourceMoveUsedEvent>().Select(x=>x.Actor.Value).Concat(e.OfType<SourceActionBlockedEvent>().Select(x=>x.Side.Value)).Order().SequenceEqual(new[]{0,1,2,3}),"four distinct actual actors "+move);
 var pp=e.OfType<SourcePpCommandEvent>().Where(x=>x.Actor.Value==2).ToArray();Check(pp.Length==(def.Target=="MOVE_TARGET_BOTH"?2:1),"per-script-target PP commands "+move);
 Check(r.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==def.Pp-1,"one actual slot debit "+move);
 if(def.Target=="MOVE_TARGET_BOTH")Check(e.OfType<SourceStatChangedEvent>().Where(x=>x.Actor.Value==2).Select(x=>x.Target.Value).SequenceEqual(new[]{3,1}),"both opposing source slots "+move+" "+J(e.OfType<SourceStatChangedEvent>().Where(x=>x.Actor.Value==2).ToArray()));
 if(def.Target=="MOVE_TARGET_USER")Check(!t.Any(x=>x.Purpose=="source-battle.slot-2.accuracy"),"self script has no accuracy RNG "+move);
 }
});
Test("real four-action Soundproof and Clear Body belong to separate holders",()=>{
 var f=Entry(Frame([Mon("PIKACHU","GROWL"),Mon("REGICE","AMNESIA"),Mon("PIDGEY","SAND_ATTACK"),Mon("MR_MIME","BARRIER")]));var (_,e,t)=Finish(Begin(f,[1,1,1,3]));
 Check(e.OfType<SourceAbilityPreventedEvent>().Any(x=>x.Actor.Value==0&&x.Target.Value==1&&x.Ability=="ABILITY_CLEAR_BODY"),"clear body first Growl target");
 Check(e.OfType<SourceAbilityPreventedEvent>().Any(x=>x.Actor.Value==0&&x.Target.Value==3&&x.Ability=="ABILITY_SOUNDPROOF"),"soundproof second Growl target");
 Check(t.Count(x=>x.Purpose=="source-battle.slot-0.accuracy")==1,"Soundproof stops only holder accuracy");
 Check(!e.OfType<SourceStatChangedEvent>().Any(x=>x.After<x.Before),"actual defenders protected separately");
});
Test("real Protect and Mist actions precede two target stat scripts",()=>{
 var f=Entry(Frame([Mon("PIKACHU","GROWL",30),Mon("ONIX","PROTECT"),Mon("GOLBAT","SCREECH",30),Mon("LAPRAS","MIST",100)]));var (_,e,t)=Finish(Begin(f,[1,1,3,3]));
 Check(e.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==0&&x.Target.Value==1&&x.Reason=="protected"),"Protect blocks its actual holder");
 Check(e.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==0&&x.Target.Value==3&&x.Reason=="mist"),"Mist blocks second target after its accuracy");
 Check(e.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==2&&x.Target.Value==3&&x.Reason=="mist"),"partner stat action reads same side Mist");
 Check(t.Count(x=>x.Purpose=="source-battle.slot-0.accuracy")==1,"protected target draws no accuracy, Mist target does");
});
Test("selected stat script can target its real partner",()=>{
 var f=Entry(Frame([Mon("RATTATA","FOCUS_ENERGY"),Mon(),Mon("GOLBAT","SCREECH"),Mon()]));var (_,e,_)=Finish(Begin(f,[1,0,0,2]));
 var change=e.OfType<SourceStatChangedEvent>().Single(x=>x.Actor.Value==2);Check(change.Target.Value==0&&change.Before==6&&change.After==4,"partner defense, not opposite-side alias");
});
Test("different learned positions retain actual four-slot PP ownership",()=>{
 var f=Entry(Frame([Mon("PIKACHU","GROWL,THUNDER_WAVE,AGILITY"),Mon(),Mon("GOLBAT","LEECH_LIFE,SCREECH,AIR_CUTTER"),Mon()]));var choices=Choices(f).SetItem(0,new(f.Slots[0].Occupant!.Value,SourceActionKind.Move,2,new(0))).SetItem(2,new(f.Slots[2].Occupant!.Value,SourceActionKind.Move,1,new(3)));
 var (r,e,_)=Finish(Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices)));Check(e.OfType<SourcePpCommandEvent>().Single(x=>x.Actor.Value==0).PpSlot==2&&e.OfType<SourcePpCommandEvent>().Single(x=>x.Actor.Value==2).PpSlot==1,"selected positions are not substituted");
 Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==40&&r.State.Slots[0].Body!.Pokemon.Moves[2].CurrentPp==29&&r.State.Slots[2].Body!.Pokemon.Moves[1].CurrentPp==39,"only real selected PP debited");
});
Test("two opposing Pressure holders charge the first BOTH pass only",()=>{
 var f=Entry(Frame([Mon("PIKACHU","GROWL"),Mon("LUGIA","RECOVER"),Mon(),Mon("LUGIA","RECOVER")]));var (r,e,_)=Finish(Begin(f));var pp=e.OfType<SourcePpCommandEvent>().Where(x=>x.Actor.Value==0).ToArray();
 Check(pp.Length==2&&pp[0].CommandCost==3&&pp[0].Before-pp[0].After==3&&pp[1].Before==pp[1].After,"per-pass markers retain actual PP owner");Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==37,"exact Growl PP after both holders");
});
Test("stat caps still draw enemy accuracy, never self accuracy",()=>{
 foreach(var (species,move) in fixtures.Where(x=>catalog.Moves[Id(x.Item2)].Effect is not("EFFECT_PARALYZE" or "EFFECT_TOXIC" or "EFFECT_WILL_O_WISP"))){var f=Entry(Frame([Mon(),Mon(),Mon(species,move),Mon()]));var a=f.Slots[2].Body! with{AttackStage=12,DefenseStage=12,SpeedStage=12,EvasionStage=12,SpecialAttackStageDelta=6,SpecialDefenseStageDelta=6,FocusEnergy=true};f=Body(f,2,a);
 foreach(int target in new[]{1,3})f=Body(f,target,f.Slots[target].Body! with{AttackStage=0,DefenseStage=0,SpeedStage=0,AccuracyStage=0,EvasionStage=0});
 var d=catalog.Moves[Id(move)];var r=Act(f,2,d.Target=="MOVE_TARGET_USER"?2:3);Check(!r.Events.OfType<SourceStatChangedEvent>().Any(),"no cap mutation "+move);
 Check(r.Trace.Count(x=>x.Purpose.EndsWith(".accuracy"))==(d.Target=="MOVE_TARGET_USER"?0:d.Target=="MOVE_TARGET_BOTH"?2:1),"cap source RNG "+move);
 }
});
Test("primary status immunity short circuits before accuracy on actual selected slots",()=>{
 foreach(var row in new[]{("PIKACHU","THUNDER_WAVE","DUGTRIO","SAND_ATTACK","type-immunity"),("ODDISH","STUN_SPORE","PERSIAN","SCREECH","ABILITY_LIMBER"),("ODDISH","TOXIC","SNORLAX","AMNESIA","ABILITY_IMMUNITY"),("VULPIX","WILL_O_WISP","SEAKING","AGILITY","ABILITY_WATER_VEIL"),("VULPIX","WILL_O_WISP","CHARMANDER","GROWL","fire-type-immunity")}){
 var f=Frame([Mon(),Mon(),Mon(row.Item1,row.Item2),Mon(row.Item3,row.Item4,pid:row.Item3=="SEAKING"?1u:0u)]);var r=Act(f,2,3);Check(r.Trace.IsEmpty,"immunity before accuracy "+row.Item2+"/"+row.Item3);Check(r.State.Slots[3].Body!.Status==0&&r.Events.OfType<SourcePpCommandEvent>().Single().Before-r.Events.OfType<SourcePpCommandEvent>().Single().After==1,"spend PP with no status");
 Check(r.Events.Any(e=>e is SourceMoveFailedEvent x&&x.Reason==row.Item5||e is SourceAbilityPreventedEvent y&&y.Ability==row.Item5),"exact original prevention "+row.Item5);
 }
});
Test("primary paralysis and Toxic can affect the actual ally and Synchronize source",()=>{
 var f=Frame([Mon("ABRA","TELEPORT"),Mon(),Mon("PIKACHU","THUNDER_WAVE"),Mon()]);var r=Act(f,2,0);
 Check(r.State.Slots[0].Body!.Status==WildBattleStatus.Paralysis&&r.State.Slots[2].Body!.Status==WildBattleStatus.Paralysis&&r.State.Slots[1].Body!.Status==0&&r.State.Slots[3].Body!.Status==0,"partner Synchronize returns to real actor");
 f=Frame([Mon("ABRA","TELEPORT"),Mon(),Mon("CLEFAIRY","TOXIC"),Mon()]);r=Act(f,2,0);Check(r.State.Slots[0].Body!.Status==WildBattleSourceFieldEffects.ToxicPoison&&r.State.Slots[2].Body!.Status==WildBattleStatus.Poison,"Gen III Toxic Synchronize becomes ordinary poison");
});
Test("real Safeguard action blocks later primary status after accuracy",()=>{
 var f=Entry(Frame([Mon(),Mon(),Mon("ODDISH","TOXIC",30),Mon("DRAGONAIR","SAFEGUARD",100)]));var (r,e,t)=Finish(Begin(f));
 Check(e.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==2&&x.Target.Value==3&&x.Reason=="safeguard"),"actual earlier same-turn side guard");
 Check(t.Count(x=>x.Purpose=="source-battle.slot-2.accuracy")==1&&r.State.Slots[3].Body!.Status==0,"status guard follows accuracy without status write");
 Check(r.State.Sides[1].SafeguardTurnsRemaining==4,"single ordinary end-turn countdown");
});
Test("Gen III primary paralysis retains Electric and Grass target eligibility",()=>{
 foreach(var row in new[]{("PIKACHU","THUNDER_WAVE","PIKACHU","GROWL"),("ODDISH","STUN_SPORE","ODDISH","SWEET_SCENT")}){
 var f=Frame([Mon(),Mon(),Mon(row.Item1,row.Item2),Mon(row.Item3,row.Item4)]);var r=Act(f,2,3);Check(r.State.Slots[3].Body!.Status==WildBattleStatus.Paralysis,"no later-generation immunity "+row.Item2);}
});
Test("four-action overflow rolls back PP RNG stages and holder status together",()=>{
 foreach(var move in new[]{("PIKACHU","GROWL"),("GOLBAT","SCREECH"),("VULPIX","WILL_O_WISP")}){
 var f=Entry(Frame([Mon(),Mon(),Mon(move.Item1,move.Item2,100),Mon()]));var b=Begin(f);var over=b.State with{EventSequence=long.MaxValue-2};Atomic(over,SourceFourActionTurns.Advance(catalog,over,new(over.Revision,over.EventSequence,over.NextAction,over.FourTurn!.Phase)),"mid-action event overflow "+move.Item2);
 var revision=b.State with{Revision=long.MaxValue};Atomic(revision,SourceFourActionTurns.Advance(catalog,revision,new(revision.Revision,revision.EventSequence,revision.NextAction,revision.FourTurn!.Phase)),"revision overflow");
 }
});
Test("4368 verbatim original C stat command vectors across all source holder slots",()=>{
 var ct=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.FrameRecipeContext")!;var shared=ct.BaseType!.BaseType!;const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 var change=shared.GetMethods(flags).Single(m=>m.Name=="SourceChangeStat"&&m.GetParameters().Length==7);
 string[] abilities=["ABILITY_NONE","ABILITY_CLEAR_BODY","ABILITY_WHITE_SMOKE","ABILITY_KEEN_EYE","ABILITY_HYPER_CUTTER","ABILITY_SHIELD_DUST"];
 WildBattleStat[] stats=[WildBattleStat.Attack,WildBattleStat.Defense,WildBattleStat.Speed,WildBattleStat.SpecialAttack,WildBattleStat.SpecialDefense,WildBattleStat.Accuracy,WildBattleStat.Evasion];
 var template=Frame();int vectors=0;foreach(var line in File.ReadLines(Path.Combine(Path.GetDirectoryName(output)!,"source-vectors.jsonl"))){var x=JsonDocument.Parse(line).RootElement;int I(string name)=>x.GetProperty(name).GetInt32();int actor=I("actor"),target=I("target"),stage=I("stage"),amount=I("amount");var stat=stats[I("stat")-1];
 // Primitive source command matrix deliberately supplies source register combinations,
 // separately from the natural species/learnset validation of the real turns above.
 var body=template.Slots[target].Body! with{AttackStage=stage,DefenseStage=stage,SpeedStage=stage,SpecialAttackStageDelta=stage-6,SpecialDefenseStageDelta=stage-6,AccuracyStage=stage,EvasionStage=stage,CalledOverlay=new(){Ability=abilities[I("abilityIndex")],Type1="TYPE_NORMAL",Type2="TYPE_NORMAL"}};
 var f=Body(template,target,body);if(I("mist")!=0)f=f with{Sides=f.Sides.SetItem(target&1,f.Sides[target&1] with{MistTurnsRemaining=5})};
 var context=Activator.CreateInstance(ct,flags,null,[catalog,f,new SourceResolvedMoveAction(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target)),null],null)!;
 change.Invoke(context,[new BattlerSlot(actor),new BattlerSlot(target),stat,amount,Id("GROWL"),false,false]);var after=(SourceBattleFrame)ct.GetProperty("Frame",flags)!.GetValue(context)!;var events=((IEnumerable<SourceBattleEvent>)ct.GetField("Events",flags)!.GetValue(context)!).ToArray();
 var targetBody=after.Slots[target].Body!;int actual=stat switch{WildBattleStat.Attack=>targetBody.AttackStage,WildBattleStat.Defense=>targetBody.DefenseStage,WildBattleStat.Speed=>targetBody.SpeedStage,WildBattleStat.SpecialAttack=>targetBody.SpecialAttackStageDelta+6,WildBattleStat.SpecialDefense=>targetBody.SpecialDefenseStageDelta+6,WildBattleStat.Accuracy=>targetBody.AccuracyStage,_=>targetBody.EvasionStage};
 Check(actual==I("after"),"source stage vector "+vectors);Check(events.OfType<SourceAbilityPreventedEvent>().Any()==(I("recorded")!=0),"source reveal vector "+vectors);
 Check(events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="mist")== (amount<0&&I("mist")!=0),"source Mist precedence vector "+vectors);
 foreach(int slot in Enumerable.Range(0,4).Where(z=>z!=target))Check(J(after.Slots[slot])==J(f.Slots[slot]),"other actual slot unchanged "+vectors+"/"+slot);
 Check(after.Rng==f.Rng,"stat command consumes no RNG");vectors++;
 }Check(vectors==4368,"nonempty complete extracted C matrix");
 using var proofDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(output)!,"source-proof.json")));Check(proofDoc.RootElement.GetProperty("passed").GetBoolean()&&proofDoc.RootElement.GetProperty("script_slices").GetArrayLength()==7,"original source script ordering checks");
});

var summary=new{groups,checks,failures,turns,singles=baseline.Count,runtime=Environment.Version.ToString()};File.WriteAllText(output,J(summary));Console.WriteLine(J(summary));return groups>0&&checks>0&&failures==0?0:1;
