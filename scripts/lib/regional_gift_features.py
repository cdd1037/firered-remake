"""Tea item presentation and the shared gift-script entry bindings."""
from source_features import script_blocks, feature_constants
from map_runtime import require


def compile_gifts(src):
    names=['CeladonCity_Condominiums_1F','CeladonCity_Condominiums_RoofRoom']
    scripts=script_blocks(src,names)
    common=['EventScript_GetGiftMonPartySlot','EventScript_NameReceivedBoxMon','EventScript_TransferredToPC','EventScript_TransferredSomeonesPC','EventScript_TransferredSomeonesPCBoxFull','EventScript_TransferredBillsPC','EventScript_TransferredBillsPCBoxFull','EventScript_ChangePokemonNickname','EventScript_NoMoreRoomForPokemon','EventScript_BagIsFull']
    texts=['Text_GiveNicknameToThisMon','Text_NoMoreRoomForPokemon','Text_TooBadBagFull','Text_MonSentToBoxInSomeonesPC','Text_MonSentToBoxInBillsPC','Text_MonSentToBoxSomeonesBoxFull','Text_MonSentToBoxBillsBoxFull']
    blocks={b['label'] for b in scripts['blocks']}
    require(set(common+texts)<=blocks,'Missing shared gift labels')
    values=feature_constants(src,[],scripts)['values']
    symbols=['FLAG_GOT_TEA','FLAG_GOT_EEVEE','FLAG_HIDE_EEVEE_BALL','FLAG_TALKED_TO_TEA_LADY_AFTER_HOF','VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES','ITEM_TEA','SPECIES_EEVEE']
    item=next(i for i in src.json('src/data/items.json')['items'] if i['itemId']=='ITEM_TEA')
    return dict(schemaVersion=1,gameplayEnabled=False,constants={k:values[k]['value'] for k in symbols},
        tea=dict(item='pret:ITEM_TEA',description=item['description_english'].replace('\\n','\n'),entryScript=names[0]+'_EventScript_TeaWoman'),
        eevee=dict(entryScript=names[1]+'_EventScript_EeveeBall'),commonScripts=common,commonTexts=texts)
