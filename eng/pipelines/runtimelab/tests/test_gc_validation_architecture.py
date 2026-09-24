import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from compare_gc_validation_previews import compare_previews, normalize_inert_metadata
from orchestrate_gc_validation import ROOTS as ORCHESTRATION_ROOTS


REPO_ROOT = Path(__file__).resolve().parents[4]
RUNTIMELAB_PIPELINE_PATH = REPO_ROOT / "eng" / "pipelines" / "runtimelab.yml"
TEMPLATE_ROOT = (
    REPO_ROOT / "eng" / "pipelines" / "coreclr" / "templates" / "gc-validation"
)
ROOTS = {
    "gcstress0x3-gcstress0xc": {
        "graph_hash": "a35522ff971c43a169d3b89e81d41d1245f42cbcd5df36fbd9e8e32a0e1c159b",
        "contract_hash": "64549eb763e69259633399c29bd2f653fdbff31d41502eff5228f58c2d5d498f",
        "preamble_hash": "1623bf7600d0a4a08c40e94bc033bda155b14f9de26aff3c4204a6425e6fd994",
    },
    "gcstress-extra": {
        "graph_hash": "684659b5172dcbc8be92a214079731bd3f8ec06b263041ab85fb95b51edff5de",
        "contract_hash": "e8bfee1e2592867bb05c4d8bdb0135eefa8f7ff5a3096bcb33ff4833638b75f2",
        "preamble_hash": "945faabf6168c3adb5993ee117e5aed3bec1604e4413f282038ee5246027f41a",
    },
    "gc-longrunning": {
        "graph_hash": "1ec05a61e36729cd0c54f800188734f4d31189c13ddb70dbf41637348be07349",
        "contract_hash": "93c73aad538f1b846dd8f2e538a9e6f08fd81147bf176c7c36b0202d4023b1b3",
        "preamble_hash": "a247af48fed326f59b34f261d0b92169816a8df4ed48024e94a344cb63c37f5d",
    },
    "gc-simulator": {
        "graph_hash": "c651f1a1ea23d76c0e7e48e1397c88ad593d689b9489ca2cc414355f61884104",
        "contract_hash": "45fe208fe865ea8e29be8fe395bee40de434b75c8ca99e4c47e9a7b092352afb",
        "preamble_hash": "5fe75f86a593994e56cb2b3a39c55686a1bf0d39554feba8606c9473bdad3932",
    },
    "gc-standalone": {
        "graph_hash": "d94699f8fc5cb9a56c6987643aead6ac7a910b34877b6139235e59126b6c9359",
        "contract_hash": "45fe208fe865ea8e29be8fe395bee40de434b75c8ca99e4c47e9a7b092352afb",
        "preamble_hash": "2cf11cb9b4f761897a6d8db580871a8145e80a992e83fc0bea5fb3000f39bad2",
    },
}
BASELINE_PIPELINE_HASH = (
    "b8732c8723cd1da1ddaaa92134a2332e04fca50be0af29e436ca04dee658aa3b"
)
BASELINE_CONDITION = "${{ if eq(parameters.gcValidationMode, 'baseline') }}"
STRESS_CONDITION = "${{ if eq(parameters.gcValidationMode, 'correctness-stress') }}"
MONITOR_ARGUMENT = "${{ variables.enableHelixJobMonitor }}"
MONITOR_PARAMETER_CONDITION = (
    "${{ if eq(parameters.enableHelixJobMonitor, 'true') }}"
)
MONITOR_VARIABLE_CONDITION = (
    "${{ if eq(variables['enableHelixJobMonitor'], true) }}"
)


def load_yaml(path: Path) -> dict:
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def canonical_hash(value: object) -> str:
    serialized = json.dumps(value, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(serialized.encode()).hexdigest()


def root_contract(root: dict) -> dict:
    contract = {key: value for key, value in root.items() if key != "extends"}
    contract["extends"] = {
        key: value for key, value in root["extends"].items() if key != "parameters"
    }
    parameters = {
        key: value
        for key, value in root["extends"]["parameters"].items()
        if key != "stages"
    }
    if parameters:
        contract["extends"]["parameters"] = parameters
    return contract


def parameter_map(pipeline: dict) -> dict:
    return {parameter["name"]: parameter for parameter in pipeline["parameters"]}


def stage_condition_map(pipeline: dict) -> dict:
    return {
        next(iter(item)): next(iter(item.values()))
        for item in pipeline["extends"]["parameters"]["stages"]
        if isinstance(item, dict)
        and len(item) == 1
        and next(iter(item)).startswith("${{ if ")
    }


def authoritative_graph(value: object) -> object:
    if isinstance(value, dict):
        return {
            (
                MONITOR_VARIABLE_CONDITION
                if key == MONITOR_PARAMETER_CONDITION
                else key
            ): authoritative_graph(item)
            for key, item in value.items()
        }
    if isinstance(value, list):
        return [authoritative_graph(item) for item in value]
    if value == "${{ parameters.enableHelixJobMonitor }}":
        return "${{ variables.enableHelixJobMonitor }}"
    return value


class GcValidationArchitectureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.pipeline = load_yaml(RUNTIMELAB_PIPELINE_PATH)
        cls.parameters = parameter_map(cls.pipeline)
        cls.conditions = stage_condition_map(cls.pipeline)

    def test_authoritative_roots_preserve_contract_and_exact_graph(self) -> None:
        for name, expected in ROOTS.items():
            with self.subTest(root=name):
                root = load_yaml(
                    REPO_ROOT / "eng" / "pipelines" / "coreclr" / f"{name}.yml"
                )
                root_text = (
                    REPO_ROOT / "eng" / "pipelines" / "coreclr" / f"{name}.yml"
                ).read_text(encoding="utf-8")
                template_path = TEMPLATE_ROOT / f"{name}-stages.yml"
                self.assertEqual(
                    [
                        {
                            "template": (
                                f"/{template_path.relative_to(REPO_ROOT).as_posix()}"
                            ),
                            "parameters": {
                                "enableHelixJobMonitor": MONITOR_ARGUMENT,
                            },
                        }
                    ],
                    root["extends"]["parameters"]["stages"],
                )
                self.assertEqual(
                    expected["preamble_hash"],
                    hashlib.sha256(root_text.split("extends:", 1)[0].encode()).hexdigest(),
                )
                self.assertEqual(
                    expected["contract_hash"], canonical_hash(root_contract(root))
                )
                self.assertEqual(
                    expected["graph_hash"],
                    canonical_hash(
                        authoritative_graph(load_yaml(template_path)["stages"])
                    ),
                )
                self.assertEqual(
                    [{"name": "enableHelixJobMonitor", "type": "string"}],
                    load_yaml(template_path)["parameters"],
                )

    def test_validation_parameters_are_closed_and_default_off(self) -> None:
        self.assertEqual(
            ["baseline", "correctness-stress", "correctness-shard"],
            self.parameters["gcValidationMode"]["values"],
        )
        self.assertEqual("baseline", self.parameters["gcValidationMode"]["default"])
        self.assertEqual(list(ROOTS), self.parameters["gcValidationRoot"]["values"])
        self.assertEqual(tuple(ROOTS), ORCHESTRATION_ROOTS)
        self.assertEqual(
            "gcstress0x3-gcstress0xc",
            self.parameters["gcValidationRoot"]["default"],
        )
        self.assertEqual("string", self.parameters["gcValidationCampaignId"]["type"])
        self.assertEqual("", self.parameters["gcValidationCampaignId"]["default"])
        self.assertEqual(
            "string", self.parameters["gcValidationParentBuildId"]["type"]
        )
        self.assertEqual("", self.parameters["gcValidationParentBuildId"]["default"])
        self.assertEqual("number", self.parameters["gcValidationAttempt"]["type"])
        self.assertEqual(1, self.parameters["gcValidationAttempt"]["default"])
        self.assertEqual([1, 2], self.parameters["gcValidationAttempt"]["values"])

    def test_baseline_authored_structure_is_exact(self) -> None:
        baseline = copy.deepcopy(self.pipeline)
        baseline.pop("parameters")
        baseline["extends"]["parameters"]["stages"] = self.conditions[
            BASELINE_CONDITION
        ]
        self.assertEqual(BASELINE_PIPELINE_HASH, canonical_hash(baseline))

    def test_each_shard_imports_exactly_one_shared_graph(self) -> None:
        expected_conditions = {}
        for name in ROOTS:
            condition = (
                "${{ if and(eq(parameters.gcValidationMode, 'correctness-shard'), "
                f"eq(parameters.gcValidationRoot, '{name}')) }}}}"
            )
            template = (
                f"/eng/pipelines/coreclr/templates/gc-validation/{name}-stages.yml"
            )
            expected_conditions[condition] = [
                {
                    "template": template,
                    "parameters": {
                        "enableHelixJobMonitor": MONITOR_ARGUMENT,
                    },
                }
            ]

        actual_conditions = {
            condition: value
            for condition, value in self.conditions.items()
            if "correctness-shard" in condition
        }
        self.assertEqual(expected_conditions, actual_conditions)

    def test_parent_orchestration_queues_only_closed_shards(self) -> None:
        stress_stages = self.conditions[STRESS_CONDITION]
        self.assertEqual(1, len(stress_stages))
        self.assertEqual(
            "GCValidationCorrectnessStress", stress_stages[0]["stage"]
        )
        jobs = stress_stages[0]["jobs"]
        self.assertEqual(1, len(jobs))
        self.assertEqual(
            "QueueAndMonitorGcValidationShards", jobs[0]["job"]
        )
        steps = jobs[0]["steps"]
        orchestration_step = next(
            step
            for step in steps
            if step.get("displayName") == "Queue and monitor child runs"
        )
        self.assertIn("orchestrate_gc_validation.py", orchestration_step["bash"])
        self.assertEqual(
            "$(System.AccessToken)",
            orchestration_step["env"]["SYSTEM_ACCESSTOKEN"],
        )
        self.assertEqual(
            "${{ parameters.gcValidationCampaignId }}",
            orchestration_step["env"]["GC_VALIDATION_CAMPAIGN_ID"],
        )
        self.assertEqual(
            "${{ parameters.gcValidationParentBuildId }}",
            orchestration_step["env"]["GC_VALIDATION_PARENT_BUILD_ID"],
        )
        publish_step = next(
            step for step in steps if step.get("task") == "PublishPipelineArtifact@1"
        )
        self.assertEqual("always()", publish_step["condition"])
        receipt_gate = next(
            step
            for step in steps
            if step.get("displayName") == "Require successful child receipts"
        )
        self.assertIn("allTerminalReceipts", receipt_gate["bash"])
        self.assertIn("len(receipt.get('children', {})) == 5", receipt_gate["bash"])
        pipeline_text = RUNTIMELAB_PIPELINE_PATH.read_text(encoding="utf-8")
        self.assertNotIn("yamlOverride", pipeline_text)
        self.assertNotIn(
            "gcValidationCorrectnessStressRequiresQueueBuildPermission",
            pipeline_text,
        )

    def test_real_preview_comparator_checks_baseline_roots_and_shards(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)

            def write(name: str, final_yaml: str) -> Path:
                path = root / f"{name}.json"
                path.write_text(
                    json.dumps({"finalYaml": final_yaml}), encoding="utf-8"
                )
                return path

            baseline_before = write("baseline-before", "stages:\n- stage: Baseline\n")
            baseline_after = write(
                "baseline-after",
                "parameters:\n- name: AddedSelector\nstages:\n- stage: Baseline\n",
            )
            root_before = write(
                "root-before",
                "stages:\n- stage: Shared\n  jobs:\n  - job: Build\n",
            )
            root_after = write(
                "root-after",
                "stages:\n- stage: Shared\n  jobs:\n  - job: Build\n",
            )
            shard = write(
                "shard",
                "stages:\n- stage: Shared\n  jobs:\n  - job: Build\n"
                "    templateContext: {}\n",
            )

            compare_previews(
                baseline_before,
                baseline_after,
                {"sample": root_before},
                {"sample": root_after},
                {"sample": shard},
            )

    def test_only_empty_template_context_is_normalized(self) -> None:
        self.assertEqual(
            {"job": "Build"},
            normalize_inert_metadata({"job": "Build", "templateContext": {}}),
        )
        self.assertEqual(
            {"templateContext": {"outputs": []}},
            normalize_inert_metadata({"templateContext": {"outputs": []}}),
        )


if __name__ == "__main__":
    unittest.main()
