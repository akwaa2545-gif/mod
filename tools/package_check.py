"""Exact candidate-package allowlist, tied to the caller's trusted plugin build SHA-256."""
import argparse
import hashlib
from pathlib import Path
import re
import sys

ALLOWED = frozenset({
    'Observer.dll',
    'README.md',
    'config/io.github.nuclearoption.observer.cfg.example',
    'docs/testing/phase1-smoke.md',
    'docs/testing/phase1-results.md',
})
DIRECTORIES = frozenset({'config', 'docs', 'docs/testing'})

def check_package(root: Path, plugin_sha256: str) -> list[str]:
    root = Path(root).absolute()
    if not re.fullmatch(r'[0-9a-f]{64}', plugin_sha256):
        raise ValueError('expected plugin SHA-256 must be 64 lowercase hexadecimal characters')
    if any(path.is_symlink() for path in (root, *root.parents)):
        raise ValueError('package root or ancestor is a symlink')
    if not root.is_dir():
        raise ValueError('package root is not a directory')
    found = set()
    for path in sorted(root.rglob('*')):
        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            raise ValueError(f'symlink rejected: {relative}')
        if path.is_dir():
            if relative not in DIRECTORIES:
                raise ValueError(f'directory not allowlisted: {relative}')
        elif path.is_file():
            if relative not in ALLOWED:
                raise ValueError(f'file not allowlisted: {relative}')
            found.add(relative)
        else:
            raise ValueError(f'unsupported filesystem entry: {relative}')
    if found != ALLOWED:
        raise ValueError(f'missing required files: {sorted(ALLOWED - found)}')
    digest = hashlib.sha256((root / 'Observer.dll').read_bytes()).hexdigest()
    if digest != plugin_sha256:
        raise ValueError('Observer.dll is not the supplied trusted build hash')
    return sorted(found)

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    parser.add_argument('--plugin-sha256', required=True, help='SHA-256 of our separately verified local build')
    args = parser.parse_args()
    try:
        files = check_package(args.root, args.plugin_sha256)
    except (ValueError, OSError) as exc:
        print(f'REJECTED: {exc}', file=sys.stderr)
        return 1
    print(f'PASS: exact {len(files)}-file allowlist; plugin SHA-256 {args.plugin_sha256}')
    for name in files:
        print(name)
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
