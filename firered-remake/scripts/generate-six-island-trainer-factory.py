"""Generate pinned Six Island trainer identities and party expectations; no upstream execution."""
import argparse,json
from pathlib import Path

def q(v):return json.dumps(v,ensure_ascii=False)
def arr(v):return '['+', '.join(q(x) if isinstance(x,str) else str(x) for x in v)+']'
def generate(root):
 d=json.loads((root/'content/generated/six-island-rules/trainers.json').read_text()); t=d['trainers'];b=d['battles'];resolved={r['trainer']:r['party'] for r in d['resolvedMoves']}
 out='''using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Original eleven first teams, including one Miu/Mia double, through shared source construction.</summary>
public static class SixIslandTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-six-island-trainers-v1";
    public static TrainerId DoubleTrainer { get; } = new("pret:TRAINER_TWINS_MIU_MIA");
    public static readonly ImmutableHashSet<TrainerId> TrainerIds = new HashSet<TrainerId>
    {
'''
 for x in t:out+=f'        new({q(x["id"]["value"])}),\n'
 out+='''    }.ToImmutableHashSet();
    public static readonly ImmutableHashSet<string> BattleScripts = new HashSet<string>
    {
'''
 for x in b:out+=f'        {q(x["scriptEntry"])},\n'
 out+='''    }.ToImmutableHashSet(StringComparer.Ordinal);
    public static ImmutableArray<TrainerId> AllTrainers { get; } = ['''+', '.join('new('+q(x['id']['value'])+')' for x in t)+'''];
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(id => id != DoubleTrainer).ToImmutableArray();
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
'''
 for x in t:
  party=[]
  for p,m in zip(x['party'],resolved[x['id']['value'][5:]]):party.append('new('+','.join([q(p['species']['value']),str(p['sourceSpeciesId']),q(p['speciesName']),str(p['level']),str(p['sourceIv']),arr(p['encodedSpeciesName']),arr(['pret:'+v for v in m['moves']]),q(p['heldItem'])])+')')
  args=[q(x['sourceTrainerName']),str(x['encounterMusicGender']),str(x['aiFlags'])+'u',str(x['doubleBattle']).lower(),arr(x['encodedTrainerName']),arr(x['items']),'['+','.join(party)+']',str((8 if x['doubleBattle'] else 4)*x['baseMoney']*x['party'][-1]['level'])]
  out+='        [new('+q(x['id']['value'])+')] = new('+','.join(args)+'),\n'
 out+='''    }.ToImmutableDictionary();
    public static int BasePrize(SixIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("six_island_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(SixIslandTrainerCatalog trainers,
        SixIslandOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(SixIslandOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("six_island_trainer_catalog");
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("six_island_trainer_species");
            if (member.CustomMoves.Any(m => !opponents.Mechanics.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("six_island_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.Where(m => m.Value != "pret:MOVE_NONE").Select(m => new PokemonMoveSlot(m,
                opponents.Mechanics.Catalog.Moves[m].Pp, opponents.Mechanics.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(SixIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 11 || catalog.BattlesByScript.Count != 12)
            throw new NotSupportedException("six_island_trainer_scope");
        foreach (var id in TrainerIds)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.Party.IsDefaultOrEmpty || trainer.Party.Any(m => m is null || m.CustomMoves.IsDefault || m.EncodedSpeciesName.IsDefaultOrEmpty) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefaultOrEmpty || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("six_island_trainer_source");
        foreach (var script in BattleScripts)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script)
                throw new NotSupportedException("six_island_trainer_battle_source");
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("six_island_trainer_battle_source");
    }
}
'''
 return out
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
 path=a.root/'src/Game.Domain/Trainers/SixIslandTrainerPartyFactory.cs';data=generate(a.root)
 if a.check:assert path.read_text()==data,'Trainer source generation differs'
 else:path.write_text(data)
 print(json.dumps(dict(checked=a.check)))
