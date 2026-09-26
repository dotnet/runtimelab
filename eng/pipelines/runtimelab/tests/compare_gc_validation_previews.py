import argparse
import json
from pathlib import Path

import yaml


ADMISSION_STAGE_NAME = "GCValidationAdmission"
DEFINITION129_ROOT = "runtime-coreclr-correctness"
DEFINITION129_AUTHORITY_JOBS = [
    "build_wasi_wasm_linux_Release_CoreCLR_WASI_RuntimeTests",
    "run_test_p0_coreclr__linux_arm_checked",
    "run_test_p0_coreclr__windows_x86_checked",
    "run_test_p0_coreclr__windows_arm64_checked",
    "run_test_p0_coreclr__browser_wasm_checked",
    "run_test_p0_coreclr_R2R_CG2_browser_wasm_checked",
    "run_test_p0_coreclr__linux_arm64_checked",
    "run_test_p0_coreclr__linux_x64_checked",
    "run_test_p0_coreclr__osx_arm64_checked",
    "run_test_p0_coreclr__osx_x64_checked",
    "run_test_p0_coreclr__windows_x64_checked",
]
DEFINITION141_ROOT = "crossgen2-composite-gcstress"
DEFINITION141_AUTHORITY_JOBS = [
    "run_test_p1_Composite_linux_arm64_checked",
    "run_test_p1_Composite_linux_x64_checked",
    "run_test_p1_Composite_osx_arm64_checked",
    "run_test_p1_Composite_windows_x64_checked",
    "run_test_p1_Composite_windows_arm64_checked",
]
DEFINITION141_SCENARIOS = [
    "heapverify1",
    "gcstress0xc_disabler2r",
    "gcstress0xc_disabler2r_jitstress2",
    "gcstress0xc_disabler2r_heapverify1",
    "gcstress0xc_jitstress1",
    "gcstress0xc_jitstress2",
    "gcstress0xc_tailcallstress",
    "gcstress0xc_jitminopts_heapverify1",
]
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
    DEFINITION129_ROOT: DEFINITION129_AUTHORITY_JOBS,
    DEFINITION141_ROOT: DEFINITION141_AUTHORITY_JOBS,
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


def stage_by_name(preview: dict, name: str) -> dict:
    matches = [stage for stage in preview["stages"] if stage.get("stage") == name]
    if len(matches) != 1:
        raise AssertionError(f"preview must contain one {name} stage")
    return matches[0]


def jobs_by_name(stage: dict) -> dict[str, dict]:
    result = {}
    for job in stage.get("jobs", []):
        name = job.get("job")
        if not name:
            continue
        if name in result:
            raise AssertionError(f"stage contains duplicate job {name}")
        result[name] = job
    return result


def contains_helix_submission(value: object) -> bool:
    if isinstance(value, dict):
        if value.get("displayName") == "Send to Helix":
            return True
        return any(contains_helix_submission(item) for item in value.values())
    if isinstance(value, list):
        return any(contains_helix_submission(item) for item in value)
    return isinstance(value, str) and "helixpublishwitharcade.proj" in value


def compare_definition129_previews(authority: Path, shard: Path) -> None:
    authority_preview = normalize_inert_metadata(load_final_yaml(authority))
    authority_stage = stage_by_name(authority_preview, "Build")
    authority_jobs = jobs_by_name(authority_stage)

    shard_preview = load_final_yaml(shard)
    shard_stages = shard_execution_stages(shard_preview, DEFINITION129_ROOT)
    if len(shard_stages) != 1 or shard_stages[0].get("stage") != "Build":
        raise AssertionError("definition 129 shard must contain one Build stage")
    shard_stage = shard_stages[0]
    shard_jobs = jobs_by_name(shard_stage)

    missing_authority = [
        name for name in DEFINITION129_AUTHORITY_JOBS if name not in authority_jobs
    ]
    missing_shard = [
        name for name in DEFINITION129_AUTHORITY_JOBS if name not in shard_jobs
    ]
    if missing_authority or missing_shard:
        raise AssertionError(
            "definition 129 authority jobs are missing: "
            f"authority={missing_authority!r}, shard={missing_shard!r}"
        )

    for name in DEFINITION129_AUTHORITY_JOBS:
        if authority_jobs[name] != shard_jobs[name]:
            raise AssertionError(
                f"definition 129 authoritative job {name} differs"
            )

    submitters = [
        job.get("job")
        for job in shard_stage.get("jobs", [])
        if contains_helix_submission(job)
    ]
    if submitters != DEFINITION129_AUTHORITY_JOBS:
        raise AssertionError(
            "definition 129 shard Helix submitters differ: "
            f"{submitters!r}"
        )


def definition141_rows(jobs: dict[str, dict]) -> set[tuple[str, str]]:
    rows = set()
    for name in DEFINITION141_AUTHORITY_JOBS:
        job = jobs[name]
        submission_steps = [
            step
            for step in job.get("steps", [])
            if contains_helix_submission(step)
        ]
        if len(submission_steps) != 1:
            raise AssertionError(
                f"definition 141 job {name} must contain one Helix submission"
            )
        scenarios = submission_steps[0].get("env", {}).get("_Scenarios")
        if not isinstance(scenarios, str):
            raise AssertionError(
                f"definition 141 job {name} is missing _Scenarios"
            )
        for scenario in scenarios.split(","):
            if not scenario or (name, scenario) in rows:
                raise AssertionError(
                    f"definition 141 job {name} has invalid scenarios"
                )
            rows.add((name, scenario))
    return rows


def compare_definition141_previews(authority: Path, shard: Path) -> None:
    authority_preview = normalize_inert_metadata(load_final_yaml(authority))
    authority_stage = stage_by_name(authority_preview, "Build")
    authority_jobs = jobs_by_name(authority_stage)

    shard_preview = load_final_yaml(shard)
    shard_stages = shard_execution_stages(shard_preview, DEFINITION141_ROOT)
    if len(shard_stages) != 1 or shard_stages[0].get("stage") != "Build":
        raise AssertionError("definition 141 shard must contain one Build stage")
    shard_stage = shard_stages[0]
    shard_jobs = jobs_by_name(shard_stage)

    missing_authority = [
        name for name in DEFINITION141_AUTHORITY_JOBS if name not in authority_jobs
    ]
    missing_shard = [
        name for name in DEFINITION141_AUTHORITY_JOBS if name not in shard_jobs
    ]
    if missing_authority or missing_shard:
        raise AssertionError(
            "definition 141 authority jobs are missing: "
            f"authority={missing_authority!r}, shard={missing_shard!r}"
        )

    for name in DEFINITION141_AUTHORITY_JOBS:
        if authority_jobs[name] != shard_jobs[name]:
            raise AssertionError(
                f"definition 141 authoritative job {name} differs"
            )

    submitters = [
        job.get("job")
        for job in shard_stage.get("jobs", [])
        if contains_helix_submission(job)
    ]
    if submitters != DEFINITION141_AUTHORITY_JOBS:
        raise AssertionError(
            "definition 141 shard Helix submitters differ: "
            f"{submitters!r}"
        )

    expected_rows = {
        (job, scenario)
        for job in DEFINITION141_AUTHORITY_JOBS
        for scenario in DEFINITION141_SCENARIOS
    }
    authority_rows = definition141_rows(authority_jobs)
    shard_rows = definition141_rows(shard_jobs)
    if authority_rows != expected_rows or shard_rows != expected_rows:
        raise AssertionError(
            "definition 141 canonical rows differ: "
            f"authority={len(authority_rows)}, shard={len(shard_rows)}, "
            f"expected={len(expected_rows)}"
        )


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
    parser.add_argument("--definition129-authority", type=Path)
    parser.add_argument("--definition129-shard", type=Path)
    parser.add_argument("--definition141-authority", type=Path)
    parser.add_argument("--definition141-shard", type=Path)
    args = parser.parse_args()

    compare_previews(
        args.baseline_before,
        args.baseline_after,
        parse_named_paths(args.root_before),
        parse_named_paths(args.root_after),
        parse_named_paths(args.shard),
    )
    if (args.definition129_authority is None) != (
        args.definition129_shard is None
    ):
        parser.error(
            "--definition129-authority and --definition129-shard are required together"
        )
    if args.definition129_authority is not None:
        compare_definition129_previews(
            args.definition129_authority,
            args.definition129_shard,
        )
    if (args.definition141_authority is None) != (
        args.definition141_shard is None
    ):
        parser.error(
            "--definition141-authority and --definition141-shard are required together"
        )
    if args.definition141_authority is not None:
        compare_definition141_previews(
            args.definition141_authority,
            args.definition141_shard,
        )


if __name__ == "__main__":
    main()
