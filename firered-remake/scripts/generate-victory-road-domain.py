#!/usr/bin/env python3
"""Deterministically render bounded Domain declarations from authenticated generated source data."""
import argparse,hashlib,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('dependency',type=Path);p.add_argument('output',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
TEMPLATE_SHA256 = {'src/Game.Domain/Trainers/ViridianGymTrainerPartyFactory.cs': 'b899bd86ce10071485b36d18872dbcc17a680b4c8754f841cac88effeeb5dd7e', 'src/Game.Domain/Trainers/ViridianGymTrainerCatalog.cs': 'f44f28714c47687a8d4b4976b2ae8dade4399d02763f6957838531e3d0dfdda4', 'src/Game.Domain/Trainers/ViridianGymTrainerCombatRules.cs': 'b9f32bbe0b87d801b8f0af1e0181919f3fdf579c00281c89d0d57bdc54c94a1f', 'src/Game.Domain/Trainers/CinnabarDoubleTrainerEncounter.cs': '29e34252e22905557e5ecaf7da5af05394a75457dc4ff2f819bd5f69f0cd81c9'}
for name,expected in TEMPLATE_SHA256.items():
 if hashlib.sha256((a.dependency/name).read_bytes()).hexdigest()!=expected:raise ValueError("Reviewed template changed: "+name)
r=a.output;d=json.loads((r/'content/generated/victory-road-rules/trainers.json').read_text());wild=json.loads((r/'content/generated/victory-road-rules/wild.json').read_text())
def q(x):return json.dumps(x,ensure_ascii=False)
def arr(x):return '['+', '.join(q(s) for s in x)+']'
def fp(x):return hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
def write(name,s):
 path=r/name
 if a.check:
  if path.read_text()!=s:raise ValueError('Generated Domain differs: '+name)
 else:path.parent.mkdir(parents=True,exist_ok=True);path.write_text(s)
# Existing reviewed NPC factory controls source RNG; no duplicate battle executor.
template=(a.dependency/'src/Game.Domain/Trainers/ViridianGymTrainerPartyFactory.cs').read_text().replace('ViridianGym','VictoryRoad').replace('viridian_gym','victory_road').replace('firered-viridian-gym','firered-victory-road')
template=template.replace('Pinned nine-team, 29-member gym roster.','Pinned twelve-team, 45-member Victory Road roster.')
template=template.replace('    public static TrainerId Giovanni { get; } = new("pret:TRAINER_LEADER_GIOVANNI");\n','')
beg=template.index('    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected')
end=template.index('    public static int BasePrize',beg)
rows=[]
for t in d['trainers']:
 resolved=next(x for x in d['resolvedMoves'] if x['trainer']==t['id']['value'][5:])['party'];members=[]
 for m,rm in zip(t['party'],resolved):
  members.append('new('+','.join([q(m['species']['value']),str(m['sourceSpeciesId']),q(m['speciesName']),str(m['level']),str(m['sourceIv']),str(m['encodedSpeciesName']),arr(['pret:'+s for s in rm['moves']]),q(m['heldItem'])])+')')
 prize=(8 if t['doubleBattle'] else 4)*t['baseMoney']*t['party'][-1]['level']
 rows.append('        [new('+q(t['id']['value'])+')] = new('+','.join([q(t['sourceTrainerName']),str(t['encounterMusicGender']),str(t['aiFlags'])+'u',str(t['doubleBattle']).lower(),str(t['encodedTrainerName']),arr(t['items']),'['+','.join(members)+']',str(prize),q(fp(t))])+'),')
block='    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>\n    {\n'+'\n'.join(rows)+'\n    }.ToImmutableDictionary();\n'
block+='    private static readonly ImmutableDictionary<string,string> ExpectedBattles = new Dictionary<string,string>(StringComparer.Ordinal)\n    {\n'+'\n'.join('        ['+q(b['scriptEntry'])+'] = '+q(fp(b))+',' for b in d['battles'])+'\n    }.ToImmutableDictionary(StringComparer.Ordinal);\n'
block+='    public static ImmutableArray<TrainerId> AllTrainers { get; } = Expected.Keys.OrderBy(t=>t.Value,StringComparer.Ordinal).ToImmutableArray();\n    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(t=>!Expected[t].DoubleBattle).ToImmutableArray();\n'
template=template[:beg]+block+template[end:]
template=template.replace('catalog.Trainers.Count != 9 || catalog.BattlesByScript.Count != 9','catalog.Trainers.Count != 12 || catalog.BattlesByScript.Count != 13')
write('src/Game.Domain/Trainers/VictoryRoadTrainerPartyFactory.cs',template)
cat=(a.dependency/'src/Game.Domain/Trainers/ViridianGymTrainerCatalog.cs').read_text().replace('The nine original single battles in Viridian Gym, including leader Giovanni (350).','Twelve exact parties and thirteen source entries, including the shared Ray/Tyra double.').replace('ViridianGym','VictoryRoad')
write('src/Game.Domain/Trainers/VictoryRoadTrainerCatalog.cs',cat)
combat=(a.dependency/'src/Game.Domain/Trainers/ViridianGymTrainerCombatRules.cs').read_text().replace('ViridianGym','VictoryRoad').replace('viridian_gym','victory_road').replace('viridian-gym','victory-road').replace('WildBattleTerrain.Building','WildBattleTerrain.Cave').replace('Viridian Gym single','Victory Road single')
write('src/Game.Domain/Trainers/VictoryRoadTrainerCombatRules.cs',combat)
double=(a.dependency/'src/Game.Domain/Trainers/CinnabarDoubleTrainerEncounter.cs').read_text().replace('CinnabarDouble','VictoryRoadDouble').replace('CinnabarTrainer','VictoryRoadTrainer').replace('cinnabar_double','victory_road_double').replace('cinnabar-double','victory-road-double').replace('firered-cinnabar-double','firered-victory-road-double').replace('Exact Route21 Lil/Ian','Exact Victory Road Ray/Tyra').replace('TRAINER_SIS_AND_BRO_LIL_IAN','TRAINER_COOL_COUPLE_RAY_TYRA').replace('0x500 + 491','0x500 + 485').replace('VictoryMoney = 264','VictoryMoney = 2160').replace('WildBattleTerrain.Water','WildBattleTerrain.Cave')
write('src/Game.Domain/Trainers/VictoryRoadDoubleTrainerEncounter.cs',double)
# Typed table constants include every fishing slot; Old Rod is a separate bounded projection.
s='using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\n/// <summary>Four exact land tables, Route23 Surf, and all original fishing declarations.</summary>\npublic static class VictoryRoadEncounterTables\n{\n'
for name,typ,tables in [('Land','EarlyLandEncounterTable',wild['land']),('Water','WaterEncounterTable',wild['water']),('FishingSource','OldRodEncounterTable',wild['fishing'])]:
 s+='    public static ImmutableArray<'+typ+'> '+name+' { get; } = [\n'
 for t in tables:
  fields=['new('+q(t['map']['value'])+')']+([q(t['sourceMapSymbol'])] if name=='Water' else [])+['EarlyLandEncounterRules.SourceCommit','EarlyLandEncounterRules.SourcePath',q(t['sourceSymbol']),q(t['sourceSha256']),str(t['rawEncounterRate'])]
  fields+=['['+','.join('new('+','.join([str(x['index']),'new('+q(x['species']['value'])+')',str(x['minimumLevel']),str(x['maximumLevel']),str(x['weight'])])+')' for x in t['slots'])+']']
  s+='        new('+','.join(fields)+'),\n'
 s+='    ];\n'
s+='    public static bool IsCave(MapId map) => Land.Any(t=>t.Map==map && map.Value.StartsWith("map.victory-road-",StringComparison.Ordinal));\n}\n'
write('src/Game.Domain/Encounters/VictoryRoadEncounterTables.cs',s)
# Exact default set matrix, generated source rows rather than fallback moves or PP filtering.
s='using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\npublic sealed record VictoryRoadInitialMoves(SpeciesId Species, byte Level, ImmutableArray<MoveId> Moves);\npublic sealed record VictoryRoadWildDependency(SpeciesId Species, int SourceSpeciesId, string Ability1, string Ability2, string ItemCommon, string ItemRare);\n\npublic static class VictoryRoadWildSource\n{\n    public static ImmutableArray<VictoryRoadInitialMoves> InitialMoves { get; } = [\n'
for row in wild['initialMoves']:s+='        new(new('+q(row['species']['value'])+'),'+str(row['level'])+',['+','.join('new('+q(m['value'])+')' for m in row['moves'])+']),\n'
s+='    ];\n    public static ImmutableArray<VictoryRoadWildDependency> Species { get; } = [\n'
for row in wild['species']:s+='        new(new('+q(row['species']['value'])+'),'+str(row['sourceSpeciesId'])+','+','.join(q(row[k]) for k in ['ability1','ability2','itemCommon','itemRare'])+'),\n'
s+='    ];\n}\n'
write('src/Game.Domain/Encounters/VictoryRoadWildSource.cs',s)
print(json.dumps({'checked':a.check,'trainerParties':12,'members':45,'sourceBattles':13,'defaultMoveSets':122,'sourceWildSpecies':19}))
