#!/usr/bin/env python3
"""Party atlas reproducibility and native state checks (no runtime required)."""
import importlib.util,io,tempfile,unittest
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('party_import',ROOT/'scripts/imports/presentation/import-party-screen.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
RAW=ROOT/'content/raw/pokefirered/037335f4'
class PartyScreenTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.data=m.convert(RAW);cls.image=Image.open(io.BytesIO(cls.data)).convert('RGBA')
 def test_regeneration(self):self.assertEqual(self.data,(ROOT/'content/generated/ui/party-screen/atlas.png').read_bytes())
 def test_background_is_opaque(self):self.assertEqual(self.image.crop((0,0,240,160)).getextrema()[3],(255,255))
 def test_all_twelve_panels_are_distinct(self):
  self.assertEqual(len({self.image.crop((0,160+i*56,224,160+(i+1)*56)).tobytes() for i in range(12)}),12)
 def test_empty_slot_and_item_mail(self):
  self.assertNotEqual(self.image.crop((112,864,120,872)).tobytes(),self.image.crop((112,872,120,880)).tobytes())
  self.assertEqual(self.image.crop((80,832,224,856)).size,(144,24))
 def test_open_closed_pokeball(self):self.assertNotEqual(self.image.crop((144,864,176,896)).tobytes(),self.image.crop((144,896,176,928)).tobytes())
 def test_missing_input_fails(self):
  with tempfile.TemporaryDirectory() as d:
   with self.assertRaises(FileNotFoundError):m.convert(Path(d))
if __name__=='__main__':unittest.main()
