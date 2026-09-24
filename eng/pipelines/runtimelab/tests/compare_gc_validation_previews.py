import argparse
import json
from pathlib import Path

import yaml


def load_final_yaml(path: Path) -> dict:
    content = path.read_text(encoding="utf-8")
    if path.suffix == ".json":
        content = json.loads(content)["finalYaml"]
    return yaml.safe_load(content)


def parse_named_paths(values: list[str]) -> dict[str, Path]:
    result = {}
    for value in values:
        name, separator, path = value.partition("=")
        if not separator or not name or not path:
            raise ValueError(f"expected NAME=PATH, got {value!r}")
        result[name] = Path(path)
    return result


def compare_previews(
    baseline_before: Path,
    baseline_after: Path,
    root_before: dict[str, Path],
    root_after: dict[str, Path],
    shards: dict[str, Path],
) -> None:
    if load_final_yaml(baseline_before) != load_final_yaml(baseline_after):
        raise AssertionError("definition 163 baseline preview changed")

    expected_roots = set(root_before)
    if set(root_after) != expected_roots or set(shards) != expected_roots:
        raise AssertionError("root preview sets do not match")

    for name in sorted(expected_roots):
        before = load_final_yaml(root_before[name])
        after = load_final_yaml(root_after[name])
        shard = load_final_yaml(shards[name])
        if before != after:
            raise AssertionError(f"{name} standard root preview changed")
        if before["stages"] != shard["stages"]:
            raise AssertionError(f"{name} shard stages differ from the standard root")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Compare real Azure previewRun finalYaml responses for GC validation."
    )
    parser.add_argument("--baseline-before", type=Path, required=True)
    parser.add_argument("--baseline-after", type=Path, required=True)
    parser.add_argument("--root-before", action="append", default=[], metavar="NAME=PATH")
    parser.add_argument("--root-after", action="append", default=[], metavar="NAME=PATH")
    parser.add_argument("--shard", action="append", default=[], metavar="NAME=PATH")
    args = parser.parse_args()

    compare_previews(
        args.baseline_before,
        args.baseline_after,
        parse_named_paths(args.root_before),
        parse_named_paths(args.root_after),
        parse_named_paths(args.shard),
    )


if __name__ == "__main__":
    main()
