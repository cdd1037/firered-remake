#!/usr/bin/env python3
"""Independent raw CUT vectors, literal labels and final art regeneration."""
import importlib.util,json,tempfile,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('cut_import',ROOT/'scripts/imports/maps/import-cut-grass.py');cut=importlib.util.module_from_spec(spec);spec.loader.exec_module(cut)
RAW=ROOT/'content/raw/pokefirered/037335f4'
class CutGrassImportTests(unittest.TestCase):
    def test_source_mapping_values_and_no_invented_flowers(self):
        self.assertEqual(cut.mapping(RAW),[[13,1],[10,19],[11,14],[12,15],[850,830],[768,784],[769,785],[770,786],[644,641]])
    def test_converter_reads_alternate_independent_labels(self):
        with tempfile.TemporaryDirectory() as name:
            raw=Path(name);(raw/'src').mkdir();(raw/'include/constants').mkdir(parents=True)
            (raw/'src/fldeff_cut.c').write_text('static const u16 sCutGrassMetatileMapping[][2] = {\n {METATILE_ID(Test, Grass),METATILE_ID(Test, Mowed)}, {0xffff,0xffff}\n};')
            (raw/'include/constants/metatile_labels.h').write_text('#define METATILE_Test_Grass 0x010\n#define METATILE_Test_Mowed 0x011\n')
            self.assertEqual(cut.mapping(raw),[[16,17]])
    def test_generated_runtime_outputs_match_raw(self):
        for path,data in cut.compile_outputs(ROOT,RAW).items():self.assertEqual((ROOT/'content/generated'/path).read_bytes(),data,path)
if __name__=='__main__':unittest.main()
