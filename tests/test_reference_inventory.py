"""Inventory tests use arbitrary bytes, not valid or executable game assemblies."""

import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "tools" / "reference_inventory.py"


class ReferenceInventoryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / "refs"
        self.root.mkdir()

    def build_manifest(self, root=None):
        # Turn the initially absent implementation into an explicit RED assertion,
        # rather than preventing unittest from discovering any behavioral tests.
        spec = importlib.util.spec_from_file_location("reference_inventory", SCRIPT)
        module = importlib.util.module_from_spec(spec)
        try:
            spec.loader.exec_module(module)
        except FileNotFoundError:
            self.fail("The reference inventory implementation does not exist yet")
        return module.build_manifest(self.root if root is None else root)

    def run_cli(self, output, root=None):
        return subprocess.run(
            [sys.executable, str(SCRIPT), "--root", str(self.root if root is None else root),
             "--output", str(output)],
            capture_output=True, text=True, check=False,
        )

    def write_fixture(self):
        (self.root / "nested").mkdir()
        (self.root / "nested" / "b.DLL").write_bytes(b"b")
        (self.root / "a.dll").write_bytes(b"a")
        (self.root / "readme.txt").write_text("not an assembly", encoding="utf-8")

    def test_known_hashes_are_sorted_and_non_dll_files_are_omitted(self):
        self.write_fixture()
        manifest = self.build_manifest()
        self.assertEqual(manifest["schema_version"], 1)
        self.assertEqual(manifest["files"], [
            {"path": "a.dll", "size_bytes": 1, "sha256": hashlib.sha256(b"a").hexdigest()},
            {"path": "nested/b.DLL", "size_bytes": 1, "sha256": hashlib.sha256(b"b").hexdigest()},
        ])
        self.assertEqual(set(manifest), {"schema_version", "files"})
        self.assertNotIn(str(self.root), json.dumps(manifest))

    def test_empty_dll_is_an_inventory_file_not_an_assembly_validation(self):
        (self.root / "empty.dll").write_bytes(b"")
        self.assertEqual(self.build_manifest()["files"], [
            {"path": "empty.dll", "size_bytes": 0,
             "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"},
        ])

    def test_repeated_calls_are_deterministic_and_do_not_write_inputs(self):
        self.write_fixture()
        before = {p.relative_to(self.root).as_posix(): (p.read_bytes(), p.stat().st_mtime_ns)
                  for p in self.root.rglob("*") if p.is_file()}
        self.assertEqual(self.build_manifest(), self.build_manifest())
        after = {p.relative_to(self.root).as_posix(): (p.read_bytes(), p.stat().st_mtime_ns)
                 for p in self.root.rglob("*") if p.is_file()}
        self.assertEqual(before, after)

    def test_missing_root_is_rejected(self):
        with self.assertRaises(ValueError):
            self.build_manifest(self.base / "missing")

    def test_root_file_is_rejected(self):
        file_root = self.base / "file.dll"
        file_root.write_bytes(b"a")
        with self.assertRaises(ValueError):
            self.build_manifest(file_root)

    def test_empty_root_is_rejected(self):
        with self.assertRaises(ValueError):
            self.build_manifest()

    def test_root_with_only_non_dll_files_is_rejected(self):
        (self.root / "notes.txt").write_text("no DLL", encoding="utf-8")
        with self.assertRaises(ValueError):
            self.build_manifest()

    def test_file_symlink_is_rejected_even_when_not_a_dll(self):
        (self.root / "a.dll").write_bytes(b"a")
        (self.root / "notes.txt").symlink_to(self.root / "a.dll")
        with self.assertRaises(ValueError):
            self.build_manifest()

    def test_dangling_symlink_is_rejected(self):
        (self.root / "a.dll").write_bytes(b"a")
        (self.root / "dangling.dll").symlink_to(self.base / "missing.dll")
        with self.assertRaises(ValueError):
            self.build_manifest()

    def test_directory_symlink_is_rejected(self):
        (self.root / "a.dll").write_bytes(b"a")
        outside = self.base / "outside"
        outside.mkdir()
        (self.root / "linked").symlink_to(outside, target_is_directory=True)
        with self.assertRaises(ValueError):
            self.build_manifest()

    def test_symlink_root_is_rejected(self):
        (self.root / "a.dll").write_bytes(b"a")
        linked = self.base / "linked"
        linked.symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(ValueError):
            self.build_manifest(linked)

    def test_symlink_ancestor_of_root_is_rejected(self):
        self.write_fixture()
        linked = self.base / "linked"
        linked.symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(ValueError):
            self.build_manifest(linked / "nested")

    def test_case_insensitive_file_collision_is_rejected(self):
        (self.root / "A.dll").write_bytes(b"a")
        (self.root / "a.DLL").write_bytes(b"b")
        with self.assertRaises(ValueError):
            self.build_manifest()

    def test_case_insensitive_directory_collision_is_rejected(self):
        (self.root / "A").mkdir()
        (self.root / "a").mkdir()
        (self.root / "A" / "one.dll").write_bytes(b"a")
        (self.root / "a" / "two.dll").write_bytes(b"b")
        with self.assertRaises(ValueError):
            self.build_manifest()

    def test_case_insensitive_non_dll_collision_is_rejected(self):
        (self.root / "a.dll").write_bytes(b"a")
        (self.root / "Notes.txt").write_bytes(b"a")
        (self.root / "notes.TXT").write_bytes(b"b")
        with self.assertRaises(ValueError):
            self.build_manifest()

    def test_cli_writes_and_replaces_only_the_requested_manifest(self):
        self.write_fixture()
        output = self.base / "discovery" / "manifest.json"
        first = self.run_cli(output)
        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(json.loads(output.read_text(encoding="utf-8")), self.build_manifest())
        first_bytes = output.read_bytes()
        second = self.run_cli(output)
        self.assertEqual(second.returncode, 0, second.stderr)
        self.assertEqual(output.read_bytes(), first_bytes)
        self.assertEqual(list(output.parent.iterdir()), [output])
        self.assertEqual((self.root / "a.dll").read_bytes(), b"a")

    def test_cli_bad_input_preserves_existing_output_and_leaves_no_temp_file(self):
        output = self.base / "manifest.json"
        output.write_bytes(b"existing manifest")
        result = self.run_cli(output)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("reference inventory error:", result.stderr)
        self.assertEqual(output.read_bytes(), b"existing manifest")
        self.assertEqual(sorted(p.name for p in self.base.iterdir()), ["manifest.json", "refs"])

    def test_cli_rejects_output_inside_input_tree(self):
        (self.root / "a.dll").write_bytes(b"a")
        for output in (self.root / "a.dll", self.root / "new" / "manifest.json"):
            with self.subTest(output=output):
                result = self.run_cli(output)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("reference inventory error:", result.stderr)
        self.assertEqual((self.root / "a.dll").read_bytes(), b"a")
        self.assertFalse((self.root / "new").exists())

    def test_cli_rejects_symlink_output_without_touching_target(self):
        (self.root / "a.dll").write_bytes(b"a")
        target = self.base / "target.json"
        target.write_bytes(b"untouched")
        output = self.base / "manifest.json"
        output.symlink_to(target)
        result = self.run_cli(output)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("reference inventory error:", result.stderr)
        self.assertTrue(output.is_symlink())
        self.assertEqual(target.read_bytes(), b"untouched")

    def test_cli_rejects_symlink_output_parent(self):
        (self.root / "a.dll").write_bytes(b"a")
        outside = self.base / "outside"
        outside.mkdir()
        linked = self.base / "linked"
        linked.symlink_to(outside, target_is_directory=True)
        result = self.run_cli(linked / "manifest.json")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("reference inventory error:", result.stderr)
        self.assertEqual(list(outside.iterdir()), [])

    def test_cli_errors_on_missing_input_without_creating_output(self):
        output = self.base / "new" / "manifest.json"
        result = self.run_cli(output, self.base / "missing")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("reference inventory error:", result.stderr)
        self.assertFalse(output.parent.exists())


if __name__ == "__main__":
    unittest.main()
