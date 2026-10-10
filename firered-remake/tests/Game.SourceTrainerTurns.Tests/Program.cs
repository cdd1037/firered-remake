using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
if(args.Length is not (1 or 2))throw new ArgumentException("root [--boundary]");
string root=Path.GetFullPath(args[0]);int checks=0,groups=0,failed=0,turns=0,called=0,commands=0;
void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
void Test(string name,Action run){groups++;try{run();Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+" "+ex);}}
var raw= PretRocketHideoutPokemon.Load(root).Catalog;
var extra=PretVermilionGymTrainers.ExtendPokemon(root,PretCeladonApproachPokemon.Load(root).Catalog,celadonApproachRules:true);
var prior=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(raw with{Moves=raw.Moves.SetItems(extra.Moves)},root,root).Catalog,root).Catalog,root).Catalog;
var route=PretFuchsiaRoutePokemon.Load(prior,root);var catalog=PretMetronomeMoveCatalog.Load(root).Extend(route.Catalog);
var loaded=PretFuchsiaGymTrainers.Load(root);var gym=new FuchsiaGymTrainerCatalog(loaded.SourceCommit,loaded.Profile,loaded.CompleteSourceRoster,loaded.Trainers,loaded.Battles);
MoveId Id(string name)=>new("pret:MOVE_"+name);
WildBattleCombatant Player(int index)=>new(PokemonFactory.Create(catalog,new(new("pret:SPECIES_CLEFAIRY"),50,42,
    IvPolicy.Fixed(31),PersonalityPolicy.Fixed((uint)(index*3))),new(0)).Pokemon with{Moves=[new(Id("METRONOME"),10,10),new(Id("TACKLE"),35,35)]});
var players=Enumerable.Range(0,3).Select(Player).ToImmutableArray();
ForestTrainerCombatState Require(ForestTrainerCombatResult result,string label)
{
 commands++;Check(result.Succeeded,label+" fault="+result.Fault);
 var state=result.State!;var validation=ForestTrainerCombatRules.Validate(catalog,state);
 if(validation is not null)File.WriteAllText(Path.Combine(root,"tests/Game.SourceTrainerTurns.Tests/evidence/last-invalid.json"),JsonSerializer.Serialize(state));
 Check(validation is null,label+" validation="+validation);
 called+=result.Events.Count(e=>e is WildBattleCalledMoveTransitionEvent);
 return state;
}
ForestTrainerCombatState Resume(ForestTrainerCombatState state)
{
 for(int n=0;n<24 && state.Phase!=ForestTrainerCombatPhase.Ready && state.Phase!=ForestTrainerCombatPhase.Ended;n++)
 {
  bool reserves=state.HasPlayerReserves;
  if(state.Phase==ForestTrainerCombatPhase.AwaitingExperience)
   state=Require(ForestTrainerCombatRules.CompleteOpponentExperience(catalog,state,WildBattleTransformationRules.PersistentPokemon(state.Battle.Player),reserves),"EXP");
  else if(state.Phase==ForestTrainerCombatPhase.AwaitingShiftChoice)
   state=Require(ForestTrainerCombatRules.ChooseShift(catalog,state,null,reserves),"SHIFT no");
  else if(state.Phase==ForestTrainerCombatPhase.AwaitingPlayerReplacement)
  {
   var party=state.Battle.CalledParty!;int next=WildBattleSourcePartyEffects.LegalReserveIndices(party,WildBattleSide.Player)[0];var m=party.Player[next];
   var incoming=new WildBattleCombatant(m.Pokemon,HeldItem:WildBattleSourcePartyEffects.PersistentHeldItem(m),Status:m.Status);
   state=Require(ForestTrainerCombatRules.ReplaceFaintedPlayer(catalog,state,incoming,reserves,next),"replace");
  }
 }
 return state;
}
if(args.Length==1)foreach(string name in new[]{"BEAUTY_SHEILA","PICNICKER_CELIA","JUGGLER_KIRK","JUGGLER_NATE","LEADER_KOGA"})Test("source turns "+name,()=>
{
 for(uint seed=0;seed<16;seed++)
 {
  TrainerId trainer=new("pret:TRAINER_"+name);
  var entry=name.StartsWith("BEAUTY")||name.StartsWith("PICNICKER")?ForestTrainerCombatRules.StartSource(route.Trainers,catalog,trainer,players,0,new(seed),ForestTrainerBattleStyle.Set):
   ForestTrainerCombatRules.Start(gym,catalog,trainer,players,0,new(seed),ForestTrainerBattleStyle.Set);
  var state=Require(entry,"entry "+name+" seed"+seed);
  for(int n=0;n<8 && state.Phase!=ForestTrainerCombatPhase.Ended;n++)
  {
   state=Resume(state);
   if(state.Phase==ForestTrainerCombatPhase.Ended)break;
   if(state.Battle.CalledParty?.PendingSwitch is {Kind:WildBattleCalledSwitchKind.BatonPass} pending)
   {state=Require(ForestTrainerCombatRules.ChooseSourceParty(catalog,state,pending.LegalPartyIndices[0]),"Baton");continue;}
   int slot=state.Battle.Player.CalledTurnState?.Lock?.PpSlot??WildBattleRules.ForcedMoveSlot(state.Battle.Player)??Enumerable.Range(-1,state.Battle.Player.Pokemon.Moves.Length+1).First(i=>WildBattleRules.ValidateSourceMoveSelection(catalog,state.Battle,WildBattleSide.Player,i)is null);
   var result=ForestTrainerCombatRules.ResolveMove(catalog,state,slot,state.HasPlayerReserves);
   if(!result.Succeeded)File.WriteAllText(Path.Combine(root,"tests/Game.SourceTrainerTurns.Tests/evidence/last-fault.json"),JsonSerializer.Serialize(new{name,seed,n,slot,result.Fault,state}));
   state=Require(result,"turn "+name+" seed"+seed+" n"+n);turns++;
  }
 }
});
Test("Baton Pass slot3 into a one-move actual reserve",()=>
{
 var lead=Player(0) with{Pokemon=Player(0).Pokemon with{Moves=[new(Id("TACKLE"),35,35),new(Id("GROWL"),40,40),new(Id("SPLASH"),40,40),new(Id("BATON_PASS"),40,40)]}};
 var reserve=Player(1) with{Pokemon=Player(1).Pokemon with{Moves=[new(Id("TACKLE"),35,35)]}};
 var state=Require(ForestTrainerCombatRules.Start(gym,catalog,new("pret:TRAINER_JUGGLER_KIRK"),[lead,reserve],0,new(42),ForestTrainerBattleStyle.Set),"Baton entry");
 state=Require(ForestTrainerCombatRules.ResolveMove(catalog,state,3,true),"Baton actual first action");
 Check(state.Battle.CalledParty?.PendingSwitch is{Side:WildBattleSide.Player,Kind:WildBattleCalledSwitchKind.BatonPass},"public replacement choice pending");
 state=Require(ForestTrainerCombatRules.ChooseSourceParty(catalog,state,1),"Baton complete remaining turn into shorter moveset");
 Check(state.Battle.Player.Pokemon.Personality==reserve.Pokemon.Personality&&state.CeruleanPlayerPartyIndex==1,"actual chosen reserve active");
 Check(state.Battle.CalledParty!.Player[0].Pokemon.Moves[3].CurrentPp==39&&state.Battle.Player.Pokemon.Moves[0].CurrentPp==35,"outgoing Baton consumes one PP; incoming never repeats outgoing slot");
 Check(state.SourceTurn is null&&state.Phase==ForestTrainerCombatPhase.Ready,"source endturn completes once with missing source move slot treated as MOVE_NONE");
 state=Require(ForestTrainerCombatRules.ResolveMove(catalog,state,0,true),"next real reserve turn");
 Check(state.Battle.CalledParty!.Player[1].Pokemon.Moves[0].CurrentPp==34,"next turn uses actual reserve action");
});
Console.WriteLine(JsonSerializer.Serialize(new{passed=failed==0,groups,checks,failed,turns,commands,called,runtime=Environment.Version.ToString()}));
return failed==0?0:1;
