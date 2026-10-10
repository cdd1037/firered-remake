using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Game.Domain.Trainers;
if(args.Length!=2)throw new ArgumentException("CURRENT_PROJECT_ROOT BASELINE_1579DFC_JSON");
string root=args[0];
var raw=PretRocketHideoutPokemon.Load(root).Catalog;
var extra=PretVermilionGymTrainers.ExtendPokemon(root,PretCeladonApproachPokemon.Load(root).Catalog,celadonApproachRules:true);
var tower=PretPokemonTowerPokemon.Load(raw with{Moves=raw.Moves.SetItems(extra.Moves)},root,root).Catalog;
var snorlax=PretSnorlaxPokemon.Load(tower,root).Catalog;
var eevee=PretEeveePokemon.Load(snorlax,root).Catalog;
var prior=PretFuchsiaRoutePokemon.Load(eevee,root).Catalog;
var current=PretMetronomeMoveCatalog.Load(root).Extend(prior);
string Hash(object value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
var results=new List<object>();
string Read(string name)=>File.ReadAllText(Path.Combine(root,"content/generated/"+name+"/catalog.json"));
var starter=PretPokemonCatalog.Load(File.ReadAllText(Path.Combine(root,"content/generated/pokemon/starter-catalog.json")));
var route1=PretRoute1PokemonCatalog.Load(Read("route1-pokemon"));
var oldman=PretOldManTutorial.LoadContent(Read("route1-pokemon"),Read("old-man-tutorial")).Catalog;
var forest=PretForestPokemon.LoadContent(Read("route1-pokemon"),Read("old-man-tutorial"),Read("forest-pokemon")).Catalog;
var pewter=PretPewterPokemon.LoadContent(Read("route1-pokemon"),Read("old-man-tutorial"),Read("forest-pokemon"),Read("pewter-pokemon")).Catalog;
foreach(var catalog in new[]{starter,route1,oldman,forest,pewter,raw,tower,snorlax,eevee,prior,current})
{
    var instances=new List<object>();
    foreach(var species in catalog.Species.Values.OrderBy(s=>s.SourceSpeciesId))
    foreach(int level in new[]{1,5,20,37,100})
    {
        var result=PokemonFactory.Create(catalog,new(species.Id,level,0x12345678,IvPolicy.Random,PersonalityPolicy.Random),new(0xdeadbeef));
        if(PokemonStateValidator.Validate(catalog,result.Pokemon)is not null)throw new Exception("old validator");
        instances.Add(result);
    }
    results.Add(new{species=catalog.Species.Count,moves=catalog.Moves.Count,creationCount=instances.Count,hash=Hash(instances)});
}
PokemonInstance Mon(string name,string move)=>PokemonFactory.Create(current,new(new("pret:SPECIES_"+name),37,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon with{Moves=[new(new("pret:MOVE_"+move),current.Moves[new("pret:MOVE_"+move)].Pp,current.Moves[new("pret:MOVE_"+move)].Pp)]};
var entry=WildBattleRules.StartSourceWild(current,[new(Mon("RATTATA","TACKLE"))],0,new(Mon("DITTO","TRANSFORM")),new(5));
if(!entry.Succeeded)throw new Exception(entry.Fault?.ToString());
var snapshots=new List<object>{entry};var state=entry.State!;
for(int i=0;i<3;i++){var turn=WildBattleRules.ResolveMove(current,state,0);if(!turn.Succeeded)throw new Exception(turn.Fault?.ToString());snapshots.Add(turn);state=turn.State!;}
results.Add(new{sourceTurns=3,hash=Hash(snapshots),state=Hash(state),state.Rng});
var trainers=PretForestTrainers.Load(root);
var starterMon=PokemonFactory.Create(current,new(new("pret:SPECIES_SQUIRTLE"),5,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
var trainerEntry=ForestTrainerCombatRules.StartSource(trainers,current,ForestTrainerPartyFactory.SupportedTrainers[0],[new(starterMon)],0,new(7));
if(!trainerEntry.Succeeded)throw new Exception(trainerEntry.Fault?.ToString());
var ai=SourceTrainerAi.Choose(current,trainerEntry.State!);
if(!ai.Succeeded)throw new Exception(ai.Diagnostic?.Code);
var trainerTurn=ForestTrainerCombatRules.ResolveMove(current,trainerEntry.State!,0);
if(!trainerTurn.Succeeded)throw new Exception(trainerTurn.Fault?.ToString());
results.Add(new{sourceTrainer="BUG_CATCHER_RICK",hash=Hash(new object[]{trainerEntry,ai,trainerTurn}),state=Hash(trainerTurn.State!),trainerTurn.State!.Battle.Rng});
string actual=JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true});
string expected=File.ReadAllText(args[1]).TrimEnd();
Console.WriteLine(actual);
if(!string.Equals(actual,expected,StringComparison.Ordinal))throw new Exception("Historical creation/state/event/RNG hashes differ from the frozen 1579dfc process output.");
