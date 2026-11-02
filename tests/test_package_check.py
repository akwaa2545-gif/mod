import hashlib
import importlib.util
import pathlib
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('package_check', ROOT / 'tools/package_check.py')
package_check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package_check)

class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = pathlib.Path(self.temp.name) / 'stage'
        self.root.mkdir()
        self.plugin = b'our compiled fixture'
        self.digest = hashlib.sha256(self.plugin).hexdigest()
        for name in package_check.ALLOWED:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(self.plugin if name == 'Observer.dll' else b'our documentation')
    def test_allow_exact_own_files(self):
        self.assertEqual(package_check.check_package(self.root, self.digest), sorted(package_check.ALLOWED))
    def test_reject_reference_dll(self):
        (self.root / 'Assembly-CSharp.dll').write_bytes(b'private')
        with self.assertRaises(ValueError): package_check.check_package(self.root, self.digest)
    def test_reject_private_empty_directory(self):
        (self.root / 'private').mkdir()
        with self.assertRaises(ValueError): package_check.check_package(self.root, self.digest)
    def test_reject_mislabeled_reference_plugin(self):
        (self.root / 'Observer.dll').write_bytes(b'not our build')
        with self.assertRaises(ValueError): package_check.check_package(self.root, self.digest)
    def test_reject_symlink_file(self):
        path=self.root/'README.md'; path.unlink(); path.symlink_to(self.root/'Observer.dll')
        with self.assertRaises(ValueError): package_check.check_package(self.root, self.digest)
    def test_reject_symlink_root(self):
        link=self.root.parent/'link'; link.symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(ValueError): package_check.check_package(link, self.digest)
    def test_reject_symlink_ancestor(self):
        link=self.root.parent/'link'; link.symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(ValueError): package_check.check_package(link/'nested', self.digest)
    def test_reject_missing_docs(self):
        (self.root/'README.md').unlink()
        with self.assertRaises(ValueError): package_check.check_package(self.root, self.digest)
    def test_reject_log_save_decompilation_or_asset(self):
        for name in ('output.log','save.json','Missile.cs','texture.assets','BepInEx.dll','0Harmony.dll'):
            path=self.root/name; path.write_bytes(b'no')
            with self.assertRaises(ValueError): package_check.check_package(self.root, self.digest)
            path.unlink()
    def test_reject_missing_or_empty_root(self):
        with self.assertRaises(ValueError): package_check.check_package(self.root/'missing', self.digest)
        for file in self.root.rglob('*'):
            if file.is_file(): file.unlink()
        with self.assertRaises(ValueError): package_check.check_package(self.root, self.digest)

if __name__ == '__main__': unittest.main()
