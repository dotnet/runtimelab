import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

import yaml


REPO_ROOT = Path(__file__).resolve().parents[4]
PIPELINE = REPO_ROOT / "eng" / "pipelines" / "runtimelab-official.yml"
CONTRACT = (
    REPO_ROOT
    / "eng"
    / "pipelines"
    / "runtimelab"
    / "standard-gc-performance-consumers-v1.json"
)
REQUIREMENTS = (
    REPO_ROOT
    / "eng"
    / "pipelines"
    / "runtimelab"
    / "standard-gc-artifact-requirements-v2.json"
)
sys.path.insert(0, str(CONTRACT.parent))

import orchestrate_standard_gc_performance as coordination


class FakeClient:
    def __init__(self, ambiguous_definition=None):
        self.ambiguous_definition = ambiguous_definition
        self.runs = {definition_id: [] for definition_id in coordination.IMPLEMENTED_DEFINITION_IDS}
        self.queue_calls = []
        self.next_id = 1000

    def list_runs(self, definition_id):
        return copy.deepcopy(self.runs[definition_id])

    def get_run(self, definition_id, run_id):
        return copy.deepcopy(
            next(run for run in self.runs[definition_id] if run["id"] == run_id)
        )

    def queue_run(self, definition_id, request):
        self.queue_calls.append((definition_id, copy.deepcopy(request)))
        run = {
            "id": self.next_id,
            "state": "inProgress",
            "result": None,
            "resources": copy.deepcopy(request["resources"]),
            "variables": copy.deepcopy(request["variables"]),
        }
        self.next_id += 1
        self.runs[definition_id].append(run)
        if definition_id == self.ambiguous_definition:
            self.ambiguous_definition = None
            raise coordination.AmbiguousQueueResponse("connection closed")
        return {"id": run["id"]}

    def timeline(self, run_id):
        return {"records": [{"id": f"job-{run_id}", "type": "Job", "issues": []}]}

    def artifacts(self, run_id):
        return [{"name": f"ExternalRuntimeCompletion_{run_id}", "resource": {}}]


def request(definition_id, request_hash=None, attempt="1"):
    request_hash = request_hash or hashlib.sha256(str(definition_id).encode()).hexdigest()
    value = {
        "resources": {
            "repositories": {
                "self": {"refName": "refs/heads/test", "version": "a" * 40}
            }
        },
        "variables": {
            coordination.RUN_VARIABLES["mode"]: {
                "value": "external-runtime",
                "isSecret": False,
            },
            coordination.RUN_VARIABLES["campaign"]: {
                "value": "campaign",
                "isSecret": False,
            },
            coordination.RUN_VARIABLES["cohort"]: {
                "value": "cohort",
                "isSecret": False,
            },
            coordination.RUN_VARIABLES["manifest"]: {
                "value": "b" * 64,
                "isSecret": False,
            },
            coordination.RUN_VARIABLES["attempt"]: {
                "value": attempt,
                "isSecret": False,
            },
            coordination.RUN_VARIABLES["controller"]: {
                "value": "123",
                "isSecret": False,
            },
            coordination.RUN_VARIABLES["request"]: {
                "value": request_hash,
                "isSecret": False,
            },
        },
        "templateParameters": {"externalRuntimeAttempt": int(attempt)},
    }
    return value


class CoordinationTests(unittest.TestCase):
    def test_pipeline_mode_is_closed_and_default_off(self):
        pipeline = yaml.safe_load(PIPELINE.read_text(encoding="utf-8"))
        parameters = {item["name"]: item for item in pipeline["parameters"]}
        mode = parameters["standardGcPerformanceCoordination"]
        self.assertEqual("none", mode["default"])
        self.assertEqual(["none", "materialize", "queue"], mode["values"])
        text = PIPELINE.read_text(encoding="utf-8")
        self.assertIn("standardGcPerformanceCoordinationRequiresProducer", text)
        self.assertIn(
            "standardGcPerformanceCoordinationRequiresApprovedIdentityPosture", text
        )

    def test_contract_is_exact_and_aspnet_is_fail_closed(self):
        contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
        self.assertEqual(
            "e49b7ec1f45de576ae709ca25ef7715012ec25fdd425ef93633dce5b2d29245f",
            contract["coverageRowsSha256"],
        )
        self.assertEqual(
            "blocked-awaiting-final-scalar-binding",
            contract["aspNetBoundary"]["status"],
        )
        self.assertEqual(76, contract["aspNetBoundary"]["requiredRowCount"])
        self.assertEqual(7, contract["aspNetBoundary"]["requiredProfileCount"])
        self.assertEqual([], contract["aspNetBoundary"]["approvedBindingFileSha256"])
        self.assertEqual(
            [],
            contract["permissionDecision"]["approvedPostureFileSha256"],
        )
        self.assertEqual(
            "3dac235590d2ad8a2e4eede35ff0ac7900788836",
            contract["consumers"]["702"]["repositories"]["performance"]["version"],
        )
        self.assertEqual(
            "05ddec6583bb76930e70004efae61f1331444fcf6be6f84960abdaa422d39fd8",
            contract["consumers"]["1012"]["contractFileSha256"],
        )
        self.assertEqual(
            "19e0e75e2cd09421696b9167cdeb1b7c48c8a299d083b6adcb596d2042f800da",
            hashlib.sha256(REQUIREMENTS.read_bytes()).hexdigest(),
        )

    def test_matching_run_is_adopted_without_duplicate_post(self):
        client = FakeClient()
        expected = request(702)
        client.runs[702].append(
            {
                "id": 42,
                "state": "inProgress",
                "result": None,
                "resources": copy.deepcopy(expected["resources"]),
                "variables": copy.deepcopy(expected["variables"]),
            }
        )
        with tempfile.TemporaryDirectory() as directory:
            coordinator = coordination.PerformanceCoordinator(
                client, Path(directory), {702: expected}, True, {}
            )
            selected = coordinator.ensure_run(702, expected)
        self.assertTrue(selected["adopted"])
        self.assertEqual(42, selected["run"]["id"])
        self.assertEqual([], client.queue_calls)

    def test_duplicate_active_runs_fail_closed(self):
        client = FakeClient()
        expected = request(702)
        for run_id in (42, 43):
            client.runs[702].append(
                {
                    "id": run_id,
                    "state": "inProgress",
                    "result": None,
                    "resources": copy.deepcopy(expected["resources"]),
                    "variables": copy.deepcopy(expected["variables"]),
                }
            )
        with tempfile.TemporaryDirectory() as directory:
            coordinator = coordination.PerformanceCoordinator(
                client, Path(directory), {702: expected}, True, {}
            )
            with self.assertRaisesRegex(coordination.CoordinationError, "multiple active"):
                coordinator.ensure_run(702, expected)

    def test_ambiguous_post_is_adoption_only(self):
        client = FakeClient(ambiguous_definition=702)
        expected = request(702)
        with tempfile.TemporaryDirectory() as directory:
            coordinator = coordination.PerformanceCoordinator(
                client,
                Path(directory),
                {702: expected},
                True,
                {},
                poll_seconds=0,
                adoption_timeout_seconds=1,
                sleep=lambda _: None,
            )
            selected = coordinator.ensure_run(702, expected)
        self.assertTrue(selected["ambiguous"])
        self.assertTrue(selected["adopted"])
        self.assertEqual(1, len(client.queue_calls))

    def test_retry_identity_does_not_match_attempt_one(self):
        attempt_one = request(702, attempt="1")
        attempt_two = request(702, attempt="2")
        run = {
            "id": 42,
            "resources": attempt_one["resources"],
            "variables": attempt_one["variables"],
        }
        self.assertFalse(coordination.run_matches(run, attempt_two))

    def test_retry_request_is_exactly_attempt_two_with_new_hash(self):
        with tempfile.TemporaryDirectory() as directory:
            coordinator = coordination.PerformanceCoordinator(
                FakeClient(), Path(directory), {}, True, {}
            )
            attempt_one = request(702, attempt="1")
            attempt_two = coordinator.request_for_attempt(attempt_one, 2)
        self.assertEqual(2, attempt_two["templateParameters"]["externalRuntimeAttempt"])
        self.assertEqual(
            "2",
            attempt_two["variables"][coordination.RUN_VARIABLES["attempt"]]["value"],
        )
        self.assertNotEqual(
            attempt_one["variables"][coordination.RUN_VARIABLES["request"]]["value"],
            attempt_two["variables"][coordination.RUN_VARIABLES["request"]]["value"],
        )

    def test_permission_posture_requires_exact_live_identity(self):
        contract = json.loads(CONTRACT.read_text(encoding="utf-8"))

        class PermissionClient:
            def connection_data(self):
                return {
                    "authenticatedUser": {
                        "descriptor": "broad",
                        "providerDisplayName": "Project Collection Build Service (dnceng)",
                    }
                }

            def namespace(self):
                return {"value": [{"actions": [{"name": "QueueBuilds", "bit": 128}]}]}

            def queue_permission(self, definition_id, queue_bit):
                return {"definitionId": definition_id, "allowed": True}

        posture = {
            "schemaVersion": 1,
            "ownerDecisionCanonicalSha256": contract["permissionDecision"][
                "ownerDecisionCanonicalSha256"
            ],
            "decision": "dedicated-identity",
            "approvedBy": "owner",
            "approvedAtUtc": "2026-09-24T00:00:00Z",
            "expectedPrincipalDescriptor": "dedicated",
            "expectedPrincipalDisplayName": "definition895",
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "posture.json"
            path.write_text(json.dumps(posture), encoding="utf-8")
            with self.assertRaisesRegex(
                coordination.PermissionPreflightError, "not frozen"
            ):
                coordination.validate_identity_posture(
                    PermissionClient(), path, contract
                )

    def test_partial_cohort_blocks_all_requests(self):
        contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
        requirements = json.loads(REQUIREMENTS.read_text(encoding="utf-8"))
        rows = [
            {
                "rowId": row["rowId"],
                "status": "ready",
            }
            for row in requirements["downstreamRowMappings"]
            if row["definitionId"] in coordination.CONSUMER_DEFINITION_IDS
        ]
        rows[0]["status"] = "blocked"
        manifest = {
            "contract": {"coverageRowsSha256": contract["coverageRowsSha256"]},
            "identity": {
                "producerDefinitionId": 895,
                "sourceRepository": "dotnet/runtimelab",
                "sourceBranch": "refs/heads/feature/gc/baseline",
                "sourceCommit": "a" * 40,
            },
            "errors": [],
            "rows": rows,
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "manifest.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaisesRegex(coordination.CoordinationError, "blocked"):
                coordination.validate_manifest(path, REQUIREMENTS, contract)

    def test_definition306_receipt_identity_mismatch_is_rejected(self):
        manifest = {
            "contract": {"coverageRowsSha256": "a" * 64},
            "identity": {
                "sourceCommit": "b" * 40,
                "producerBuildId": 123,
                "campaignId": "campaign",
                "cohortId": "cohort",
            },
            "artifacts": {},
        }
        receipt = {
            "definitionId": 999,
            "coverageRowsSha256": "a" * 64,
            "sourceCommit": "b" * 40,
            "producerDefinitionId": 895,
            "producerBuildId": 123,
            "campaignId": "campaign",
            "cohortId": "cohort",
            "cohortManifestSha256": "c" * 64,
            "rows": [],
        }
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest_path = root / "standard-gc-cohort-manifest.json"
            manifest_path.write_text("{}", encoding="utf-8")
            validation = root / "ExternalRuntime306Validation" / "receipt.json"
            validation.parent.mkdir()
            validation.write_text(json.dumps(receipt), encoding="utf-8")
            with self.assertRaisesRegex(
                coordination.CoordinationError, "definitionId"
            ):
                coordination._artifact_map_306(
                    manifest,
                    {"downstreamRowMappings": []},
                    validation,
                )


if __name__ == "__main__":
    unittest.main()
