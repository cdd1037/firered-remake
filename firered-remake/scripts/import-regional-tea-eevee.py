#!/usr/bin/env python3
"""Read-only fixed-source extraction. Never execute reference scripts or fetch remote bytes."""
import argparse,  json, pathlib, re
ROOT=pathlib.Path(__file__).resolve().parents[1]
LOCK=ROOT/'scripts/regional-tea-eevee.lock.json'
def canonical(value): return (json.dumps(value,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode()
def safe(root,path):
    p=pathlib.PurePosixPath(path)
    if p.is_absolute() or any(x in ('','..','.') for x in path.split('/')): raise ValueError('unsafe path')
    full=root.joinpath(*p.parts)
    if any(q.is_symlink() for q in (full,*full.parents)): raise ValueError('symlink unsupported')
    return full

def main():
    p=argparse.ArgumentParser();p.add_argument('--source',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    rawlock=LOCK.read_bytes()
    lock=json.loads(rawlock); raw={}
    for row in lock['files']:
        source=safe(a.source if a.source else ROOT/'content/raw/pokefirered/037335f4',row['path']);b=source.read_bytes()
        b.decode('utf8');raw[row['path']]=b
    files=lock['files']
    constants={}
    names=['FLAG_GOT_TEA','FLAG_GOT_EEVEE','FLAG_HIDE_EEVEE_BALL','FLAG_TALKED_TO_TEA_LADY_AFTER_HOF','VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES','ITEM_TEA','SPECIES_EEVEE']
    for path in ['include/constants/flags.h','include/constants/vars.h','include/constants/items.h','include/constants/species.h']:
        for name,value in re.findall(r'^#define\s+(\w+)\s+(0x[0-9a-fA-F]+|\d+)\b',raw[path].decode(),re.M):
            if name in names:constants[name]=int(value,0)
    assert len(constants)==len(names)
    tea=json.loads(raw['src/data/items.json'])
    # Source document is an object whose items array contains the original item records.
    item=next(x for x in tea['items'] if x.get('itemId')=='ITEM_TEA')
    contract=dict(schemaVersion=1,profile=lock['profile'],sourceCommit=lock['sourceCommit'],gameplayEnabled=False,sourceFiles=files,constants=constants,tea=dict(item=item,map='CeladonCity_Condominiums_1F',entryScript='CeladonCity_Condominiums_1F_EventScript_TeaWoman',flagBeforeInventory=True,fullBagKeepsFlag=True,repeatAfterFullBagGivesNoItem=True,yesNoPrompt=False,gateChecksFlagNotPossession=True,gateRemovalFailureIgnored=True,sharedGateScene=1),eevee=dict(species=133,level=25,map='CeladonCity_Condominiums_RoofRoom',entryScript='CeladonCity_Condominiums_RoofRoom_EventScript_EeveeBall',metLocation='MAPSEC_CELADON_CITY',rngDrawsBeforeCapacity=4,partyResult=0,boxResult=1,fullResult=2,hideBallAfterSuccessBeforeNickname=True,gotFlagAfterNickname=True,fullStorageKeepsBall=True,fullStorageKeepsIdAndDex=True,initialMoves=['MOVE_HELPING_HAND','MOVE_SAND_ATTACK','MOVE_GROWL','MOVE_QUICK_ATTACK']),gates=['Route5_SouthEntrance','Route6_NorthEntrance','Route7_EastEntrance','Route8_WestEntrance'],commonScripts=['EventScript_GetGiftMonPartySlot','EventScript_NameReceivedBoxMon','EventScript_TransferredToPC','EventScript_TransferredSomeonesPC','EventScript_TransferredSomeonesPCBoxFull','EventScript_TransferredBillsPC','EventScript_TransferredBillsPCBoxFull','EventScript_ChangePokemonNickname','EventScript_NoMoreRoomForPokemon','EventScript_BagIsFull'],commonTexts=['Text_GiveNicknameToThisMon','Text_NoMoreRoomForPokemon','Text_TooBadBagFull','Text_MonSentToBoxInSomeonesPC','Text_MonSentToBoxInBillsPC','Text_MonSentToBoxSomeonesBoxFull','Text_MonSentToBoxBillsBoxFull'],limitations=['Source evidence does not enable gameplay, admit new species, or unlock Saffron maps.','Original Tea flag-before-capacity and flag-based gate semantics are preserved.','Eevee generation and PC/naming use the same bounded ScriptGiveMon pipeline as Magikarp.','Evolution metadata and later moves are catalog-owned; this contract never silently enables them.'])
    encoded=canonical(contract);outputs={'content/generated/regional-tea-eevee/contract.json':encoded}
    for path,b in raw.items():outputs['content/raw/pokefirered/037335f4/'+path]=b
    for path,b in outputs.items():
        dst=safe(ROOT,path)
        if a.check:
            if not dst.exists() or dst.read_bytes()!=b:raise ValueError('output differs: '+path)
        else:dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(b)
    print(json.dumps(dict(check=a.check,sourceFiles=len(files),sourceBytes=sum(len(b) for b in raw.values())),sort_keys=True))
if __name__=='__main__':main()
