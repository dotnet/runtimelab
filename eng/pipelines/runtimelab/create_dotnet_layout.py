#!/usr/bin/env python3

import argparse
import hashlib
import json
from pathlib import Path
import shutil
from typing import Dict, List
import xml.etree.ElementTree as ET
import zipfile


RUNTIME_FILES = {
    "win-x64": (
        "runtimes/win-x64/lib/net11.0/System.Private.CoreLib.dll",
        "runtimes/win-x64/native/coreclr.dll",
        "runtimes/win-x64/native/clrgc.dll",
    ),
    "win-arm64": (
        "runtimes/win-arm64/lib/net11.0/System.Private.CoreLib.dll",
        "runtimes/win-arm64/native/coreclr.dll",
        "runtimes/win-arm64/native/clrgc.dll",
    ),
}


def _hash_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _package_identity(path: Path) -> str:
    with zipfile.ZipFile(path) as archive:
        nuspecs = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise ValueError(f"expected one nuspec in {path}, found {len(nuspecs)}")
        root = ET.fromstring(archive.read(nuspecs[0]))
    for element in root.iter():
        if element.tag.rsplit("}", 1)[-1] == "id" and element.text:
            return element.text.strip()
    raise ValueError(f"package id is missing from {path}")


def _find_runtime_package(package_root: Path, rid: str) -> Path:
    expected_id = f"Microsoft.NETCore.App.Runtime.{rid}"
    matches = []
    for path in package_root.rglob("*.nupkg"):
        if ".symbols." not in path.name and _package_identity(path) == expected_id:
            matches.append(path)
    if len(matches) != 1:
        raise ValueError(
            f"expected one {expected_id} package under {package_root}, found {len(matches)}"
        )
    return matches[0]


def _find_layout(source_root: Path, rid: str) -> Path:
    architecture = rid.rsplit("-", 1)[-1]
    candidates = []
    for dotnet in source_root.rglob("dotnet.exe"):
        root = dotnet.parent
        if not (root / "host" / "fxr").is_dir():
            continue
        if not (root / "shared" / "Microsoft.NETCore.App").is_dir():
            continue
        normalized = root.as_posix().lower()
        if architecture not in normalized or "release" not in normalized:
            continue
        candidates.append(root)
    if len(candidates) != 1:
        rendered = ", ".join(str(path) for path in candidates)
        raise ValueError(
            f"expected one complete Release {rid} dotnet layout under {source_root}, "
            f"found {len(candidates)}: {rendered}"
        )
    return candidates[0]


def create_layout(source_root: Path, package_root: Path, output: Path, rid: str) -> Dict:
    if rid not in RUNTIME_FILES:
        raise ValueError(f"unsupported dotnet layout RID: {rid}")
    source = _find_layout(source_root, rid)
    package = _find_runtime_package(package_root, rid)

    if output.exists():
        shutil.rmtree(output)
    shutil.copytree(source, output, symlinks=False)

    framework_roots = sorted(
        path
        for path in (output / "shared" / "Microsoft.NETCore.App").iterdir()
        if path.is_dir()
    )
    fxr_roots = sorted(path for path in (output / "host" / "fxr").iterdir() if path.is_dir())
    if len(framework_roots) != 1 or len(fxr_roots) != 1:
        raise ValueError(
            "dotnet layout must contain exactly one Microsoft.NETCore.App and hostfxr version"
        )

    with zipfile.ZipFile(package) as archive:
        package_hashes = {
            name: _hash_bytes(archive.read(name)) for name in RUNTIME_FILES[rid]
        }

    layout_runtime_files = {
        RUNTIME_FILES[rid][0]: framework_roots[0] / "System.Private.CoreLib.dll",
        RUNTIME_FILES[rid][1]: framework_roots[0] / "coreclr.dll",
        RUNTIME_FILES[rid][2]: framework_roots[0] / "clrgc.dll",
    }
    for package_path, layout_path in layout_runtime_files.items():
        if not layout_path.is_file():
            raise ValueError(f"dotnet layout is missing {layout_path}")
        if _hash_file(layout_path) != package_hashes[package_path]:
            raise ValueError(
                f"dotnet layout runtime file does not match {package.name}: {layout_path.name}"
            )

    files: List[Dict] = []
    for path in sorted(output.rglob("*")):
        if path.is_symlink():
            raise ValueError(f"dotnet layout cannot contain symlinks: {path}")
        if path.is_file():
            files.append(
                {
                    "path": path.relative_to(output).as_posix(),
                    "sha256": _hash_file(path),
                    "size": path.stat().st_size,
                }
            )

    manifest = {
        "schemaVersion": 1,
        "rid": rid,
        "sourceLayout": source.as_posix(),
        "runtimePackage": {
            "fileName": package.name,
            "sha256": _hash_file(package),
            "runtimeFileSha256": package_hashes,
        },
        "layout": {
            "host": "dotnet.exe",
            "hostFxrVersion": fxr_roots[0].name,
            "sharedFrameworkVersion": framework_roots[0].name,
            "files": files,
        },
    }
    (output / "external-runtime-layout.json").write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Create a complete immutable dotnet layout from native build outputs."
    )
    parser.add_argument("--source-root", required=True, type=Path)
    parser.add_argument("--package-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--rid", required=True, choices=sorted(RUNTIME_FILES))
    args = parser.parse_args()
    create_layout(
        args.source_root.resolve(),
        args.package_root.resolve(),
        args.output.resolve(),
        args.rid,
    )


if __name__ == "__main__":
    main()
