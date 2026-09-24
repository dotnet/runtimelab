import copy
import hashlib
import http.client
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import orchestrate_gc_validation

from orchestrate_gc_validation import (
    AmbiguousQueueResponse,
    ApiRequestError,
    AzureDevOpsClient,
    GcValidationOrchestrator,
    OrchestrationError,
    PermissionPreflightError,
    ROOTS,
    canonical_json_bytes,
    classify_transient_infrastructure_failure,
    evaluate_queue_permission,
    run_matches,
)


SOURCE_REF = "refs/heads/feature/gc/baseline"
SOURCE_VERSION = "1" * 40
CAMPAIGN_ID = "campaign-123"
PARENT_BUILD_ID = "456"
FIXTURES = Path(__file__).resolve().parent / "fixtures"


class FakeClient:
    pipeline_id = 163

    def __init__(
        self,
        results: dict[tuple[str, int], str] | None = None,
        timeline_messages: dict[tuple[str, int], list[str]] | None = None,
        ambiguous_root: str | None = None,
        complete_on_get: bool = True,
    ):
        self.results = results or {}
        self.timeline_messages = timeline_messages or {}
        self.ambiguous_root = ambiguous_root
        self.complete_on_get = complete_on_get
        self.runs = []
        self.queue_calls = []
        self.get_calls = []
        self.timeline_calls = []
        self.next_id = 1000

    def preflight_queue_permission(self) -> dict:
        return {
            "allowed": True,
            "descriptor": "build-service",
            "permissionToken": "project/163",
            "queueBuildsBit": 128,
            "effectiveAllow": 128,
            "effectiveDeny": 0,
        }

    def list_runs(self) -> list[dict]:
        return [
            {
                key: copy.deepcopy(value)
                for key, value in run.items()
                if key != "resources"
            }
            | {"templateParameters": {}}
            for run in self.runs
        ]

    def queue_run(self, request: dict) -> dict:
        self.queue_calls.append(copy.deepcopy(request))
        root = request["templateParameters"]["gcValidationRoot"]
        attempt = int(request["templateParameters"]["gcValidationAttempt"])
        run = {
            "id": self.next_id,
            "name": f"run-{self.next_id}",
            "url": f"https://example/{self.next_id}",
            "state": "inProgress",
            "result": None,
            "resources": copy.deepcopy(request["resources"]),
            "templateParameters": {},
            "variables": copy.deepcopy(request["variables"]),
        }
        self.next_id += 1
        self.runs.append(run)
        if root == self.ambiguous_root:
            self.ambiguous_root = None
            raise AmbiguousQueueResponse("connection closed")
        return copy.deepcopy(run)

    def get_run(self, run_id: int) -> dict:
        self.get_calls.append(run_id)
        run = next(run for run in self.runs if run["id"] == run_id)
        root = run["variables"]["GC_VALIDATION_ROOT"]["value"]
        attempt = int(run["variables"]["GC_VALIDATION_ATTEMPT"]["value"])
        if self.complete_on_get:
            run["state"] = "completed"
            run["result"] = self.results.get((root, attempt), "succeeded")
        return copy.deepcopy(run)

    def get_timeline(self, run_id: int) -> dict:
        self.timeline_calls.append(run_id)
        run = next(run for run in self.runs if run["id"] == run_id)
        root = run["variables"]["GC_VALIDATION_ROOT"]["value"]
        attempt = int(run["variables"]["GC_VALIDATION_ATTEMPT"]["value"])
        return {
            "records": [
                {
                    "issues": [
                        {"type": "error", "message": message}
                        for message in self.timeline_messages.get((root, attempt), [])
                    ]
                }
            ]
        }


def create_orchestrator(
    client: FakeClient, output_directory: Path
) -> GcValidationOrchestrator:
    return GcValidationOrchestrator(
        client,
        output_directory,
        CAMPAIGN_ID,
        PARENT_BUILD_ID,
        SOURCE_REF,
        SOURCE_VERSION,
        monitor_timeout_seconds=60,
        poll_seconds=0,
        adoption_timeout_seconds=1,
        sleep=lambda _: None,
    )


class GcValidationOrchestrationTests(unittest.TestCase):
    def test_api_client_uses_runtime_bearer_token(self) -> None:
        client = AzureDevOpsClient(
            "https://dev.azure.com/example",
            "project",
            163,
            "runtime-token",
        )
        captured = {}

        class Response:
            def __enter__(self):
                return self

            def __exit__(self, *_):
                return False

            def read(self) -> bytes:
                return b"{}"

        def open_request(request, timeout):
            captured["authorization"] = request.get_header("Authorization")
            captured["timeout"] = timeout
            return Response()

        original_urlopen = orchestrate_gc_validation.urllib.request.urlopen
        try:
            orchestrate_gc_validation.urllib.request.urlopen = open_request
            client.request_json("GET", "https://example.test")
        finally:
            orchestrate_gc_validation.urllib.request.urlopen = original_urlopen

        self.assertEqual("Bearer runtime-token", captured["authorization"])
        self.assertEqual(60, captured["timeout"])

    def test_successful_post_protocol_and_decode_failures_are_ambiguous(
        self,
    ) -> None:
        client = AzureDevOpsClient(
            "https://dev.azure.com/example",
            "project",
            163,
            "runtime-token",
        )

        class Response:
            def __init__(self, value):
                self.value = value

            def __enter__(self):
                return self

            def __exit__(self, *_):
                return False

            def read(self) -> bytes:
                if isinstance(self.value, Exception):
                    raise self.value
                return self.value

        failures = (
            Response(http.client.IncompleteRead(b'{"id":', 1)),
            http.client.BadStatusLine("invalid status"),
            Response(b"{"),
            Response(b"\xff"),
        )
        original_urlopen = orchestrate_gc_validation.urllib.request.urlopen
        try:
            for failure in failures:
                with self.subTest(failure=type(failure).__name__):

                    def open_request(*_args, **_kwargs):
                        if isinstance(failure, Exception):
                            raise failure
                        return failure

                    orchestrate_gc_validation.urllib.request.urlopen = open_request
                    with self.assertRaises(AmbiguousQueueResponse):
                        client.request_json(
                            "POST",
                            "https://example.test/runs",
                            {"previewRun": False},
                        )
                    with self.assertRaises(ApiRequestError):
                        client.request_json("GET", "https://example.test/runs")
        finally:
            orchestrate_gc_validation.urllib.request.urlopen = original_urlopen

    def test_queue_permission_requires_effective_allow_without_deny(self) -> None:
        namespace = {
            "value": [
                {
                    "actions": [
                        {"name": "QueueBuilds", "bit": 128},
                    ]
                }
            ]
        }
        connection = {
            "authenticatedUser": {
                "descriptor": "build-service",
                "providerDisplayName": "Public Build Service",
            }
        }
        permission = {"count": 1, "value": [True]}
        result = evaluate_queue_permission(
            namespace, connection, permission, "project/163"
        )
        self.assertTrue(result["allowed"])

        permission["value"][0] = False
        result = evaluate_queue_permission(
            namespace, connection, permission, "project/163"
        )
        self.assertFalse(result["allowed"])

    def test_permission_preflight_fails_before_queue_when_permission_is_missing(
        self,
    ) -> None:
        client = AzureDevOpsClient(
            "https://dev.azure.com/example",
            "project",
            163,
            "runtime-token",
        )
        requested_urls = []

        def request_json(method, url, body=None, timeout_seconds=60):
            requested_urls.append(url)
            if "securitynamespaces" in url:
                return {
                    "value": [
                        {
                            "actions": [
                                {"name": "QueueBuilds", "bit": 128},
                            ]
                        }
                    ]
                }
            if "connectionData" in url:
                return {
                    "authenticatedUser": {
                        "descriptor": "build-service",
                        "providerDisplayName": "Public Build Service",
                    }
                }
            if "_apis/permissions/" in url:
                return {"count": 1, "value": [False]}
            self.fail(f"Unexpected URL {url}")

        client.request_json = request_json
        with self.assertRaisesRegex(
            PermissionPreflightError,
            "QueueBuilds is not effectively allowed for Public Build Service",
        ):
            client.preflight_queue_permission()

        self.assertTrue(any("_apis/permissions/" in url for url in requested_urls))
        self.assertFalse(any("_apis/pipelines/" in url for url in requested_urls))

    def test_child_request_is_source_pinned_and_has_no_yaml_override(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            orchestrator = create_orchestrator(
                FakeClient(), Path(directory)
            )
            request = orchestrator.build_request(ROOTS[0], 1)

        self.assertNotIn("yamlOverride", request)
        self.assertEqual(
            {
                "refName": SOURCE_REF,
                "version": SOURCE_VERSION,
            },
            request["resources"]["repositories"]["self"],
        )
        self.assertEqual(
            {
                "gcValidationMode": "correctness-shard",
                "gcValidationRoot": ROOTS[0],
                "gcValidationCampaignId": CAMPAIGN_ID,
                "gcValidationParentBuildId": PARENT_BUILD_ID,
                "gcValidationAttempt": "1",
            },
            request["templateParameters"],
        )
        self.assertEqual(
            {
                "GC_VALIDATION_MODE": {
                    "value": "correctness-shard",
                    "isSecret": False,
                },
                "GC_VALIDATION_CAMPAIGN_ID": {
                    "value": CAMPAIGN_ID,
                    "isSecret": False,
                },
                "GC_VALIDATION_PARENT_BUILD_ID": {
                    "value": PARENT_BUILD_ID,
                    "isSecret": False,
                },
                "GC_VALIDATION_ROOT": {
                    "value": ROOTS[0],
                    "isSecret": False,
                },
                "GC_VALIDATION_ATTEMPT": {
                    "value": "1",
                    "isSecret": False,
                },
            },
            request["variables"],
        )

    def test_run_match_includes_campaign_root_parent_and_source(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            orchestrator = create_orchestrator(
                FakeClient(), Path(directory)
            )
            request = orchestrator.build_request(ROOTS[0], 1)
        run = {
            "id": 123,
            "resources": request["resources"],
            "templateParameters": {},
            "variables": request["variables"],
        }
        self.assertTrue(
            run_matches(
                run,
                campaign_id=CAMPAIGN_ID,
                parent_build_id=PARENT_BUILD_ID,
                root=ROOTS[0],
                source_ref=SOURCE_REF,
                source_version=SOURCE_VERSION,
                attempt=1,
            )
        )
        run["resources"]["repositories"]["self"]["version"] = "2" * 40
        self.assertFalse(
            run_matches(
                run,
                campaign_id=CAMPAIGN_ID,
                parent_build_id=PARENT_BUILD_ID,
                root=ROOTS[0],
                source_ref=SOURCE_REF,
                source_version=SOURCE_VERSION,
            )
        )
        run["resources"]["repositories"]["self"]["version"] = SOURCE_VERSION
        run["id"] = -1
        self.assertFalse(
            run_matches(
                run,
                campaign_id=CAMPAIGN_ID,
                parent_build_id=PARENT_BUILD_ID,
                root=ROOTS[0],
                source_ref=SOURCE_REF,
                source_version=SOURCE_VERSION,
            )
        )

    def test_matching_runs_hydrates_captured_list_shape_with_get(self) -> None:
        listing = json.loads(
            (FIXTURES / "definition163-runs-list.json").read_text(encoding="utf-8")
        )
        detail = json.loads(
            (FIXTURES / "definition163-run-get.json").read_text(encoding="utf-8")
        )
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient()
            orchestrator = create_orchestrator(client, Path(directory))
            request = orchestrator.build_request(ROOTS[0], 1)
            returned_variables = {
                name.lower(): copy.deepcopy(value)
                for name, value in request["variables"].items()
            }
            detail["variables"] = copy.deepcopy(returned_variables)
            detail["resources"] = copy.deepcopy(request["resources"])
            get_calls = []
            client.list_runs = lambda: copy.deepcopy(listing["value"])

            def get_run(run_id):
                get_calls.append(run_id)
                return copy.deepcopy(detail)

            client.get_run = get_run

            matches = orchestrator.matching_runs(ROOTS[0])

        self.assertEqual([1607407], [run["id"] for run in matches])
        self.assertEqual([1607407], get_calls)
        self.assertNotIn("resources", listing["value"][0])
        self.assertEqual({}, matches[0]["templateParameters"])
        self.assertEqual(
            SOURCE_VERSION,
            matches[0]["resources"]["repositories"]["self"]["version"],
        )

    def test_ambiguous_queue_response_adopts_matching_run_without_requeue(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient(ambiguous_root=ROOTS[0])
            orchestrator = create_orchestrator(client, Path(directory))
            attempt = orchestrator.ensure_run(ROOTS[0], 1)
            receipt = json.loads(orchestrator.receipt_path.read_text(encoding="utf-8"))

        self.assertEqual(1, len(client.queue_calls))
        self.assertTrue(attempt["adopted"])
        self.assertTrue(attempt["ambiguousQueueResponse"])
        self.assertEqual(1000, attempt["runId"])
        self.assertEqual(1, receipt["queueRequestsSubmitted"])
        self.assertEqual(1, receipt["runsQueued"])
        self.assertEqual(
            1000, receipt["children"][ROOTS[0]]["currentRunId"]
        )

    def test_invalid_successful_post_shape_is_adopted_without_requeue(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient()
            queue_run = client.queue_run

            def queue_without_run_shape(request):
                queue_run(request)
                return []

            client.queue_run = queue_without_run_shape
            orchestrator = create_orchestrator(client, Path(directory))
            attempt = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertEqual(1, len(client.queue_calls))
        self.assertTrue(attempt["adopted"])
        self.assertTrue(attempt["ambiguousQueueResponse"])
        self.assertEqual(1000, attempt["runId"])

    def test_duplicate_matching_attempts_fail_before_queue(self) -> None:
        for attempt in (1, 2):
            with self.subTest(attempt=attempt):
                with tempfile.TemporaryDirectory() as directory:
                    client = FakeClient(complete_on_get=False)
                    orchestrator = create_orchestrator(client, Path(directory))
                    request = orchestrator.build_request(ROOTS[0], attempt)
                    for run_id in (1, 2):
                        client.queue_run(request)
                        client.runs[-1]["id"] = run_id
                    client.queue_calls.clear()
                    with self.assertRaisesRegex(
                        OrchestrationError,
                        f"duplicate attempt {attempt} runs",
                    ):
                        orchestrator.ensure_run(ROOTS[0], attempt)

                self.assertEqual([], client.queue_calls)

    def test_attempt_two_without_attempt_one_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient(complete_on_get=False)
            orchestrator = create_orchestrator(client, Path(directory))
            client.queue_run(orchestrator.build_request(ROOTS[0], 2))
            client.queue_calls.clear()

            with self.assertRaisesRegex(
                OrchestrationError, "attempt 2 exists without attempt 1"
            ):
                orchestrator.ensure_run(ROOTS[0], 2)

        self.assertEqual([], client.queue_calls)

    def test_attempt_two_requires_terminal_unsuccessful_attempt_one(self) -> None:
        scenarios = (
            ("inProgress", None, "is not terminal"),
            ("completed", "succeeded", "did not finish unsuccessfully"),
        )
        for state, result, expected in scenarios:
            with self.subTest(state=state, result=result):
                with tempfile.TemporaryDirectory() as directory:
                    client = FakeClient(complete_on_get=False)
                    orchestrator = create_orchestrator(client, Path(directory))
                    for attempt in (1, 2):
                        client.queue_run(
                            orchestrator.build_request(ROOTS[0], attempt)
                        )
                    client.queue_calls.clear()
                    client.runs[0]["state"] = state
                    client.runs[0]["result"] = result

                    with self.assertRaisesRegex(OrchestrationError, expected):
                        orchestrator.ensure_run(ROOTS[0], 2)

                self.assertEqual([], client.queue_calls)
                self.assertEqual([], client.timeline_calls)

    def test_deterministic_attempt_one_rejects_existing_attempt_two(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient(
                timeline_messages={(ROOTS[0], 1): ["GCStress test failed."]},
                complete_on_get=False,
            )
            orchestrator = create_orchestrator(client, Path(directory))
            for attempt in (1, 2):
                client.queue_run(orchestrator.build_request(ROOTS[0], attempt))
                client.runs[-1]["state"] = "completed"
                client.runs[-1]["result"] = "failed"
            client.queue_calls.clear()

            with self.assertRaisesRegex(
                OrchestrationError,
                "not a proven transient infrastructure failure",
            ):
                orchestrator.ensure_run(ROOTS[0], 2)

        self.assertEqual([], client.queue_calls)
        self.assertEqual([1000], client.timeline_calls)

    def test_only_proven_infrastructure_failure_is_transient(self) -> None:
        transient = classify_transient_infrastructure_failure(
            {
                "records": [
                    {
                        "issues": [
                            {
                                "type": "error",
                                "message": "We stopped hearing from agent build-1.",
                            },
                            {
                                "type": "error",
                                "message": "The operation was canceled.",
                            },
                        ]
                    }
                ]
            }
        )
        deterministic = classify_transient_infrastructure_failure(
            {
                "records": [
                    {
                        "issues": [
                            {
                                "type": "error",
                                "message": "Test GCStress failed with exit code 1.",
                            }
                        ]
                    }
                ]
            }
        )
        product_infrastructure_error = classify_transient_infrastructure_failure(
            {
                "records": [
                    {
                        "issues": [
                            {
                                "type": "error",
                                "message": "GC infrastructure error in the test process.",
                            }
                        ]
                    }
                ]
            }
        )
        self.assertTrue(transient["isTransientInfrastructureFailure"])
        self.assertFalse(deterministic["isTransientInfrastructureFailure"])
        self.assertFalse(
            product_infrastructure_error["isTransientInfrastructureFailure"]
        )

    def test_success_requires_five_terminal_child_receipts(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient()
            orchestrator = create_orchestrator(client, Path(directory))
            self.assertTrue(orchestrator.run())
            receipt_bytes = orchestrator.receipt_path.read_bytes()
            receipt = json.loads(receipt_bytes)
            self.assertEqual(5, len(client.queue_calls))
            self.assertEqual(5, receipt["queueRequestsSubmitted"])
            self.assertEqual(5, receipt["runsQueued"])
            self.assertTrue(receipt["allTerminalReceipts"])
            self.assertTrue(receipt["succeeded"])
            self.assertEqual(set(ROOTS), set(receipt["children"]))
            for root in ROOTS:
                child = receipt["children"][root]
                self.assertTrue(child["terminalReceiptPresent"])
                self.assertEqual("succeeded", child["finalResult"])
                request_path = Path(directory) / child["attempts"][0]["requestFile"]
                self.assertEqual(
                    child["attempts"][0]["requestSha256"],
                    hashlib.sha256(request_path.read_bytes()).hexdigest(),
                )
            canonical = dict(receipt)
            expected_hash = canonical.pop("canonicalSha256")
            self.assertEqual(
                expected_hash,
                hashlib.sha256(canonical_json_bytes(canonical)).hexdigest(),
            )

    def test_completed_campaign_is_adopted_without_new_queue_requests(self) -> None:
        with tempfile.TemporaryDirectory() as first_directory:
            client = FakeClient()
            first = create_orchestrator(client, Path(first_directory))
            self.assertTrue(first.run())
        with tempfile.TemporaryDirectory() as second_directory:
            second = create_orchestrator(client, Path(second_directory))
            self.assertTrue(second.run())
            receipt = json.loads(second.receipt_path.read_text(encoding="utf-8"))

        self.assertEqual(5, len(client.queue_calls))
        self.assertEqual(0, receipt["queueRequestsSubmitted"])
        self.assertEqual(0, receipt["runsQueued"])
        self.assertTrue(
            all(
                child["attempts"][0]["adopted"]
                for child in receipt["children"].values()
            )
        )

    def test_transient_failure_retries_once(self) -> None:
        root = ROOTS[0]
        client = FakeClient(
            results={(root, 1): "failed", (root, 2): "succeeded"},
            timeline_messages={
                (root, 1): ["We stopped hearing from agent build-1."]
            },
        )
        with tempfile.TemporaryDirectory() as directory:
            orchestrator = create_orchestrator(client, Path(directory))
            self.assertTrue(orchestrator.run())
            receipt = json.loads(orchestrator.receipt_path.read_text(encoding="utf-8"))

        self.assertEqual(6, len(client.queue_calls))
        self.assertEqual(2, receipt["children"][root]["finalAttempt"])
        self.assertEqual(2, len(receipt["children"][root]["attempts"]))
        with tempfile.TemporaryDirectory() as directory:
            resumed = create_orchestrator(client, Path(directory))
            self.assertTrue(resumed.run())
            resumed_receipt = json.loads(
                resumed.receipt_path.read_text(encoding="utf-8")
            )
        self.assertEqual(6, len(client.queue_calls))
        self.assertEqual(2, len(resumed_receipt["children"][root]["attempts"]))

    def test_deterministic_failure_is_not_retried(self) -> None:
        root = ROOTS[0]
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient(
                results={(root, 1): "failed"},
                timeline_messages={(root, 1): ["GCStress test failed."]},
            )
            orchestrator = create_orchestrator(client, Path(directory))
            self.assertFalse(orchestrator.run())
            receipt = json.loads(orchestrator.receipt_path.read_text(encoding="utf-8"))

        self.assertEqual(5, len(client.queue_calls))
        self.assertEqual(1, receipt["children"][root]["finalAttempt"])
        self.assertFalse(
            receipt["children"][root]["attempts"][0]["failureClassification"][
                "isTransientInfrastructureFailure"
            ]
        )


if __name__ == "__main__":
    unittest.main()
