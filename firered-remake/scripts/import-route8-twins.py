#!/usr/bin/env python3
"""Bounded Twins import. Reads Git objects from the pinned tree; never executes upstream code.
Reuses the authenticated 23-file Celadon trainer inventory, avoiding duplicate raw source copies.
"""
import argparse,  json, re, subprocess
from pathlib import Path
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE='firered-route8-twins-encounter-v1'
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',required=True);ap.add_argument('--output-root',required=True);a=ap.parse_args()
    root=Path(a.output_root); source=Path(a.source)
    def read(path): return subprocess.check_output(['git','-C',str(source),'show',f'{COMMIT}:{path}'])
    files={p:read(p) for p in ['src/data/trainers.h','src/data/trainer_parties.h','src/data/text/species_names.h','src/battle_main.c','src/battle_setup.c','data/scripts/trainers.inc','include/constants/trainers.h','include/constants/opponents.h','charmap.txt','src/data/pokemon/level_up_learnsets.h']}
    def ref(path,symbol,pattern):
        text=files[path].decode();m=re.search(pattern,text,re.S);assert m,(path,symbol)
        return dict(path=path,symbol=symbol,startLine=text[:m.start()].count('\n')+1,endLine=text[:m.end()].count('\n')+1)
    def encode(s):
        charmap=files['charmap.txt'].decode();return [int(re.search(r"^'"+re.escape(c)+r"'\s*= ([0-9A-F]{2})$",charmap,re.M).group(1),16) for c in s]
    t=ref('src/data/trainers.h','TRAINER_TWINS_ELI_ANNE',r'    \[TRAINER_TWINS_ELI_ANNE\] = \{.*?\n    \},')
    partyref=ref('src/data/trainer_parties.h','sParty_TwinsEliAnne',r'static const struct TrainerMonNoItemDefaultMoves sParty_TwinsEliAnne\[\] = \{.*?\n\};')
    body=files['src/data/trainers.h'].decode().split('[TRAINER_TWINS_ELI_ANNE] = {',1)[1].split('\n    },',1)[0]
    for exact in ['.trainerClass = TRAINER_CLASS_TWINS,','.encounterMusic_gender = TRAINER_ENCOUNTER_MUSIC_TWINS,','.trainerPic = TRAINER_PIC_TWINS,','.trainerName = _("ELI & ANNE"),','.items = {},','.doubleBattle = TRUE,','.aiFlags = AI_SCRIPT_CHECK_BAD_MOVE,','.party = NO_ITEM_DEFAULT_MOVES(sParty_TwinsEliAnne),']: assert exact in body,exact
    partybody=files['src/data/trainer_parties.h'].decode().split('sParty_TwinsEliAnne[] = {',1)[1].split('\n};',1)[0]
    assert re.findall(r'\.iv = (\d+),\s*\.lvl = (\d+),\s*\.species = SPECIES_(\w+),',partybody)==[('0','22','CLEFAIRY'),('0','22','JIGGLYPUFF')]
    moneyref=ref('src/battle_main.c','TRAINER_CLASS_TWINS',r'    \{TRAINER_CLASS_TWINS, 3\},')
    assert 'if (gTrainers[trainerNum].doubleBattle == TRUE)\n                personalityValue = 0x80;' in files['src/battle_main.c'].decode()
    assert re.search(r'case TRAINER_ENCOUNTER_MUSIC_TWINS:\s*music = MUS_ENCOUNTER_GIRL;',files['src/battle_setup.c'].decode())
    party=[]
    for name,sid in [('CLEFAIRY',35),('JIGGLYPUFF',39)]:
        nref=ref('src/data/text/species_names.h','SPECIES_'+name,r'    \[SPECIES_'+name+r'\] = _\("'+name+r'"\),')
        party.append(dict(species={'value':'pret:SPECIES_'+name},sourceSpeciesId=sid,speciesName=name,encodedSpeciesName=encode(name),level=22,sourceIv=0,heldItem='ITEM_NONE',customMoves=[],source=partyref,speciesNameSource=nref))
    trainer=dict(id={'value':'pret:TRAINER_TWINS_ELI_ANNE'},sourceTrainerId=484,trainerClass='TRAINER_CLASS_TWINS',sourceTrainerClass=92,trainerPic='TRAINER_PIC_TWINS',sourceTrainerPic=127,sourceTrainerName='ELI & ANNE',encodedTrainerName=encode('ELI & ANNE'),encounterMusicGender=9,items=['ITEM_NONE']*4,doubleBattle=True,partyFlags=0,aiFlags=1,aiFlagSymbols=['AI_SCRIPT_CHECK_BAD_MOVE'],baseMoney=3,party=party,source=t,moneySource=moneyref)
    interactions=[]
    for name in ['Eli','Anne']:
        script=f'Route8_EventScript_{name}'; text=files['data/scripts/trainers.inc'].decode();block=text.split(script+'::',1)[1].split('\n\n',1)[0]
        assert f'trainerbattle_double TRAINER_TWINS_ELI_ANNE, Route8_Text_{name}Intro, Route8_Text_{name}Defeat, Route8_Text_{name}NotEnoughMons' in block
        interactions.append(dict(scriptEntry=script,introText=f'Route8_Text_{name}Intro',defeatText=f'Route8_Text_{name}Defeat',postBattleText=f'Route8_Text_{name}PostBattle',notEnoughMonsText=f'Route8_Text_{name}NotEnoughMons',source=ref('data/scripts/trainers.inc',script,script+r'::.*?\n\tend')))
    d=dict(sourceCommit=COMMIT,rulesVersion=PROFILE,trainer=trainer,interactions=interactions,entryScript='EventScript_TryDoDoubleTrainerBattle',callback='CB2_EndTrainerBattle',battleMode=4,battleTypeFlags=9,encounterMusic='MUS_ENCOUNTER_GIRL',encounterMusicId=284,trainerFlag=0x500+484,victoryMoney=4*22*2*3)
    dest=root/'content/generated/route8-twins/trainer.json';dest.parent.mkdir(parents=True,exist_ok=True);raw=(json.dumps(d,ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n').encode();dest.write_bytes(raw)
    print(json.dumps(dict(path=str(dest),sourceCommit=COMMIT)))
if __name__=='__main__':main()
