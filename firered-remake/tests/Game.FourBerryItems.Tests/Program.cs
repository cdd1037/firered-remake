using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

if (args.Length != 2) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
var definition = PretRoute8Twins.Load(args[0]);
int groups=0,checks=0,failures=0,turns=0,oracle=0;
void Check(bool value,string reason){checks++;if(!value)throw new InvalidDataException(reason);}
void Test(string name,Action work){groups++;try{work();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);}}
string Json<T>(T value)=>JsonSerializer.Serialize(value);
WildBattleCombatant Mon(string species="POOCHYENA",int level=30,string? onlyMove="HOWL"){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 if(level<5&&species=="POOCHYENA"&&onlyMove=="HOWL")onlyMove="TACKLE";
 if(onlyMove is not null){MoveId move=new("pret:MOVE_"+onlyMove);Check(catalog.Species[p.Species].LevelUpMoves.Any(m=>m.Move==move&&m.Level<=level),"natural move "+species+" "+onlyMove);p=p with{Moves=[new(move,catalog.Moves[move].Pp,catalog.Moves[move].Pp)]};}
 return new(p);
}
WildBattleCalledPartyMember Member(WildBattleCombatant b)=>new(b.Pokemon,b.HeldItem,WildBattleTransformationRules.PersistentStatus(b));
SourceBattleFrame Frame(WildBattleCombatant[]? bodies=null,WildBattleCombatant[]? players=null,WildBattleCombatant[]? opponents=null){
 var m=bodies??[Mon(),Mon(),Mon(),Mon()];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,
 SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),
 Parties=[new(BattleSide.Player,new[]{Member(m[0]),Member(m[2])}.Concat((players??[]).Select(Member)).ToImmutableArray()),new(BattleSide.Opponent,new[]{Member(m[1]),Member(m[3])}.Concat((opponents??[]).Select(Member)).ToImmutableArray())],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};
}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"success");Check(SourceBattleIdentityRules.Validate(r.State) is null,"identity: "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceBattleFrame Entry(SourceBattleFrame f)=>Good(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision)).State;
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{
 var move=catalog.Moves[c.Body!.Pokemon.Moves[0].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot:c.Slot.Opposite);}).ToImmutableArray();
SourceFourActionResult Begin(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? choices=null)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices??Choices(f)));
SourceFourActionResult Next(SourceFourActionResult r)=>Good(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
SourceActionSelection Switch(SourceBattleFrame f,int slot,int incoming)=>new(f.Slots[slot].Occupant!.Value,SourceActionKind.Switch,Incoming:new((BattleSide)(slot&1),incoming));
SourceFourActionResult EndBoundary(SourceBattleFrame f){var r=Begin(f);while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn.Phase==SourceFourActionPhase.EndTurn,"actual end-turn boundary");return r;}
(SourceBattleFrame State,ImmutableArray<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var events=r.Events.ToBuilder();for(int i=0;i<12&&r.State.FourTurn!=null;i++){r=Next(r);events.AddRange(r.Events);}Check(r.CompletedCleanTurn,"complete turn");turns++;return(r.State,events.ToImmutable());}
void SameResume(SourceFourActionResult r){var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var a=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var b=SourceFourActionTurns.Advance(catalog,copy,r.Continuation.Value);Check(Json(a)==Json(b),"exact serialized state/event/RNG continuation");}
Test("actual Twins accepts whole party with reserve Pecha Meowth, cures and consumes on slot zero switch",()=>{
 var reserve=Mon("MEOWTH",25,null) with{Status=WildBattleStatus.Poison,HeldItem="ITEM_PECHA_BERRY"};
 var party=ImmutableArray.Create(Member(Mon("RATTATA",30,"TACKLE")),Member(Mon("SPEAROW",30,"PECK")),Member(reserve));
 var created=SourceTwinsTrainerEncounter.Begin(catalog,definition,party,new(31));Check(created.Succeeded,created.Fault??"Twins entry");var f=created.State!;
 Check(f.Party(BattleSide.Player).Members.SequenceEqual(party),"entry preserves actual reserve berry and poison");Check(!created.Events.OfType<SourceHeldItemConsumedEvent>().Any(),"initial entry does not invent item callback");
 var r=Next(Begin(f,Choices(f).SetItem(0,Switch(f,0,2))));var actual=r.State.Party(BattleSide.Player).Members[2];
 Check(r.State.Slots[0].Member!.Value.Index==2&&actual.Pokemon.Personality==reserve.Pokemon.Personality,"actual reserve identity bound");
 Check(actual.Status==0&&actual.HeldItem=="ITEM_NONE"&&WildBattleSourcePartyEffects.PersistentHeldItem(actual)=="ITEM_NONE","permanent cure and genuine consumption");
 Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Item=="ITEM_PECHA_BERRY"&&r.State.Slots[0].Position.UsedHeldItem=="ITEM_PECHA_BERRY","consumption event and position used-item receipt");
 Check(r.Events.OfType<SourceCalledPartySwitchedEvent>().Single().Sequence<r.Events.OfType<SourceStatusChangedEvent>().Single().Sequence,"item after actual entry");
 SameResume(r);
});
Test("voluntary slot two Pecha entry waits until real move-end, unlike slot zero callback",()=>{
 var incoming=Mon("MEOWTH",25,null) with{HeldItem="ITEM_PECHA_BERRY",Status=WildBattleStatus.Poison};var f=Entry(Frame(players:[incoming]));
 var choices=Choices(f);int choice=Array.FindIndex(choices.ToArray(),c=>c.Actor.Slot.Value==2);
 var r=Next(Begin(f,choices.SetItem(choice,Switch(f,2,2))));
 Check(r.State.Slots[2].Body!.Status==WildBattleStatus.Poison&&r.State.Slots[2].Body!.HeldItem=="ITEM_PECHA_BERRY","no invented full item scan on ActionSwitch");
 Check(!r.Events.OfType<SourceHeldItemConsumedEvent>().Any(),"no early consumption");r=Next(r);
 Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Side.Value==2&&r.State.Party(BattleSide.Player).Members[2].Status==0,"next actual move-end cure");Finish(r);
});
Test("switch away and back never resurrects a consumed berry and retains the position receipt",()=>{
 var incoming=Mon("MEOWTH",25,null) with{HeldItem="ITEM_PECHA_BERRY",Status=WildBattleStatus.Poison};var f=Entry(Frame(players:[incoming]));
 var (first,_)=Finish(Begin(f,Choices(f).SetItem(0,Switch(f,0,2))));
 var (away,outEvents)=Finish(Begin(first,Choices(first).SetItem(0,Switch(first,0,0))));
 var (back,inEvents)=Finish(Begin(away,Choices(away).SetItem(0,Switch(away,0,2))));
 Check(back.Slots[0].Member!.Value.Index==2&&back.Slots[0].Body!.HeldItem=="ITEM_NONE"&&back.Party(BattleSide.Player).Members[2].HeldItem=="ITEM_NONE","actual consumed reserve stays itemless");
 Check(back.Slots[0].Position.UsedHeldItem=="ITEM_PECHA_BERRY"&&!outEvents.Concat(inEvents).OfType<SourceHeldItemConsumedEvent>().Any(),"used-item receipt survives entrants without duplicate consumption");
});
Test("all six status berries cure numerically at move-end and retain unrelated status or confusion",()=>{
 foreach(var (item,status,confusion) in new[]{("CHERI",WildBattleStatus.Paralysis,0),("CHESTO",3u,0),("PECHA",WildBattleSourceFieldEffects.ToxicPoison|3840u,0),("RAWST",WildBattleStatus.Burn,0),("ASPEAR",WildBattleStatus.Freeze,0),("PERSIM",0u,4)}){
 var body=Mon() with{HeldItem="ITEM_"+item+"_BERRY",Status=status,ConfusionTurnsRemaining=confusion};var f=Entry(Frame([body,body,body,Mon("POOCHYENA",50)]));
 var r=Next(Begin(f));Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Select(e=>e.Side.Value).SequenceEqual(new[]{0,1,2}),"numeric item scan "+item);
 foreach(int slot in new[]{0,1,2})Check(r.State.Slots[slot].Body is{Status:0,ConfusionTurnsRemaining:0,HeldItem:"ITEM_NONE"},"cure "+item+" "+slot);
 Finish(r);
 }
 var other=Mon() with{HeldItem="ITEM_PECHA_BERRY",Status=WildBattleStatus.Burn,ConfusionTurnsRemaining=3};var first=Next(Begin(Entry(Frame([other,Mon(),Mon(),Mon("POOCHYENA",50)]))));
 Check(first.State.Slots[0].Body!.HeldItem=="ITEM_PECHA_BERRY"&&first.State.Slots[0].Body!.Status==WildBattleStatus.Burn&&first.State.Slots[0].Body!.ConfusionTurnsRemaining==3,"wrong-condition berry remains held");
});
Test("immunity precedes berries and preserves the unneeded holder item",()=>{
 var immune=Mon("PERSIAN",30,"SCRATCH") with{HeldItem="ITEM_CHERI_BERRY",Status=WildBattleStatus.Paralysis};Check(immune.Pokemon.AbilityId=="ABILITY_LIMBER","actual Limber");
 var f=Entry(Frame([Mon(),Mon(),immune,Mon("POOCHYENA",70)]));var r=Next(Begin(f));
 Check(r.Events.OfType<SourceAbilityActivatedEvent>().Any(e=>e.Side.Value==2&&e.Ability=="ABILITY_LIMBER"),"immunity activated");
 Check(r.State.Slots[2].Body is{Status:0,HeldItem:"ITEM_CHERI_BERRY"}&&!r.Events.OfType<SourceHeldItemConsumedEvent>().Any(),"ability saves berry");
});
Test("Oran uses floor half HP and heals ten or caps, only in Items1",()=>{
 foreach(var (level,above) in new[]{(30,false),(30,true),(1,false)}){
 var holder=Mon(level:level);int hp=holder.Pokemon.Stats.MaxHp/2+(above?1:0);holder=holder with{HeldItem="ITEM_ORAN_BERRY",Pokemon=holder.Pokemon with{CurrentHp=hp}};
 var f=Entry(Frame([holder,Mon(),Mon(),Mon()]));var r=EndBoundary(f);Check(r.State.Slots[0].Body!.HeldItem=="ITEM_ORAN_BERRY","no Oran on entry or move-end");SameResume(r);r=Next(r);Check(r.CompletedCleanTurn,"Oran endturn completes");turns++;
 int healed=above?0:Math.Min(10,holder.Pokemon.Stats.MaxHp-hp);Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==hp+healed,"exact HP threshold and cap");
 Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Count()==(above?0:1),"consume only when applicable");Check(r.State.Party(BattleSide.Player).Members[0].HeldItem==(above?"ITEM_ORAN_BERRY":"ITEM_NONE"),"party item persisted");
 }
});
Test("crossing Oran threshold from poison waits through Items2 and faint callbacks until next Items1",()=>{
 var body=Mon();int hp=body.Pokemon.Stats.MaxHp/2+1;body=body with{HeldItem="ITEM_ORAN_BERRY",Status=WildBattleStatus.Poison,Pokemon=body.Pokemon with{CurrentHp=hp}};
 var first=Next(EndBoundary(Entry(Frame([body,Mon(),Mon(),Mon()]))));Check(first.CompletedCleanTurn,"first turn complete");turns++;
 Check(first.State.Slots[0].Body!.HeldItem=="ITEM_ORAN_BERRY"&&!first.Events.OfType<SourceHeldItemConsumedEvent>().Any(),"source does not heal immediately after poison");
 var second=Next(EndBoundary(first.State));Check(second.CompletedCleanTurn,"second turn complete");turns++;
 Check(second.Events.OfType<SourceHeldItemConsumedEvent>().Single().Sequence<second.Events.OfType<SourceStatusDamageEvent>().Single().Sequence,"next turn Oran before poison");
});
Test("simultaneous residual KOs retain both defeat identities while living Oran heals before burn",()=>{
 var holder=Mon() with{HeldItem="ITEM_ORAN_BERRY",Status=WildBattleStatus.Burn};holder=holder with{Pokemon=holder.Pokemon with{CurrentHp=1}};
 var doomed=Mon() with{Status=WildBattleStatus.Poison};doomed=doomed with{Pokemon=doomed.Pokemon with{CurrentHp=1}};
 var r=Next(EndBoundary(Entry(Frame([holder,doomed,Mon(),doomed],opponents:[Mon()]))));
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&r.State.FourTurn.Defeats.Length==2,"two actual opponent residual KOs wait for rewards");
 Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==11-Math.Max(1,holder.Pokemon.Stats.MaxHp/8)&&r.State.Slots[0].Body!.HeldItem=="ITEM_NONE","living Oran holder heals then suffers burn");
 Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Sequence<r.Events.OfType<SourceStatusDamageEvent>().Single(e=>e.Side.Value==0).Sequence,"item before holder residual");
 Check(r.State.Party(BattleSide.Player).Members[0].HeldItem=="ITEM_NONE"&&r.State.Party(BattleSide.Opponent).Members.Take(2).All(m=>m.Pokemon.CurrentHp==0&&m.Status==0),"both parties synchronized through reward boundary");SameResume(r);
});
Test("weather KO prevents Oran consumption and KO replacement keeps position item history",()=>{
 var body=Mon() with{HeldItem="ITEM_ORAN_BERRY"};body=body with{Pokemon=body.Pokemon with{CurrentHp=1}};
 var reserve=Mon("MEOWTH",25,null) with{HeldItem="ITEM_PECHA_BERRY",Status=WildBattleStatus.Poison};var f=Entry(Frame([body,Mon(),Mon(),Mon()],players:[reserve])) with{Field=new(){HailTurnsRemaining=3}};
 var r=Next(EndBoundary(f));Check(r.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending,"weather KO waits for replacement");
 Check(r.State.Party(BattleSide.Player).Members[0].HeldItem=="ITEM_ORAN_BERRY"&&!r.Events.OfType<SourceHeldItemConsumedEvent>().Any(),"fainted holder does not eat berry");
 r=Good(SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2)));
 Check(r.State.Slots[0].Body is{Status:0,HeldItem:"ITEM_NONE"}&&r.State.Slots[0].Position.UsedHeldItem=="ITEM_PECHA_BERRY","real KO reserve cured by source slot zero callback");
 SameResume(r);Finish(r);
});
Test("Baton Pass slot two runs the full numeric item phase after actual reserve entry",()=>{
 var passer=Mon("GIRAFARIG",50,"BATON_PASS");var reserve=Mon("MEOWTH",25,null) with{HeldItem="ITEM_PECHA_BERRY",Status=WildBattleStatus.Poison};var f=Entry(Frame([Mon(),Mon(),passer,Mon()],players:[reserve]));
 var r=Begin(f);while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);
 Check(r.State.FourTurn.Phase==SourceFourActionPhase.ActionSwitchPending,"Baton actual choice boundary");
 r=Good(SourceFourActionTurns.ChooseActionSwitch(catalog,r.State,r.Continuation!.Value,new(2),new(BattleSide.Player,2)));
 Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Side.Value==2&&r.State.Slots[2].Body!.Status==0,"full resumed move-end heals nonzero slot");Finish(r);
});
Test("five authenticated HOLD_EFFECT_NONE berries remain held and unsupported items reject atomically",()=>{
 foreach(string name in new[]{"SPELON","PAMTRE","WATMEL","DURIN","BELUE"}){
 var holder=Mon() with{HeldItem="ITEM_"+name+"_BERRY",Status=WildBattleStatus.Poison};var f=Entry(Frame([holder,Mon(),Mon(),Mon()]));var(after,events)=Finish(Begin(f));
 Check(after.Slots[0].Body!.HeldItem==holder.HeldItem&&!events.OfType<SourceHeldItemConsumedEvent>().Any(),"real inert source berry remains "+name);
 }
 foreach(string item in new[]{"ITEM_LUM_BERRY","ITEM_LEPPA_BERRY","ITEM_SITRUS_BERRY","ITEM_ENIGMA_BERRY","ITEM_LEFTOVERS","ITEM_CHOICE_BAND"}){
 var f=Frame([Mon() with{HeldItem=item},Mon(),Mon(),Mon()]);var r=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f));
 Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"unsupported active item atomic "+item);
 var encounter=SourceTwinsTrainerEncounter.Begin(catalog,definition,[Member(Mon()),Member(Mon()),Member(Mon() with{HeldItem=item})],new(31));Check(!encounter.Succeeded&&encounter.State is null&&encounter.Events.IsEmpty&&encounter.Trace.IsEmpty,"unsupported reserve item atomic "+item);
 }
});
Test("35 exact original C berry cases match shared frame phase adapter",()=>{
 const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
 var assembly=typeof(SourceFourActionTurns).Assembly;var type=assembly.GetType("Game.Domain.Battle.SourceFrameBerryItems")!;var phaseType=assembly.GetType("Game.Domain.Battle.SourceBerryPhase")!;
 var method=type.GetMethod("Apply",flags)!;using var vectors=JsonDocument.Parse(File.ReadAllText(Path.Combine(args[1],"source-berry-vectors.json")));
 Check(vectors.RootElement.GetProperty("sourceCommit").GetString()=="037335f4c725d7c9aecdac87066f2002b4bd7e14","source pin");
 Check(vectors.RootElement.GetProperty("pins").EnumerateObject().Count()==6,"bounded six-file source proof");
 foreach(var v in vectors.RootElement.GetProperty("vectors").EnumerateArray()){
 var mon=Mon();mon=mon with{HeldItem=v.GetProperty("item").GetString()!,Status=v.GetProperty("status").GetUInt32(),ConfusionTurnsRemaining=v.GetProperty("confusion").GetInt32(),Pokemon=mon.Pokemon with{CurrentHp=v.GetProperty("hp").GetInt32(),Stats=mon.Pokemon.Stats with{MaxHp=v.GetProperty("maxHp").GetInt32()}}};
 var f=Frame([Mon(),Mon(),mon,Mon()]);var r=(SourceFourActionResult)method.Invoke(null,[f,Enum.ToObject(phaseType,v.GetProperty("phase").GetInt32()),new BattlerSlot(2)])!;
 var after=r.State.Slots[2].Body!;bool consumed=v.GetProperty("effect").GetInt32()!=0;
 Check(after.Status==v.GetProperty("afterStatus").GetUInt32()&&after.ConfusionTurnsRemaining==v.GetProperty("afterConfusion").GetInt32(),"exact original status/confusion");
 Check(after.Pokemon.CurrentHp==mon.Pokemon.CurrentHp+v.GetProperty("healed").GetInt32(),"exact original HP");
 Check(after.HeldItem==(consumed?"ITEM_NONE":mon.HeldItem)&&r.Events.OfType<SourceHeldItemConsumedEvent>().Count()==(consumed?1:0),"source removeitem exact");
 Check(r.State.Rng==f.Rng&&r.Trace.IsEmpty,"item phases consume zero RNG");oracle++;
 }
 Check(oracle==35,"nonempty exact bounded oracle");
});
Check(Environment.Version.Major==10&&groups==12&&turns>0&&oracle==35,"actual runtime and nonempty suite");
var summary=new{passed=failures==0,groups,checks,failures,turns,oracle,runtime=Environment.Version.ToString()};
File.WriteAllText(Path.Combine(args[1],"result.json"),Json(summary));Console.WriteLine(Json(summary));return failures==0?0:1;
