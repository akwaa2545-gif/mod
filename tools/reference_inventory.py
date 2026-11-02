"""Hash private DLL files without loading, executing, or inspecting assemblies."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile


def _reject_symlink_components(path: Path) -> None:
    for component in reversed((path, *path.parents)):
        if component.is_symlink():
            raise ValueError(f"symlinks are not allowed: {component}")


def build_manifest(root: Path) -> dict:
    """Return a deterministic inventory; raise ValueError for unsafe/empty input.

    Only ordinary files and directories are accepted. Symlinks and casefolded
    relative-path collisions anywhere in the tree fail closed, even for non-DLLs.
    Inputs must remain unchanged while this read-only operation runs.
    """
    root = root.absolute()
    _reject_symlink_components(root)
    if not root.is_dir():
        raise ValueError(f"input root must be an existing directory: {root}")
    root = root.resolve()
    seen = set()
    dlls = []

    def visit(directory: Path) -> None:
        for path in sorted(directory.iterdir()):
            relative = path.relative_to(root).as_posix()
            if path.is_symlink():
                raise ValueError(f"symlinks are not allowed: {relative}")
            folded = relative.casefold()
            if folded in seen:
                raise ValueError(f"case-insensitive path collision: {relative}")
            seen.add(folded)
            if path.is_dir():
                visit(path)
            elif not path.is_file():
                raise ValueError(f"only ordinary files/directories are allowed: {relative}")
            elif path.suffix.casefold() == ".dll":
                dlls.append((relative, path))

    visit(root)
    if not dlls:
        raise ValueError("input contains no DLL files")
    files = []
    for relative, path in sorted(dlls):
        digest = hashlib.sha256()
        size = 0
        with path.open("rb") as source:
            while chunk := source.read(1024 * 1024):
                digest.update(chunk)
                size += len(chunk)
        files.append({"path": relative, "size_bytes": size, "sha256": digest.hexdigest()})
    return {"schema_version": 1, "files": files}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path, help="private DLL input directory")
    parser.add_argument("--output", required=True, type=Path, help="explicit JSON destination outside input")
    args = parser.parse_args()
    temporary = None
    try:
        manifest = build_manifest(args.root)
        destination = args.output.absolute()
        _reject_symlink_components(destination)
        destination = destination.resolve()
        root = args.root.resolve()
        if destination == root or root in destination.parents:
            raise ValueError("output must be outside the read-only input tree")
        destination.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(
            mode="w", encoding="utf-8", newline="\n", dir=destination.parent,
            prefix=f".{destination.name}.", suffix=".tmp", delete=False,
        ) as output:
            temporary = Path(output.name)
            json.dump(manifest, output, indent=2)
            output.write("\n")
        os.replace(temporary, destination)
        return 0
    except (OSError, ValueError) as error:
        print(f"reference inventory error: {error}", file=sys.stderr)
        return 1
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


if __name__ == "__main__":
    raise SystemExit(main())
