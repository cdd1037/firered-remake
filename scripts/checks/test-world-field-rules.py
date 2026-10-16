#!/usr/bin/env python3
"""Live field contracts regenerate from raw and retain their actual business data."""
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/lib'))
from world_source import WorldSource
from world_selection import load_selection
from world_field_rules import compile_rules, compile_region
from icefall_features import doll_collection_contract, sticker_milestones, ice_fall_pairs
from source_features import feature_worlds, script_blocks
RAW=ROOT/'content/raw/pokefirered/037335f4'
SELECTION=ROOT/'data/world-maps.json'


def compare_projection(test, original, current):
    if isinstance(current,dict):
        for key,value in current.items():
            test.assertIn(key,original)
            compare_projection(test,original[key],value)
    elif isinstance(current,list):
        if current and isinstance(current[0],dict) and 'itemId' in current[0]:
            by_id={r['itemId']:r for r in original}
            original=[by_id[r['itemId']] for r in current]
        test.assertEqual(len(original),len(current))
        if current and isinstance(current[0],dict) and ('map' in current[0] or 'sourceMap' in current[0]):
            key=lambda r:json.dumps(r.get('map',r.get('sourceMap')),sort_keys=True)+':'+json.dumps(r.get('localId',r.get('event',r.get('declaration',{}))),sort_keys=True)
            original=sorted(original,key=key);current=sorted(current,key=key)
        for a,b in zip(original,current):compare_projection(test,a,b)
    else:test.assertEqual(original,current)


class WorldFieldRuleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.outputs=compile_rules(WorldSource(RAW),SELECTION)

    def test_retained_business_values(self):
        self.assertEqual(len(self.outputs),24)
        for path,value in self.outputs.items():
            with self.subTest(path=path):
                compare_projection(self,json.loads((ROOT/'content/generated'/path).read_bytes()),value)

    def test_no_generated_input_or_region_preparation(self):
        read=Path.read_bytes
        def only_source(path):
            self.assertNotIn('generated',path.parts,'Compiler read generated data')
            return read(path)
        with patch.object(Path,'read_bytes',only_source):
            actual=compile_rules(WorldSource(RAW),SELECTION)
        self.assertEqual(actual,self.outputs)
        self.assertFalse(any(name.endswith(('catalog.json','manifest.json','npc-source.json','scripts.json','geometry.json')) for name in actual))

    def test_runtime_outputs_have_no_source_inventories(self):
        forbidden={'sourceText','lines','commit','repository','mapTopology','geometry','nativeMechanisms'}
        def visit(value):
            if isinstance(value,dict):
                self.assertFalse(forbidden & value.keys())
                for item in value.values():visit(item)
            elif isinstance(value,list):
                for item in value:visit(item)
        for value in self.outputs.values():visit(value)

    def test_saffron_stock_follows_source(self):
        from saffron_mart_features import compile_saffron
        src=WorldSource(RAW);path='data/maps/SaffronCity_Mart/scripts.inc'
        src.data[path]=src.text(path).replace('.2byte ITEM_GREAT_BALL','.2byte ITEM_ULTRA_BALL').encode()
        self.assertEqual(compile_saffron(src)['martStock'][0],'ITEM_ULTRA_BALL')

    def test_silph_door_patch_follows_source(self):
        src=WorldSource(RAW);path='data/scripts/silphco_doors.inc'
        import re
        original=src.text(path)
        match=re.search(r'(EventScript_Close2FDoor.*?setmetatile )(\d+)',original,re.S)
        self.assertIsNotNone(match)
        offset=match.start(2)
        src.data[path]=(original[:offset]+str(int(match[2])+1)+original[match.end(2):]).encode()
        names=[r['sourceName'] for r in load_selection(SELECTION) if r['path'].startswith('maps/regions/silph/') and 'variant' not in r]
        actual=compile_region(src,'silph',names)['contracts.json']
        door=next(row for row in actual['doors'] if row['closedSource']['label']==original[match.start():].split('::')[0])
        self.assertEqual(door['closedPatches'][0]['x'],int(match[2])+1)

    def test_doll_threshold_follows_native_source(self):
        src=WorldSource(RAW)
        path='src/field_specials.c';body=src.text(path)
        start=body.index('void UpdateLoreleiDollCollection(void)')
        import re
        match=re.search(r'>= (\d+)',body[start:])
        offset=start+match.start(1);value=int(match[1])
        src.data[path]=(body[:offset]+str(value+1)+body[offset+len(match[1]):]).encode()
        names=['FourIsland_LoreleisHouse']
        worlds=feature_worlds(src,names)
        scripts=script_blocks(src,names,['EventScript_ResetAllMapFlags'])
        actual=doll_collection_contract(src,worlds,{b['label']:b for b in scripts['blocks']})
        baseline=self.outputs['maps/regions/four-island/contracts.json']['statFlagUnlocks']
        self.assertEqual(actual['statFlagUnlocks'][0]['minimum'],baseline[0]['minimum']+1)

    def test_ice_coordinates_validate_actual_source_geometry(self):
        src=WorldSource(RAW);worlds=feature_worlds(src,['FourIsland_IcefallCave_1F'])
        actual=ice_fall_pairs(src,worlds)
        first=actual[0]['source'];world=worlds[0]
        world['cells'][first['y']*world['width']+first['x']]['behaviorId']=0
        with self.assertRaisesRegex(ValueError,'geometry'):ice_fall_pairs(src,worlds)

    def test_tanoby_solved_position_follows_source_script(self):
        src=WorldSource(RAW);path='data/maps/SevenIsland_SevaultCanyon_TanobyKey/scripts.inc'
        src.data[path]=src.text(path).replace('setobjectxyperm LOCALID_TANOBY_KEY_BOULDER1, 7, 2','setobjectxyperm LOCALID_TANOBY_KEY_BOULDER1, 8, 2').encode()
        selected=load_selection(SELECTION)
        names=[r['sourceName'] for r in selected if r['path'].startswith('maps/regions/seven-island/') and 'variant' not in r]
        result=compile_region(src,'seven-island',names)['contracts.json']
        self.assertEqual(result['tanobyKey']['boulders'][0]['solvedX'],8)


if __name__=='__main__':unittest.main()
