using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if(args.Length < 2) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
string output = args[1]; Directory.CreateDirectory(output);
MoveId Id(string name) => new("pret:MOVE_" + name);
string Json<T>(T item) => JsonSerializer.Serialize(item);
WildBattleCombatant Mon(string species, int level, params string[] moves) {
 var p = SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with { Moves = moves.Select(m => new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray() });
}
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon,m.HeldItem,m.Status);
uint Seed(int sourceId) { uint inv=1; unchecked {for(int i=0;i<5;i++)inv*=2-0x41C64E6D*inv;return (((uint)(sourceId-1)<<16)-0x6073)*inv;} }
List<object> SinglesCorpus() {
 var result = new List<object>();
 foreach(var (caller,seed,terrain,sleep) in new[] {
  ("METRONOME",Seed(33),WildBattleTerrain.Plain,0u), ("METRONOME",Seed(267),WildBattleTerrain.Cave,0u),
  ("ASSIST",7u,WildBattleTerrain.Plain,0u),("MIRROR_MOVE",7u,WildBattleTerrain.Plain,0u),
  ("SLEEP_TALK",7u,WildBattleTerrain.Plain,3u),("SLEEP_TALK",7u,WildBattleTerrain.Plain,1u),
  ("NATURE_POWER",7u,WildBattleTerrain.Grass,0u),("NATURE_POWER",7u,WildBattleTerrain.LongGrass,0u),
  ("NATURE_POWER",7u,WildBattleTerrain.Sand,0u),("NATURE_POWER",7u,WildBattleTerrain.Underwater,0u),
  ("NATURE_POWER",7u,WildBattleTerrain.Water,0u),("NATURE_POWER",7u,WildBattleTerrain.Pond,0u),
  ("NATURE_POWER",7u,WildBattleTerrain.Mountain,0u),("NATURE_POWER",7u,WildBattleTerrain.Cave,0u),
  ("NATURE_POWER",7u,WildBattleTerrain.Building,0u),("NATURE_POWER",7u,WildBattleTerrain.Plain,0u)}) {
   var player=Mon("CLEFABLE",30,caller,"GROWL") with {Status=sleep};var opponent=Mon("RATTATA",80,"SPLASH");
   var s=new WildBattleState(player,opponent,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion) {
    SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,Terrain=terrain,
    CalledParty=new([Member(player),Member(Mon("CLEFABLE",30,"NATURE_POWER"))],[Member(opponent)],0,0)};
   var r=WildBattleRules.ResolveSourceMoveAction(catalog,s,new(WildBattleSide.Player,0){Context=new(0,0)});
   if(!r.Succeeded)throw new Exception("baseline "+caller+" "+r.Fault);
   result.Add(new {caller,seed,terrain,sleep,result=r});
 }
 return result;
}

int checks=0,groups=0,failures=0,cleanTurns=0;
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string name,Action run){groups++;try{run();Console.WriteLine("PASS "+name);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+" "+ex);}}
SourceBattleFrame Frame(string caller,uint seed=7,params string[] extra){
 var mons=Enumerable.Range(0,4).Select(i=>Mon("CLEFABLE",70,i==0?new[]{caller}.Concat(extra).ToArray():new[]{"HARDEN"}) with {SpeedStage=new[]{12,8,6,4}[i]}).ToArray();
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,
 SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),
 Field=new(){Terrain=WildBattleTerrain.Plain},
 Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray()};
}
SourceBattleFrame Body(SourceBattleFrame f,int slot,WildBattleCombatant body){var cell=f.Slots[slot];var key=cell.Member!.Value;var p=f.Parties[(int)key.Side];return f with{
 Slots=f.Slots.SetItem(slot,cell with{Body=body}),Parties=f.Parties.SetItem((int)key.Side,p with{Members=p.Members.SetItem(key.Index,Member(body))})};}
SourceFourActionResult Success(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid resulting identity "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Begin(SourceBattleFrame f){var choices=f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,
 c.Registers.Turn?.Lock?.Target ?? (catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot:c.Slot.Opposite))).ToImmutableArray();return Success(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices));}
SourceFourActionResult Next(SourceFourActionResult r)=>Success(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
SourceFourActionResult Finish(SourceFourActionResult r){for(int i=0;i<20&&r.State.FourTurn is not null&&r.State.Outcome==WildBattleOutcome.Ongoing;i++)r=Next(r);Check(r.CompletedCleanTurn,"complete four action turn");cleanTurns++;return r;}
void Atomic(SourceFourActionResult start,SourceFourActionResult result,string reason){Check(!result.Succeeded&&ReferenceEquals(start.State,result.State)&&result.Events.IsEmpty&&result.Trace.IsEmpty,reason+" "+result.Fault);}
Test("1024 extracted original C selector vectors preserve all source pools and register positions",()=>{
 int rows=0;var fullPool=new HashSet<int>();
 foreach(var row in File.ReadLines(Path.Combine(output,"source-vectors.csv"))){
  var parts=row.Split(',');var x=parts.Skip(1).Select(ulong.Parse).ToArray();rows++;
  if(parts[0]=="M"){
   var r=WildBattleSourceCallers.Metronome(new((uint)x[1]));Check(r.SourceMoveId==(int)x[2]&&r.Rng.Seed==x[3]&&r.Rng.Cursor==x[4],"Metronome C choice/state/cursor");if(x[4]==1)fullPool.Add(r.SourceMoveId!.Value);
  }else if(parts[0]=="R"){
   int mask=(int)x[0];ImmutableArray<int> hist=[(mask&1)!=0?33:0,(mask&2)!=0?45:0,0,(mask&8)!=0?39:0];
   var r=WildBattleSourceCallers.MirrorMove(2,(mask&4)!=0?52:0,hist,new((uint)x[2]));
   Check((r.SourceMoveId??119)==(int)x[3]&&r.Rng.Seed==x[4]&&r.Rng.Cursor==x[5]&&(r.SourceMoveId is null)==(x[6]==1),"Mirror C numeric source histories/direct priority/failure");
  }else{
   int mask=(int)x[0];var f=Frame("SLEEP_TALK",(uint)x[2],"GROWL","TACKLE");f=Body(f,0,f.Slots[0].Body! with{Status=3});
   if((mask&6)!=0){var blocked=new List<string>();if((mask&2)!=0)blocked.Add("GROWL");if((mask&4)!=0)blocked.Add("TACKLE");f=Body(f,3,Mon("CLEFABLE",70,blocked.ToArray()) with{Imprison=true,SpeedStage=4});}
   // The other actor's ordinary choice does not affect the caller's first action.
   var selections=f.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot:c.Slot.Opposite)).ToImmutableArray();
   var start=Success(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,selections));var r=Next(start);var selection=r.Events.OfType<SourceCallerSelectionEvent>().SingleOrDefault();
   Check((selection is null?0:catalog.Moves[selection.Result!.Value].SourceMoveId)==(int)x[3],"SleepTalk C selected source move");
   if(selection is not null)Check(selection.RngCursor==x[6]&&r.Trace[(int)x[6]-1].After==(uint)x[5]&&r.Events.OfType<SourceSleepTalkEvent>().Single().CalledSlot==(int)x[4],"SleepTalk C rejection trace and current position");
   else Check(r.Trace.IsEmpty,"SleepTalk all-limited no RNG");
  }
 }
 Check(rows==1024&&fullPool.Count==336,"all extracted vectors and all 336 original Metronome choices");
});
Test("immutable pre-edit canonical singles caller results",()=>{
 Check(Environment.Version.Major==10,"actual .NET 10");
 var before=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../..","baseline-a51e8a6.json"));
 Check(Json(SinglesCorpus())==before,"all 16 complete singles state/event/RNG results unchanged");
});
Test("Metronome shared leaf keeps full target and PP ownership in genuine four action turn",()=>{
 var f=Frame("METRONOME",Seed(33));var start=Begin(f);Check(start.Trace.IsEmpty,"distinct speeds retain selected seed");var r=Next(start);
 var transition=r.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation;
 Check(transition.OriginalChosenMove==Id("METRONOME")&&transition.ChosenMove==Id("METRONOME")&&transition.CurrentMove==Id("TACKLE")&&transition.CalledMove is null,"distinct invocation registers");
 Check(transition.Actor.Value==0&&transition.PpOwner.Value==0&&transition.PpSlot==0&&transition.ChosenSlot==0,"original chosen PP slot and owner");
 var pp=r.Events.OfType<SourcePpCommandEvent>().Single();Check(pp.CurrentMove==Id("TACKLE")&&pp.PpOwner.Value==0&&pp.Before-pp.After==1,"one leaf PP command");
 Check(r.State.Slots[0].Body!.LastUsedMove==Id("METRONOME")&&r.State.Slots[0].Body!.LastCalledMove==Id("TACKLE"),"caller and resulting move history");
 Check(r.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==30,"duplicate species partner not charged");Finish(r);
});
Test("Choice Band locks the chosen caller while the selected shared leaf executes",()=>{
 var f=Frame("METRONOME",Seed(33));f=Body(f,0,f.Slots[0].Body! with{HeldItem="ITEM_CHOICE_BAND",SourceChoiceMove=Id("METRONOME")});
 var r=Next(Begin(f));var transition=r.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation;
 Check(transition.CurrentMove==Id("TACKLE")&&r.Events.OfType<SourceHpChangedEvent>().Any(),"called leaf executes despite different Choice move identity");
 Check(r.State.Slots[0].Body!.SourceChoiceMove==Id("METRONOME")&&r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==9,"Choice and PP belong to selected Metronome");Finish(r);
});
Test("called Bide retains caller PP through continuation and called Perish has no invented learned slot",()=>{
 var first=Finish(Next(Begin(Frame("METRONOME",Seed(117)))));
 var locked=first.State.Slots[0].Registers.Turn!.Lock!;
 Check(locked.Kind==WildBattleCalledLockKind.Bide&&locked.PpSlotMove==Id("METRONOME"),"called Bide uses original learned slot");
 var second=Finish(Next(Begin(first.State)));var release=Next(Begin(second.State));
 Check(release.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="bide-no-energy")&&!release.Events.OfType<SourceCallerSelectionEvent>().Any(),"continuation releases without redrawing caller");
 var end=Finish(release);Check(end.State.Slots[0].Registers.Turn?.Lock is null&&end.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==9,"Bide clears and charges caller once across three turns");
 var f=Frame("METRONOME",Seed(195));f=Body(f,1,Mon("DUSCLOPS",70,"HARDEN") with{SpeedStage=8});f=Body(f,3,Mon("DUSCLOPS",70,"HARDEN") with{SpeedStage=4});
 var song=Next(Begin(f));Check(song.State.Slots.All(c=>c.Body!.CalledVolatile?.PerishSongTimer==3),"called Perish sets all four positions");
 Check(song.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==9&&song.Events.OfType<SourcePpCommandEvent>().Count()==1,"Pressure does not invent an absent learned Perish slot");Finish(song);
});
Test("all ten Nature Power terrain paths execute shared single and spread leaves",()=>{
 foreach(var terrain in Enum.GetValues<WildBattleTerrain>().Where(t=>t<=WildBattleTerrain.Plain)){
  var f=Frame("NATURE_POWER") with {Field=new(){Terrain=terrain}};var r=Next(Begin(f));
  var inv=r.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation;
  Check(catalog.Moves[inv.CurrentMove].SourceMoveId==WildBattleSourceCallers.NaturePower(terrain)&&inv.CalledMove is null,"authentic terrain result "+terrain);
  Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==19,"caller PP exactly one "+terrain);
  Check(r.Events.OfType<SourceHpChangedEvent>().All(e=>e.Move==inv.CurrentMove),"leaf damage identity");
  if(catalog.Moves[inv.CurrentMove].Target=="MOVE_TARGET_BOTH")Check(r.Events.OfType<SourceHpChangedEvent>().Select(e=>e.Target.Value).Distinct().Count()==2,"each foe once, no recursive duplicate spread");
  Finish(r);
 }
});
Test("Assist nested caller preserves actual party index and duplicate weighted pool",()=>{
 var f=Frame("ASSIST");f=Body(f,2,Mon("CLEFABLE",70,"NATURE_POWER") with {SpeedStage=6});
 var dead=Member(Mon("CLEFABLE",70,"TACKLE"));dead=dead with {Pokemon=dead.Pokemon with{CurrentHp=0,Moves=dead.Pokemon.Moves.Select(m=>m with{CurrentPp=0}).ToImmutableArray()}};
 f=f with{Parties=f.Parties.SetItem(0,f.Parties[0] with{Members=f.Parties[0].Members.Add(dead).Add(Member(Mon("CLEFABLE",70,"TACKLE"))).Add(Member(Mon("CLEFABLE",70,"THUNDERBOLT")) with{IsEgg=true})})};
 var r=Next(Begin(f));var select=r.Events.OfType<SourceCallerSelectionEvent>().First();Check(select.CandidateSourceMoveIds.SequenceEqual(new[]{267,33,33}),"party pool includes active partner, dead zeroPP duplicates, excludes egg and actual actor");Finish(r);
 var nested=Frame("METRONOME",Seed(274));nested=Body(nested,2,Mon("CLEFABLE",70,"NATURE_POWER") with {SpeedStage=6});r=Next(Begin(nested));
 var chain=r.Events.OfType<SourceCalledMoveTransitionEvent>().Select(e=>e.Invocation).ToArray();Check(chain.Select(i=>i.CurrentMove).SequenceEqual(new[]{Id("ASSIST"),Id("NATURE_POWER"),Id("SWIFT")}),"real three caller transition chain");
 Check(chain[^1].CalledMove==Id("NATURE_POWER")&&chain[^1].ChosenMove==Id("METRONOME")&&r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==9,"nested called/current/chosen and one PP");Finish(r);
});
Test("Mirror Move four-source fallback and empty failure retain source Pressure flags",()=>{
 var f=Frame("MIRROR_MOVE");f=f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Registers=new(){History=new(){LastTakenFrom=[null,Id("GROWL"),Id("TACKLE"),Id("TAIL_WHIP")]}}})};
 var r=Next(Begin(f));Check(r.Events.OfType<SourceCallerSelectionEvent>().Single().CandidateSourceMoveIds.SequenceEqual(new[]{45,33,39}),"all three source battlers in numeric order");Check(r.Trace[0].Purpose=="wild-battle.mirror-move-select","fallback draw");Finish(r);
 r=Next(Begin(Frame("MIRROR_MOVE")));Check(r.Trace.IsEmpty&&r.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="mirror-move-no-history","empty history normal no-RNG failure");Check(r.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==1,"failure exact PP");Finish(r);
});
Test("Sleep Talk compact movesets decrement sleep once and separate caller PP from selected position",()=>{
 foreach(int leafPp in new[]{0,40}){
  var f=Frame("SLEEP_TALK",7,"GROWL");var actor=f.Slots[0].Body!;f=Body(f,0,actor with{Status=3,Pokemon=actor.Pokemon with{Moves=actor.Pokemon.Moves.SetItem(1,actor.Pokemon.Moves[1] with{CurrentPp=leafPp})}});
  var r=Next(Begin(f));var chosen=r.Events.OfType<SourceSleepTalkEvent>().Single();Check(chosen.CalledSlot==1&&chosen.CalledMove==Id("GROWL"),"short moveset rejection selects sole source slot");
  Check(r.State.Slots[0].Body!.Status==2&&r.Events.OfType<SourceStatusChangedEvent>().Count()==1,"sleep cancellation once");
  Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==9&&r.State.Slots[0].Body!.Pokemon.Moves[1].CurrentPp==leafPp,"only Sleep Talk PP spent");
  var inv=r.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation;Check(inv.PpSlot==1&&inv.ChosenSlot==0&&inv.ChosenMove==Id("SLEEP_TALK")&&inv.CalledMove==Id("GROWL"),"source currMovePos changes, chosen slot preserved");
  if(leafPp==0)Check(r.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="no-pp","zeroPP remains candidate then fails late source canceller");
  else Check(r.Events.OfType<SourcePpCommandEvent>().Count()==3,"caller command and two zero-cost Growl target passes");
  Finish(r);
 }
 var empty=Frame("SLEEP_TALK");empty=Body(empty,0,empty.Slots[0].Body! with{Status=3});var failed=Next(Begin(empty));Check(failed.Trace.IsEmpty&&failed.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="no-callable-move","empty pool no random draw");Finish(failed);
});
Test("called charge retains source rewritten target and caller PP across two full turns",()=>{
 uint seed=Enumerable.Range(0,128).Select(i=>Seed(91+512*i)).First(s=>{
  var f=Frame("METRONOME",s);var selected=WildBattleSourceCallers.Metronome(f.Rng);
  return SourceMoveTargeting.ResolveCalled(f with{Rng=selected.Rng},new(0),catalog.Moves[Id("DIG")]).Target?.Value==3;
 });
 var r=Next(Begin(Frame("METRONOME",seed)));var locked=r.State.Slots[0].Registers.Turn!.Lock!;
 Check(locked.Kind==WildBattleCalledLockKind.Charge&&locked.Move==Id("DIG")&&locked.Target.Value==3&&locked.PpSlot==0&&locked.PpSlotMove==Id("METRONOME"),"called charge lock target/PP identity");
 Check(r.State.Queue.Single(a=>a.Slot.Value==0).Target?.Value==3,"GetMoveTarget retained slot updated");
 var ready=Finish(r);r=Next(Begin(ready.State));Check(!r.Events.OfType<SourceCallerSelectionEvent>().Any(),"charge release does not call Metronome again");
 Check(r.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==3&&r.State.Slots[0].Registers.Turn?.Lock is null,"same called target receives release");
 Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==9,"release does not charge caller twice");Finish(r);
});
Test("called target redirection and source Pressure count use actual battler slots",()=>{
 var f=Frame("METRONOME",Seed(85));f=Body(f,3,Mon("RHYHORN",70,"HARDEN") with{SpeedStage=4});
 Check(f.Slots[3].Body!.Pokemon.AbilityId=="ABILITY_LIGHTNING_ROD","authentic species ability");
 var r=Next(Begin(f));var target=r.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation.Target;
 Check(target.Value==3,"called Electric target redirects to other opposing flank");
 Check(r.Events.OfType<SourceAbilityActivatedEvent>().Count(e=>e.Ability=="ABILITY_LIGHTNING_ROD")==1,"called redirect announcement exactly once");Finish(r);
 f=Frame("METRONOME",Seed(33));f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{FollowMeTimer=1,FollowMeTarget=new(3)})};r=Next(Begin(f));
 Check(r.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation.Target.Value==3&&r.Trace.All(t=>!t.Purpose.Contains("target.called")),"Follow Me consumes no target selection draw");Finish(r);
 f=Frame("NATURE_POWER") with{Field=new(){Terrain=WildBattleTerrain.Water}};f=Body(f,1,Mon("DUSCLOPS",70,"HARDEN") with{SpeedStage=8});f=Body(f,3,Mon("DUSCLOPS",70,"HARDEN") with{SpeedStage=4});
 r=Next(Begin(f));Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==17,"called Surf debits caller by both actual Pressure opponents");Check(r.Events.OfType<SourcePpCommandEvent>().Count()==2,"two source per-target PP commands");Finish(r);
});
Test("unsupported full-pool result and missing terrain reject complete action atomically",()=>{
 var unsupported=catalog.Moves.Values.FirstOrDefault(m=>WildBattleSourceCallers.IsDirectMetronomeChoice(m.SourceMoveId)&&!SourceBattleKernel.SupportsRecipe(m));
 if(unsupported is not null){
  var gap=Begin(Frame("METRONOME",Seed(unsupported.SourceMoveId)));var failed=SourceFourActionTurns.Advance(catalog,gap.State,gap.Continuation!.Value);
  Atomic(gap,failed,"unsupported selected source leaf retained");Check(failed.Fault=="source_kernel_called_recipe_not_migrated_"+unsupported.Id.Value,"explicit actual leaf fault");
 }else Check(catalog.Moves.Values.Count(m=>WildBattleSourceCallers.IsDirectMetronomeChoice(m.SourceMoveId)&&SourceBattleKernel.SupportsRecipe(m))==336,"every original Metronome result has now migrated");
 var start=Begin(Frame("NATURE_POWER") with{Field=new()});Atomic(start,SourceFourActionTurns.Advance(catalog,start.State,start.Continuation!.Value),"missing terrain");
 start=Begin(Frame("METRONOME",Seed(33)) with{Rng=new(Seed(33),ulong.MaxValue)});Atomic(start,SourceFourActionTurns.Advance(catalog,start.State,start.Continuation!.Value),"RNG overflow");
 foreach(string history in new[]{"METRONOME","MIRROR_MOVE","NOT_A_MOVE"}){
  var f=Frame("MIRROR_MOVE");f=f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Registers=new(){History=new(){LastTakenMove=Id(history)}}})};
  start=Begin(f);Atomic(start,SourceFourActionTurns.Advance(catalog,start.State,start.Continuation!.Value),"impossible or missing mirror history cannot recurse");
 }
});
Test("called Explosion owns real self KO and reward pause with exact JSON resume",()=>{
 var f=Frame("METRONOME",Seed(153)) with{Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};var start=Begin(f);var r=Next(start);
 Check(r.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==0),"real caller self KO");
 Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==0&&r.State.Parties[0].Members[0].Pokemon.CurrentHp==0&&r.State.Queue.Single(a=>a.Slot.Value==0).Kind==SourceActionKind.Cancelled,"caller identity and permanent self faint");
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending,"real enemy KO reward boundary");
 var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;
 var direct=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var restored=SourceFourActionTurns.Advance(catalog,copy,r.Continuation!.Value);
 Check(Json(direct)==Json(restored)&&direct.Succeeded,"reward continuation deterministic after called selfKO: "+direct.Fault+" / "+restored.Fault);
});
File.WriteAllText(Path.Combine(output,"summary.json"),Json(new{groups,checks,failures,cleanTurns,runtime=Environment.Version.ToString()}));
Console.WriteLine($"{groups} groups; {checks} checks; {failures} failures; {cleanTurns} full four-action turns");
return failures==0&&groups>0?0:1;
