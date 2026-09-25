import argparse
import json
from pathlib import Path

import yaml


ADMISSION_STAGE_NAME = "GCValidationAdmission"
HELIX_SUBMITTER_JOBS = {
    "gcstress0x3-gcstress0xc": [
        "run_test_p1__linux_arm_checked",
        "run_test_p1__linux_arm64_checked",
        "run_test_p1__linux_x64_checked",
        "run_test_p1__osx_arm64_checked",
        "run_test_p1__windows_x64_checked",
        "run_test_p1__windows_x86_checked",
        "run_test_p1__windows_arm64_checked",
    ],
    "gcstress-extra": [
        "run_test_p1__linux_arm_checked",
        "run_test_p1__linux_arm64_checked",
        "run_test_p1__linux_x64_checked",
        "run_test_p1__osx_arm64_checked",
        "run_test_p1__windows_x64_checked",
        "run_test_p1__windows_x86_checked",
        "run_test_p1__windows_arm64_checked",
    ],
    "gc-longrunning": [
        "run_test_p1__linux_arm64_release",
        "run_test_p1__linux_x64_release",
        "run_test_p1__windows_x64_release",
        "run_test_p1__windows_arm64_release",
    ],
    "gc-simulator": [
        "run_test_p1__linux_arm64_release",
        "run_test_p1__windows_x64_release",
        "run_test_p1__windows_arm64_release",
    ],
    "gc-standalone": [
        "run_test_p1_GCStandAlone_linux_arm64_checked",
        "run_test_p1_GCStandAlone_linux_x64_checked",
        "run_test_p1_GCStandAlone_windows_x64_checked",
        "run_test_p1_GCStandAlone_windows_arm64_checked",
        "run_test_p1_GCStandAloneServer_linux_arm64_checked",
        "run_test_p1_GCStandAloneServer_linux_x64_checked",
        "run_test_p1_GCStandAloneServer_windows_x64_checked",
        "run_test_p1_GCStandAloneServer_windows_arm64_checked",
    ],
}


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


def baseline_execution_contract(preview: dict) -> dict:
    preview = dict(preview)
    preview.pop("parameters", None)
    return normalize_inert_metadata(preview)


def normalize_inert_metadata(value: object) -> object:
    if isinstance(value, dict):
        return {
            key: normalize_inert_metadata(item)
            for key, item in value.items()
            if not (key == "templateContext" and item == {})
        }
    if isinstance(value, list):
        return [normalize_inert_metadata(item) for item in value]
    return value


def normalize_authority_preview(preview: dict) -> dict:
    preview = normalize_inert_metadata(preview)
    normalized = dict(preview)
    normalized["stages"] = [
        normalize_monitor_dependency(
            normalize_execution_stage(stage, expected_dependency=[]),
            expected_dependency=[],
            expected_condition="",
        )
        for stage in preview["stages"]
    ]
    return normalized


def normalize_execution_stage(
    stage: dict, *, expected_dependency: list[str]
) -> dict:
    normalized = dict(stage)
    actual_dependency = normalized.pop("dependsOn", [])
    if actual_dependency != expected_dependency:
        raise AssertionError(
            f"{stage.get('stage')} has unexpected dependsOn {actual_dependency!r}"
        )
    return normalized


def normalize_monitor_dependency(
    stage: dict, *, expected_dependency: list[str], expected_condition: str
) -> dict:
    normalized = dict(stage)
    jobs = []
    monitor_count = 0
    for job in stage.get("jobs", []):
        normalized_job = dict(job)
        if normalized_job.get("job") == "HelixJobMonitor":
            monitor_count += 1
            actual_dependency = normalized_job.pop("dependsOn", [])
            if actual_dependency != expected_dependency:
                raise AssertionError(
                    "HelixJobMonitor has unexpected dependsOn "
                    f"{actual_dependency!r}"
                )
            actual_condition = normalized_job.pop("condition", "")
            if actual_condition != expected_condition:
                raise AssertionError(
                    "HelixJobMonitor has unexpected condition "
                    f"{actual_condition!r}"
                )
        jobs.append(normalized_job)
    if monitor_count != 1:
        raise AssertionError(
            f"{stage.get('stage')} must contain one HelixJobMonitor job"
        )
    normalized["jobs"] = jobs
    return normalized


def shard_execution_stages(preview: dict, root: str) -> list[dict]:
    preview = normalize_inert_metadata(preview)
    stages = preview["stages"]
    if not stages or stages[0].get("stage") != ADMISSION_STAGE_NAME:
        raise AssertionError("shard preview does not start with GCValidationAdmission")
    if sum(
        stage.get("stage") == ADMISSION_STAGE_NAME for stage in stages
    ) != 1:
        raise AssertionError("shard preview must contain one admission stage")
    return [
        normalize_monitor_dependency(
            normalize_execution_stage(
                stage, expected_dependency=[ADMISSION_STAGE_NAME]
            ),
            expected_dependency=HELIX_SUBMITTER_JOBS[root],
            expected_condition="succeededOrFailed()",
        )
        for stage in stages[1:]
    ]


def compare_previews(
    baseline_before: Path,
    baseline_after: Path,
    root_before: dict[str, Path],
    root_after: dict[str, Path],
    shards: dict[str, Path],
) -> None:
    before = baseline_execution_contract(load_final_yaml(baseline_before))
    after = baseline_execution_contract(load_final_yaml(baseline_after))
    if before != after:
        raise AssertionError("definition 163 baseline preview changed")

    expected_roots = set(root_before)
    if set(root_after) != expected_roots or set(shards) != expected_roots:
        raise AssertionError("root preview sets do not match")

    for name in sorted(expected_roots):
        before = normalize_authority_preview(load_final_yaml(root_before[name]))
        after = normalize_authority_preview(load_final_yaml(root_after[name]))
        shard_stages = shard_execution_stages(
            load_final_yaml(shards[name]), name
        )
        if before != after:
            raise AssertionError(f"{name} standard root preview changed")
        if before["stages"] != shard_stages:
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
