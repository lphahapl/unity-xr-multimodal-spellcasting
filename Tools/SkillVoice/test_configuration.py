"""Configuration contract tests: no microphone, Unity packets, or model downloads."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from configuration import load_configuration, materialize_keywords, normalize_phrase, read_keyword_file


BASE = {
    "schema": 1, "cancelPhrase": "取消施法", "unityPort": 47831, "servicePort": 47832,
    "keywords": [
        {"phrase": "护体咒", "tokens": "h ù t ǐ zh òu"},
        {"phrase": "火球术", "tokens": "h uǒ q iú sh ù"},
        {"phrase": "取消施法", "tokens": "q ǔ x iāo sh ī f ǎ"},
    ],
}


class ConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="skill-config-test-")
        self.root = Path(self.directory.name)
        self.legacy = self.root / "keywords.txt"
        self.legacy.write_text("h uǒ q iú sh ù @火球术\n", encoding="utf-8")

    def tearDown(self):
        self.directory.cleanup()

    def export(self, data=None, name="skill-voice-config.json"):
        path = self.root / name
        path.write_text(json.dumps(BASE if data is None else data, ensure_ascii=False), encoding="utf-8-sig")
        return path

    def test_default_json_wins_over_legacy_file(self):
        path = self.export()
        config = load_configuration(self.root)
        self.assertEqual(config.phrases, ("护体咒", "火球术", "取消施法"))
        self.assertEqual(config.source, str(path.resolve()))

    def test_fourth_phrase_is_accepted_by_cli_without_code_changes(self):
        data = copy.deepcopy(BASE)
        data["keywords"].append({"phrase": "雷电", "tokens": "l éi d iàn"})
        path = self.export(data)
        result = subprocess.run([sys.executable, str(Path(__file__).with_name("skill_voice.py")),
                                 "--config", str(path), "--test-event", "雷电", "--check-config"],
                                capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        event = json.loads(result.stdout.strip())
        self.assertEqual(event["phrases"][-1], "雷电")
        self.assertEqual(event["kind"], "configuration")

    def test_explicit_keywords_override_json_vocabulary_and_keep_its_ports(self):
        data = copy.deepcopy(BASE)
        data["unityPort"], data["servicePort"] = 49201, 49202
        self.export(data)
        experiment = self.root / "长句.txt"
        experiment.write_text("l ǐng y ù @领域\n", encoding="utf-8")
        config = load_configuration(self.root, keywords_path=experiment)
        self.assertEqual(config.phrases, ("领域",))
        self.assertEqual((config.unity_port, config.service_port), (49201, 49202))
        self.assertIsNone(config.cancel_phrase)
        self.assertEqual(config.source, str(experiment.resolve()))

    def test_explicit_ports_override_config(self):
        self.export()
        config = load_configuration(self.root, unity_port=49301, service_port=49302)
        self.assertEqual((config.unity_port, config.service_port), (49301, 49302))

    def test_absent_default_falls_back_to_actual_legacy_labels(self):
        config = load_configuration(self.root)
        self.assertEqual(config.phrases, ("火球术",))
        self.assertEqual(config.source, str(self.legacy.resolve()))

    def test_explicit_missing_config_does_not_silently_fall_back(self):
        with self.assertRaises(FileNotFoundError):
            load_configuration(self.root, config_path=self.root / "missing.json")

    def test_malformed_default_does_not_fall_back(self):
        (self.root / "skill-voice-config.json").write_text("{ broken", encoding="utf-8")
        with self.assertRaises(json.JSONDecodeError):
            load_configuration(self.root)

    def test_invalid_schema_is_rejected(self):
        for schema in (0, 2, True, "1"):
            with self.subTest(schema=schema):
                data = copy.deepcopy(BASE); data["schema"] = schema; self.export(data)
                with self.assertRaisesRegex(ValueError, "schema"):
                    load_configuration(self.root)

    def test_blank_duplicate_and_containment_labels_are_rejected(self):
        for phrase in ("  ， ", "火球术", "大火球术", "all"):
            with self.subTest(phrase=phrase):
                data = copy.deepcopy(BASE)
                data["keywords"].append({"phrase": phrase, "tokens": "d à"}); self.export(data)
                with self.assertRaises(ValueError):
                    load_configuration(self.root)

    def test_missing_or_contained_cancel_is_rejected(self):
        for cancel in ("撤回", "取消", ""):
            with self.subTest(cancel=cancel):
                data = copy.deepcopy(BASE); data["cancelPhrase"] = cancel; self.export(data)
                with self.assertRaises(ValueError):
                    load_configuration(self.root)

    def test_missing_or_control_tokens_are_rejected(self):
        for tokens in ("", "  ", "@火球术", "#0.5", None):
            with self.subTest(tokens=tokens):
                data = copy.deepcopy(BASE); data["keywords"][0]["tokens"] = tokens; self.export(data)
                with self.assertRaises(ValueError):
                    load_configuration(self.root)

    def test_ports_are_bounded_and_distinct(self):
        self.export()
        for ports in ({"unity_port": 0}, {"service_port": 65536}, {"unity_port": True},
                      {"unity_port": 47832}, {"service_port": "47832"}):
            with self.subTest(ports=ports):
                with self.assertRaises(ValueError):
                    load_configuration(self.root, **ports)

    def test_normalization_conflicts_use_unity_rules(self):
        self.assertEqual(normalize_phrase("领 域展开，诛伏赐死！"), "领域展开诛伏赐死")
        data = copy.deepcopy(BASE)
        data["keywords"].append({"phrase": "火 球术！", "tokens": "h uǒ q iú sh ù"}); self.export(data)
        with self.assertRaisesRegex(ValueError, "冲突"):
            load_configuration(self.root)

    def test_temp_wordlist_does_not_change_sources_and_is_cleaned(self):
        path = self.export()
        originals = {source: source.read_bytes() for source in (path, self.legacy)}
        config = load_configuration(self.root)
        with materialize_keywords(config) as generated:
            self.assertEqual(tuple(item.phrase for item in read_keyword_file(generated)), config.phrases)
            self.assertNotEqual(generated, self.legacy)
        self.assertFalse(generated.exists())
        for source, original in originals.items():
            self.assertEqual(source.read_bytes(), original)

    def test_keyword_comments_and_modifiers_preserved(self):
        self.legacy.write_text("# comment\nh uǒ q iú sh ù :1.5 #0.35 @火球术\n", encoding="utf-8")
        config = load_configuration(self.root)
        with materialize_keywords(config) as generated:
            self.assertIn(":1.5 #0.35 @火球术", generated.read_text(encoding="utf-8"))

    def test_cli_rejects_unknown_simulation_before_network(self):
        path = self.export()
        result = subprocess.run([sys.executable, str(Path(__file__).with_name("skill_voice.py")),
                                 "--config", str(path), "--test-event", "未知咒语", "--check-config"],
                                capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(result.returncode, 1)
        self.assertIn("不在本次有效词表", result.stdout)


if __name__ == "__main__":
    unittest.main(verbosity=2)
