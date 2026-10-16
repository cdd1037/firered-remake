#!/usr/bin/env python3
"""Independent Quest Log importer vectors and missing-input rejection."""
import importlib.util,json,pathlib,shutil,tempfile,unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('quest_log_import',ROOT/'scripts/imports/presentation/import-quest-log.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

class QuestLogImporterTests(unittest.TestCase):
    def test_independent_source_vectors(self):
        data=module.generate()
        self.assertEqual(data['texts']['ArrivedInLocation'],'Arrived in {STR_VAR_1}.')
        self.assertEqual(data['texts']['MonsWereFullyRestoredAtCenter'],'POKéMON were fully restored\nat a POKéMON CENTER.')
        self.assertEqual(data['eventSizes'][2],8)
        self.assertEqual(data['eventSizes'][11],4)
        self.assertEqual(data['eventSizes'][37],14)
        self.assertEqual(data['eventSizes'][39],2)
        self.assertEqual(data['mapNames']['map.pallet-town'],'PALLET TOWN')
        self.assertEqual(data['windowPalette'][15],[66,90,107])
        self.assertEqual(data['windowPixels'][:8],[11]*8)
        self.assertEqual(len(data['windowPixels']),192)
        self.assertIn(143,data['excludedWildSpecies'])
        self.assertNotIn(25,data['excludedWildSpecies'])
        self.assertIn('FLAG_WORLD_MAP_SIX_ISLAND',data['arrivalFlags'])
        self.assertIn('map.trainer-tower-lobby',data['disabledMaps'])
        self.assertTrue(any(d['textKey']=='LeftOaksLab' for d in data['departures']))
        self.assertEqual([d['locationId'] for d in data['departures']],list(range(51)))
        by_symbol={d['locationSymbol']:d for d in data['departures']}
        self.assertEqual(by_symbol['QL_LOCATION_VIRIDIAN_GYM']['badgeFlag'],'FLAG_BADGE08_GET')
        self.assertEqual(by_symbol['QL_LOCATION_PEWTER_GYM']['badgeFlag'],'FLAG_BADGE01_GET')
        self.assertEqual(by_symbol['QL_LOCATION_VIRIDIAN_FOREST_2']['regionName'],'ROUTE 2')

    def test_raw_override_and_missing_caption_are_not_silently_accepted(self):
        with tempfile.TemporaryDirectory() as directory:
            raw=pathlib.Path(directory)/'raw'
            files=['src/quest_log_events.c','include/constants/quest_log.h','src/data/text/quest_log.h',
                   'src/data/region_map/region_map_sections.json','include/constants/event_objects.h',
                   'src/field_specials.c','src/item.c','include/constants/species.h','data/maps/PalletTown/map.json',
                   'graphics/text_window/stdpal_4.pal','graphics/quest_log/description_window.png']
            for name in files:
                target=raw/name;target.parent.mkdir(parents=True,exist_ok=True)
                shutil.copyfile(module.RAW/name,target)
            world=pathlib.Path(directory)/'world.json'
            world.write_text(json.dumps({'mapSymbols':{'MAP_PALLET_TOWN':'map.pallet-town'},'maps':[{'sourceMapSymbol':'MAP_PALLET_TOWN','stableId':'map.pallet-town'}]}))
            self.assertEqual(module.generate(raw,world)['mapNames'],{'map.pallet-town':'PALLET TOWN'})
            text=raw/'src/data/text/quest_log.h'
            text.write_text('\n'.join(line for line in text.read_text().splitlines() if 'gText_QuestLog_ArrivedInLocation[' not in line))
            with self.assertRaisesRegex(ValueError,'incomplete source tables'):module.generate(raw,world)

if __name__=='__main__':unittest.main()
