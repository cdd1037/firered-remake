#!/usr/bin/env python3
"""NPC scene importer parity and compact generated-only output checks."""
import importlib.util,json,re,tempfile,unittest
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('trade_import',ROOT/'scripts/imports/presentation/import-npc-trade-scene.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class TradeImportTests(unittest.TestCase):
    def test_regeneration_and_source_callback_tables(self):
        raw=ROOT/'content/raw/pokefirered/037335f4'
        with tempfile.TemporaryDirectory() as tmp:
            out=Path(tmp);d=m.convert(raw,out)
            for name in ['scene.json','atlas.png']:
                self.assertEqual((ROOT/'content/generated/ui/npc-trade-scene'/name).read_bytes(),(out/name).read_bytes())
            self.assertEqual(108,len(d['ballVelocity']))
            self.assertEqual([0,0,1,0],d['ballVelocity'][:4]);self.assertEqual([-4,-4],d['ballVelocity'][22:24])
            self.assertEqual(101,sum(p[1]+1 for p in d['signalParameters'][:-1]))
            self.assertEqual(11,len(d['assets']));self.assertEqual({'scene.json','atlas.png'},{p.name for p in out.iterdir()})
            image=Image.open(out/'atlas.png');self.assertEqual((d['width'],d['height']),image.size);image.close()
            regions={a['id']:a for a in d['assets']}
            self.assertEqual(512,regions['signal_send']['width'])
            self.assertEqual(512,regions['signal_receive']['height'])
            self.assertEqual(192,regions['pokeball']['height'])
        source=(raw/'src/trade_scene.c').read_text()
        body=source.split('static void CB2_InitInGameTrade(void)\n{')[1].split('static ',1)[0]
        self.assertIn('AllocZeroed',body);self.assertNotIn('isCableTrade = TRUE',body)
        self.assertIn('else if (JOY_NEW(A_BUTTON))',source)
    def test_no_raw_runtime_or_transaction_side_effect(self):
        source=(ROOT/'src/Game.Godot/CampaignNpcTradeView.cs').read_text()
        draw=source.split('private bool DrawNpcTrade()')[1]
        self.assertNotIn('Apply(',draw);self.assertNotIn('content/raw',source)
        self.assertIn('NpcTradeSceneTimeline.ReadyFrame',source)
        self.assertNotIn('NpcTradePresentationStage',source)
if __name__=='__main__':unittest.main()
