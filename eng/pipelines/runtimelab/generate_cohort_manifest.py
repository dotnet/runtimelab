#!/usr/bin/env python3

import argparse
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile


SOURCE_VERSION_PATTERN = re.compile(r"^[0-9a-fA-F]{40}$")


def _non_empty(value: str) -> str:
    if not value.strip():
        raise argparse.ArgumentTypeError("value must not be empty")
    return value


def _source_version(value: str) -> str:
    if not SOURCE_VERSION_PATTERN.fullmatch(value):
        raise argparse.ArgumentTypeError("source version must be a 40-character Git commit SHA")
    return value.lower()


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _package_identity(path: Path) -> tuple[str, str]:
    with zipfile.ZipFile(path) as archive:
        nuspecs = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise ValueError(f"package must contain exactly one .nuspec: {path}")
        root = ET.fromstring(archive.read(nuspecs[0]))

    metadata = next(
        (element for element in root.iter() if element.tag.rsplit("}", 1)[-1] == "metadata"),
        None,
    )
    if metadata is None:
        raise ValueError(f"package metadata is missing: {path}")

    values = {
        child.tag.rsplit("}", 1)[-1]: child.text
        for child in metadata
        if child.text is not None
    }
    package_id = values.get("id", "").strip()
    package_version = values.get("version", "").strip()
    if not package_id or not package_version:
        raise ValueError(f"package identity is incomplete: {path}")
    return package_id, package_version


def _file_evidence(path: Path, relative_to: Path | None = None) -> dict:
    return {
        "path": (
            path.relative_to(relative_to).as_posix() if relative_to else path.name
        ),
        "sha256": _sha256(path),
        "size": path.stat().st_size,
    }


def create_manifest(args: argparse.Namespace) -> dict:
    package_root = args.package_root.resolve()
    if not package_root.is_dir():
        raise ValueError(f"package root does not exist: {package_root}")

    packages = []
    for path in sorted(package_root.rglob("*")):
        if path.is_file() and path.suffix in (".nupkg", ".snupkg"):
            package_id, package_version = _package_identity(path)
            evidence = _file_evidence(path, package_root)
            evidence.update(
                {
                    "id": package_id,
                    "version": package_version,
                    "kind": "symbols" if path.suffix == ".snupkg" else "package",
                }
            )
            packages.append(evidence)

    if not packages:
        raise ValueError(f"no NuGet packages found under {package_root}")

    artifacts = []
    for artifact in args.artifact:
        artifact = artifact.resolve()
        if not artifact.is_file():
            raise ValueError(f"artifact does not exist: {artifact}")
        artifacts.append(_file_evidence(artifact))

    return {
        "schemaVersion": 1,
        "source": {
            "repository": args.source_repository,
            "branch": args.source_branch,
            "commit": args.source_version,
        },
        "performanceSource": {
            "ref": args.performance_repository_ref,
            "commit": args.performance_source_version,
        },
        "build": {
            "id": args.build_id,
            "number": args.build_number,
            "job": args.job_name,
            "configuration": args.build_configuration,
            "os": args.os_group,
            "architecture": args.architecture,
            "cohortRole": args.cohort_role,
        },
        "packages": packages,
        "artifacts": artifacts,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Create immutable package-cohort evidence for a runtimelab build job."
    )
    parser.add_argument("--package-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--source-repository", required=True, type=_non_empty)
    parser.add_argument("--source-branch", required=True, type=_non_empty)
    parser.add_argument("--source-version", required=True, type=_source_version)
    parser.add_argument("--performance-repository-ref", required=True, type=_non_empty)
    parser.add_argument("--performance-source-version", required=True, type=_source_version)
    parser.add_argument("--build-id", required=True, type=_non_empty)
    parser.add_argument("--build-number", required=True, type=_non_empty)
    parser.add_argument("--job-name", required=True, type=_non_empty)
    parser.add_argument("--build-configuration", required=True, type=_non_empty)
    parser.add_argument("--os-group", required=True, type=_non_empty)
    parser.add_argument("--architecture", required=True, type=_non_empty)
    parser.add_argument("--cohort-role", required=True, type=_non_empty)
    parser.add_argument("--artifact", action="append", default=[], type=Path)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    manifest = create_manifest(args)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
