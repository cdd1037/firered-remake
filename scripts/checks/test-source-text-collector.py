import json
from pathlib import Path
import tempfile
import unittest
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "lib"))

from source_text_collector import (
    collect_declarations, parse_charmap, canonicalize_controls,
    control_units, apply_declared_transform, EXT_CONTROL_LENGTHS,
)

ROOT = Path(__file__).resolve().parents[2]
ZH = ROOT / "content/raw/PretFRLG/a04f6317"
EN = ROOT / "content/raw/pokefirered/037335f4"


class CollectorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.zh = parse_charmap(ZH / "charmap.txt")
        cls.en = parse_charmap(EN / "charmap.txt")
        cls.collection = collect_declarations(ZH)

    def result(self, source, baseline):
        return canonicalize_controls(source, baseline, self.zh, self.en)

    def test_inline_comments_and_braille(self):
        self.assertIn("gFameCheckerFlavorText_ProfOak0", self.collection.texts)
        self.assertEqual(self.collection.texts["Braille_Text_ABC"], "ABC$")

    def test_commented_strings_not_ingested(self):
        declaration = self.collection.records["PalletTown_Text_CanStoreItemsAndMonsInPC"][0]
        self.assertNotIn("通过电脑你可以像管理数据一样", declaration.text)
        self.assertNotIn("叶绿", declaration.text)

    def test_translator_commentary_withheld(self):
        self.assertIn("PalletTown_Text_CanStoreItemsAndMonsInPC", self.collection.excluded)
        self.assertNotIn("PalletTown_Text_CanStoreItemsAndMonsInPC", self.collection.texts)

    def test_hack_only_message_not_selected(self):
        self.assertEqual(self.collection.texts["sText_EmptyString4"], "")

    def test_all_story_ids_accounted_for(self):
        bank = json.loads((ROOT / "content/generated/data/story.json").read_text())["TextBank"]
        self.assertFalse(set(bank) - self.collection.texts.keys() - self.collection.excluded.keys())

    def test_all_battle_ids_accounted_for(self):
        bank = json.loads((ROOT / "content/generated/battle/text.json").read_text())
        self.assertFalse(set(bank) - self.collection.texts.keys())

    def test_no_collection_conflicts(self):
        self.assertFalse(self.collection.diagnostics["conflicts"])
        self.assertFalse(self.collection.diagnostics["unknownConditions"])
        self.assertFalse(self.collection.diagnostics["issues"])

    def test_c_concatenation_and_comments(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / "sample.c"
            p.write_text(
                '// const u8 hidden[] = _("hidden");\n'
                'static const u8 multi[] = _("first\\n" /* comment */ "second");\n'
                '#if XZONN_HACK\nconst u8 choice[] = _("hack");\n'
                '#else\nconst u8 choice[] = _("baseline");\n#endif\n')
            c = collect_declarations(directory)
            self.assertEqual(c.texts["multi"], r"first\nsecond")
            self.assertEqual(c.texts["choice"], "baseline")
            self.assertNotIn("hidden", c.texts)

    def test_variable_alias_and_reorder(self):
        r = self.result("{B_COPY_VAR_2}看{B_COPY_VAR_1}", "{STR_VAR_1} sees {STR_VAR_2}")
        self.assertEqual(r["text"], "{STR_VAR_2}看{STR_VAR_1}")
        self.assertFalse(r["unresolved"])

    def test_battle_player_alias_is_contextual(self):
        r = self.result("{PLAYER}学习了招式", "{B_BUFF2} learned a move")
        self.assertEqual(r["text"], "{B_BUFF2}学习了招式")
        self.assertFalse(r["unresolved"])

    def test_music_split_operands(self):
        r = self.result("开始{PLAY_BGM 0x56 0x01}$", "Begin{PLAY_BGM}{MUS_ENCOUNTER_GYM_LEADER}$")
        self.assertEqual(r["text"], "开始{PLAY_BGM}{MUS_ENCOUNTER_GYM_LEADER}$")
        self.assertFalse(r["unresolved"])

    def test_legacy_literal_glyph_operands(self):
        for source, baseline in (("{HIGHLIGHT}À文字", "{HIGHLIGHT WHITE}text"),
                                 ("{HIGHLIGHT} 文字", "{HIGHLIGHT 0}text")):
            r = self.result(source, baseline)
            self.assertFalse(r["unresolved"])
            self.assertNotIn("À", r["text"])

    def test_newline_reflow_allowed(self):
        self.assertFalse(self.result(r"你好\n世界$", "Hello world$")["unresolved"])

    def test_page_sequence_preserved(self):
        r = self.result(r"甲\l乙\p丙$", r"A\pB\lC$")
        self.assertTrue(any(i["kind"] == "page-scroll-sequence-difference" for i in r["unresolved"]))

    def test_pause_removal_not_hidden(self):
        r = self.result("一{PAUSE 0x0F}二", "One{PAUSE 0x0F}two{PAUSE 0x0F}")
        self.assertFalse(r["encodedControlsMatch"])

    def test_semantic_variable_difference_not_guessed(self):
        r = self.result("{B_COPY_VAR_1}", "{B_CURRENT_MOVE}")
        self.assertEqual(r["text"], "{B_COPY_VAR_1}")
        self.assertFalse(r["encodedControlsMatch"])

    def test_terminator_anchor_preserved(self):
        r = self.result("字$后", "word$")
        self.assertTrue(any(i["kind"] == "terminator-anchor-difference" for i in r["unresolved"]))

    def test_unknown_token_withheld(self):
        r = self.result("{UNKNOWN_NEW}你好", "Hello")
        self.assertTrue(any(i["kind"] == "unknown-control-encoding" for i in r["unresolved"]))

    def test_only_declared_transformations_apply(self):
        self.assertEqual(apply_declared_transform("字{PAUSE_UNTIL_PRESS}", {}),
                         "字{PAUSE_UNTIL_PRESS}")
        self.assertEqual(apply_declared_transform("字{PAUSE_UNTIL_PRESS}",
                                                  {"removePause": True, "terminate": True}), "字$")

    def test_control_arity_source_table_matches(self):
        import re
        source = (EN / "src/string_util.c").read_text()
        body = re.search(r"u8 GetExtCtrlCodeLength.*?lengths\[\]\s*=\s*\{(.*?)\}", source, re.S)[1]
        self.assertEqual(tuple(map(int, re.findall(r"\d+", body))), EXT_CONTROL_LENGTHS)


if __name__ == "__main__":
    unittest.main(verbosity=2)
