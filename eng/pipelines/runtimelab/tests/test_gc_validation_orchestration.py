from concurrent.futures import ThreadPoolExecutor
from contextlib import redirect_stdout
import copy
import hashlib
import http.client
import io
import json
from pathlib import Path
import ssl
import sys
import tempfile
import threading
import unittest
import urllib.error
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import orchestrate_gc_validation

from orchestrate_gc_validation import (
    AmbiguousQueueResponse,
    ApiRequestError,
    AzureDevOpsClient,
    GcValidationAdmission,
    GcValidationOrchestrator,
    OrchestrationError,
    PermissionPreflightError,
    ROOTS,
    RUN_KIND_DIRECT,
    RUN_KIND_PARENT_CHILD,
    canonical_json_bytes,
    classify_transient_infrastructure_failure,
    emit_run_identity_tags,
    evaluate_queue_permission,
    hydrated_matching_runs,
    identity_tags,
    run_matches,
    run_tag_value,
    validate_shard_identity,
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
        self.artifact_list_calls = []
        self.artifact_download_calls = []
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
            "tags": list(
                identity_tags(
                    run_kind=request["templateParameters"][
                        "gcValidationRunKind"
                    ],
                    campaign_id=request["templateParameters"][
                        "gcValidationCampaignId"
                    ],
                    parent_build_id=request["templateParameters"][
                        "gcValidationParentBuildId"
                    ],
                    root=root,
                    attempt=attempt,
                )
            ),
            "nativeScenario": "genuine",
            "buildResult": None,
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
        root = run_tag_value(run, "root")
        attempt = int(run_tag_value(run, "attempt"))
        if self.complete_on_get:
            run["state"] = "completed"
            run["result"] = self.results.get((root, attempt), "succeeded")
            if run["buildResult"] is None:
                run["buildResult"] = run["result"]
        return copy.deepcopy(run)

    def get_timeline(self, run_id: int) -> dict:
        self.timeline_calls.append(run_id)
        run = next(run for run in self.runs if run["id"] == run_id)
        root = run_tag_value(run, "root")
        attempt = int(run_tag_value(run, "attempt"))
        timeline = json.loads(
            (FIXTURES / "definition163-child-timeline.json").read_text(
                encoding="utf-8"
            )
        )
        scenario = run["nativeScenario"]
        if scenario == "timeline-pending" and run["state"] != "completed":
            return {"records": []}
        if scenario == "timeline-null" and run["state"] != "completed":
            return {"records": None}
        if scenario == "terminal-timeline-null":
            return {"records": None}
        if scenario == "baseline-forgery":
            timeline["records"] = [
                record
                for record in timeline["records"]
                if record["identifier"] == "build"
            ]
        admission_state = (
            "completed" if run["state"] == "completed" else "inProgress"
        )
        for record in timeline["records"]:
            if record["identifier"] in (
                "gcvalidationadmission",
                "gcvalidationadmission.AdmitCanonicalGcValidationShard",
            ):
                record["state"] = admission_state
                record["result"] = (
                    "succeeded" if admission_state == "completed" else None
                )
            if record["identifier"] == "build":
                record["state"] = (
                    "completed" if run["state"] == "completed" else "pending"
                )
                if scenario == "skipped-build":
                    record["result"] = "skipped"
                elif run["state"] == "completed":
                    record["result"] = run["buildResult"] or run["result"]
                else:
                    record["result"] = None
                if (
                    scenario == "terminal-timeline-pending"
                    and run["state"] == "completed"
                ):
                    record["state"] = "inProgress"
                    record["result"] = None
                record["issues"] = [
                    {"type": "error", "message": message}
                    for message in self.timeline_messages.get((root, attempt), [])
                ]
        return timeline

    def list_artifacts(self, run_id: int) -> list[dict]:
        self.artifact_list_calls.append(run_id)
        run = next(run for run in self.runs if run["id"] == run_id)
        if run["nativeScenario"] in (
            "baseline-forgery",
            "artifact-pending",
        ):
            return []
        response = json.loads(
            (FIXTURES / "definition163-admission-artifacts.json").read_text(
                encoding="utf-8"
            )
        )
        artifact = response["value"][0]
        artifact["name"] = f"GCValidationAdmission_{run_id}"
        if run["nativeScenario"] == "artifact-wrong-name":
            artifact["name"] = "GCValidationAdmission_9999"
        artifact["resource"]["url"] = (
            f"https://example.test/builds/{run_id}/artifacts"
        )
        artifact["resource"]["downloadUrl"] = (
            f"https://example.test/artifacts/{run_id}.zip"
        )
        return response["value"]

    def download_artifact(self, artifact: dict) -> bytes:
        self.artifact_download_calls.append(artifact["name"])
        run_id = int(artifact["name"].rsplit("_", 1)[1])
        run = next(run for run in self.runs if run["id"] == run_id)
        receipt = {
            "schemaVersion": 2,
            "definitionId": self.pipeline_id,
            "runKind": run_tag_value(run, "runKind"),
            "campaignId": run_tag_value(run, "campaignId"),
            "parentBuildId": run_tag_value(run, "parentBuildId"),
            "root": run_tag_value(run, "root"),
            "attempt": int(run_tag_value(run, "attempt")),
            "sourceRef": run["resources"]["repositories"]["self"]["refName"],
            "sourceVersion": run["resources"]["repositories"]["self"]["version"],
            "currentRunId": run_id,
            "matchingRunIds": [run_id],
            "activeRunIds": [run_id],
            "canonicalRunId": run_id,
            "status": "admitted",
            "admitted": True,
            "startedAt": "2026-09-24T19:44:00Z",
            "updatedAt": "2026-09-24T19:45:00Z",
            "finishedAt": "2026-09-24T19:45:00Z",
        }
        if run["nativeScenario"] == "artifact-mismatch":
            receipt["root"] = ROOTS[1]
        receipt["canonicalSha256"] = hashlib.sha256(
            canonical_json_bytes(receipt)
        ).hexdigest()
        if run["nativeScenario"] == "artifact-hash-mismatch":
            receipt["canonicalSha256"] = "0" * 64
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr(
                "gc-validation-admission.json",
                canonical_json_bytes(receipt) + b"\n",
            )
        return stream.getvalue()
def create_orchestrator(
    client: FakeClient,
    output_directory: Path,
    *,
    terminal_evidence_grace_seconds: int = 120,
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
        terminal_evidence_grace_seconds=terminal_evidence_grace_seconds,
        sleep=lambda _: None,
    )


def seed_run(
    client: FakeClient,
    orchestrator: GcValidationOrchestrator,
    *,
    scenario: str,
    run_id: int,
    state: str = "completed",
    result: str | None = "succeeded",
    build_result: str | None = None,
) -> dict:
    client.queue_run(orchestrator.build_request(ROOTS[0], 1))
    run = client.runs[-1]
    run["id"] = run_id
    run["state"] = state
    run["result"] = result
    run["buildResult"] = build_result or result
    run["nativeScenario"] = scenario
    return run


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

        class ErrorBody:
            def read(self) -> bytes:
                raise http.client.IncompleteRead(b"error", 1)

            def close(self) -> None:
                pass

        failures = (
            Response(http.client.IncompleteRead(b'{"id":', 1)),
            http.client.BadStatusLine("invalid status"),
            ssl.SSLError("TLS response ended early"),
            OSError("socket read failed"),
            Response(b"{"),
            Response(b"\xff"),
            urllib.error.HTTPError(
                "https://example.test/runs",
                500,
                "server error",
                {},
                ErrorBody(),
            ),
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

    def test_api_client_lists_and_downloads_exact_pipeline_artifact(self) -> None:
        client = AzureDevOpsClient(
            "https://dev.azure.com/example",
            "project",
            163,
            "runtime-token",
        )
        response = json.loads(
            (FIXTURES / "definition163-admission-artifacts.json").read_text(
                encoding="utf-8"
            )
        )
        requested = {}

        def request_json(method, url, body=None, timeout_seconds=60):
            requested["list"] = (method, url)
            return copy.deepcopy(response)

        def request_bytes(url, *, max_bytes, timeout_seconds=60):
            requested["download"] = (url, max_bytes)
            return b"artifact"

        client.request_json = request_json
        client.request_bytes = request_bytes
        artifacts = client.list_artifacts(1000)
        content = client.download_artifact(artifacts[0])

        self.assertEqual(
            (
                "GET",
                "https://dev.azure.com/example/project/_apis/build/builds/"
                "1000/artifacts?api-version=7.1",
            ),
            requested["list"],
        )
        self.assertEqual(
            (
                response["value"][0]["resource"]["downloadUrl"],
                1024 * 1024,
            ),
            requested["download"],
        )
        self.assertEqual(b"artifact", content)

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
                "gcValidationRunKind": RUN_KIND_PARENT_CHILD,
                "gcValidationParentBuildId": PARENT_BUILD_ID,
                "gcValidationAttempt": "1",
            },
            request["templateParameters"],
        )
        self.assertNotIn("variables", request)

    def test_direct_shard_identity_is_tagged_and_discoverable(self) -> None:
        output = io.StringIO()
        with redirect_stdout(output):
            emit_run_identity_tags(
                run_kind=RUN_KIND_DIRECT,
                campaign_id="phase1-direct-campaign",
                parent_build_id=RUN_KIND_DIRECT,
                root=ROOTS[0],
                attempt=1,
            )
        tags = [
            line.removeprefix("##vso[build.addbuildtag]")
            for line in output.getvalue().splitlines()
        ]
        run = {
            "id": 123,
            "tags": tags,
            "resources": {
                "repositories": {
                    "self": {
                        "refName": SOURCE_REF,
                        "version": SOURCE_VERSION,
                    }
                }
            },
            "templateParameters": {},
        }

        self.assertEqual(6, len(tags))
        self.assertTrue(
            run_matches(
                run,
                run_kind=RUN_KIND_DIRECT,
                campaign_id="phase1-direct-campaign",
                parent_build_id=RUN_KIND_DIRECT,
                root=ROOTS[0],
                source_ref=SOURCE_REF,
                source_version=SOURCE_VERSION,
                attempt=1,
            )
        )
        self.assertFalse(
            run_matches(
                run,
                run_kind=RUN_KIND_PARENT_CHILD,
                campaign_id="phase1-direct-campaign",
                parent_build_id=RUN_KIND_DIRECT,
                root=ROOTS[0],
                source_ref=SOURCE_REF,
                source_version=SOURCE_VERSION,
                attempt=1,
            )
        )

    def test_shard_identity_rejects_missing_or_forged_parameters(self) -> None:
        cases = (
            (
                {
                    "run_kind": RUN_KIND_DIRECT,
                    "campaign_id": "",
                    "parent_build_id": RUN_KIND_DIRECT,
                    "root": ROOTS[0],
                    "attempt": 1,
                },
                "campaign ID must be explicit",
            ),
            (
                {
                    "run_kind": RUN_KIND_DIRECT,
                    "campaign_id": CAMPAIGN_ID,
                    "parent_build_id": PARENT_BUILD_ID,
                    "root": ROOTS[0],
                    "attempt": 1,
                },
                "must use parent build ID 'direct'",
            ),
            (
                {
                    "run_kind": RUN_KIND_DIRECT,
                    "campaign_id": CAMPAIGN_ID,
                    "parent_build_id": RUN_KIND_DIRECT,
                    "root": ROOTS[0],
                    "attempt": 2,
                },
                "permit attempt 1 only",
            ),
            (
                {
                    "run_kind": RUN_KIND_PARENT_CHILD,
                    "campaign_id": CAMPAIGN_ID,
                    "parent_build_id": RUN_KIND_DIRECT,
                    "root": ROOTS[0],
                    "attempt": 1,
                },
                "positive numeric parent build ID",
            ),
        )
        for arguments, message in cases:
            with self.subTest(arguments=arguments):
                with self.assertRaisesRegex(OrchestrationError, message):
                    validate_shard_identity(**arguments)

        validate_shard_identity(
            run_kind=RUN_KIND_PARENT_CHILD,
            campaign_id=CAMPAIGN_ID,
            parent_build_id=PARENT_BUILD_ID,
            root=ROOTS[0],
            attempt=2,
            current_run_id=1000,
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
            "tags": list(
                identity_tags(
                    run_kind=RUN_KIND_PARENT_CHILD,
                    campaign_id=CAMPAIGN_ID,
                    parent_build_id=PARENT_BUILD_ID,
                    root=ROOTS[0],
                    attempt=1,
                )
            ),
        }
        self.assertTrue(
            run_matches(
                run,
                run_kind=RUN_KIND_PARENT_CHILD,
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
                run_kind=RUN_KIND_PARENT_CHILD,
                campaign_id=CAMPAIGN_ID,
                parent_build_id=PARENT_BUILD_ID,
                root=ROOTS[0],
                source_ref=SOURCE_REF,
                source_version=SOURCE_VERSION,
            )
        )
        run["id"] = 123
        run["tags"].append("gc-validation.parent.999")
        self.assertFalse(
            run_matches(
                run,
                run_kind=RUN_KIND_PARENT_CHILD,
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
                run_kind=RUN_KIND_PARENT_CHILD,
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
            detail["tags"] = list(
                identity_tags(
                    run_kind=RUN_KIND_PARENT_CHILD,
                    campaign_id=CAMPAIGN_ID,
                    parent_build_id=PARENT_BUILD_ID,
                    root=ROOTS[0],
                    attempt=1,
                )
            )
            detail["resources"] = copy.deepcopy(request["resources"])
            listing["value"].insert(
                0,
                {
                    "id": int(PARENT_BUILD_ID),
                    "state": "inProgress",
                    "templateParameters": {},
                },
            )
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
            set(detail["tags"]),
            set(matches[0]["tags"]),
        )
        self.assertEqual(
            SOURCE_VERSION,
            matches[0]["resources"]["repositories"]["self"]["version"],
        )

    def test_direct_run_hydrates_captured_list_and_get_shapes(self) -> None:
        listing = json.loads(
            (FIXTURES / "definition163-runs-list.json").read_text(encoding="utf-8")
        )
        detail = json.loads(
            (FIXTURES / "definition163-run-get.json").read_text(encoding="utf-8")
        )
        campaign_id = "phase1-direct-campaign"
        detail["tags"] = list(
            identity_tags(
                run_kind=RUN_KIND_DIRECT,
                campaign_id=campaign_id,
                parent_build_id=RUN_KIND_DIRECT,
                root=ROOTS[0],
                attempt=1,
            )
        )
        detail["resources"]["repositories"]["self"].update(
            {
                "refName": SOURCE_REF,
                "version": SOURCE_VERSION,
            }
        )
        client = FakeClient()
        client.list_runs = lambda: copy.deepcopy(listing["value"])
        client.get_run = lambda _: copy.deepcopy(detail)

        matches = hydrated_matching_runs(
            client,
            run_kind=RUN_KIND_DIRECT,
            campaign_id=campaign_id,
            parent_build_id=RUN_KIND_DIRECT,
            root=ROOTS[0],
            source_ref=SOURCE_REF,
            source_version=SOURCE_VERSION,
            attempt=1,
        )

        self.assertEqual([1607407], [run["id"] for run in matches])
        self.assertEqual({}, matches[0]["templateParameters"])
        self.assertNotIn("variables", matches[0])
        self.assertEqual(set(detail["tags"]), set(matches[0]["tags"]))

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

    def test_adoption_waits_for_source_identity_tags(self) -> None:
        class DelayedIdentityClient(FakeClient):
            def __init__(self):
                super().__init__(complete_on_get=False)
                self.identity_reads = 0

            def list_runs(self) -> list[dict]:
                runs = super().list_runs()
                if self.identity_reads == 0:
                    for run in runs:
                        run.pop("tags", None)
                return runs

            def get_run(self, run_id: int) -> dict:
                run = super().get_run(run_id)
                self.identity_reads += 1
                if self.identity_reads == 1:
                    run.pop("tags", None)
                return run

        with tempfile.TemporaryDirectory() as directory:
            client = DelayedIdentityClient()
            orchestrator = create_orchestrator(client, Path(directory))
            attempt = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertEqual(1, len(client.queue_calls))
        self.assertEqual(1000, attempt["runId"])
        self.assertGreaterEqual(client.identity_reads, 2)

    def test_nonpositive_or_invalid_post_id_is_adopted_without_requeue(self) -> None:
        for response in ([], {}, {"id": 0}, {"id": -1}, {"id": True}, {"id": "1"}):
            with self.subTest(response=response):
                with tempfile.TemporaryDirectory() as directory:
                    client = FakeClient()
                    queue_run = client.queue_run

                    def queue_without_positive_id(request):
                        queue_run(request)
                        return response

                    client.queue_run = queue_without_positive_id
                    orchestrator = create_orchestrator(client, Path(directory))
                    attempt = orchestrator.ensure_run(ROOTS[0], 1)

                self.assertEqual(1, len(client.queue_calls))
                self.assertTrue(attempt["adopted"])
                self.assertTrue(attempt["ambiguousQueueResponse"])
                self.assertEqual(1000, attempt["runId"])

    def test_duplicate_matching_attempt_uses_lowest_run_id(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient(complete_on_get=False)
            orchestrator = create_orchestrator(client, Path(directory))
            request = orchestrator.build_request(ROOTS[0], 1)
            for run_id in (458, 457):
                client.queue_run(request)
                client.runs[-1]["id"] = run_id
            client.queue_calls.clear()

            attempt = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertEqual([], client.queue_calls)
        self.assertEqual(457, attempt["runId"])
        self.assertEqual([458], attempt["duplicateRunIds"])

    def test_baseline_forgery_is_rejected_and_genuine_shard_is_queued(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient()
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="baseline-forgery",
                run_id=1000,
            )
            client.queue_calls.clear()

            attempt = orchestrator.ensure_run(ROOTS[0], 1)
            receipt = json.loads(
                orchestrator.receipt_path.read_text(encoding="utf-8")
            )

        self.assertEqual(1, len(client.queue_calls))
        self.assertEqual(1001, attempt["runId"])
        self.assertTrue(attempt["nativeEvidence"]["valid"])
        self.assertEqual(
            [1000],
            [
                candidate["runId"]
                for candidate in receipt["children"][ROOTS[0]][
                    "rejectedCandidates"
                ]
            ],
        )

    def test_active_baseline_forgery_is_rejected_without_duplicate_trust(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient(complete_on_get=False)
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="baseline-forgery",
                run_id=1000,
                state="inProgress",
                result=None,
            )
            client.queue_calls.clear()

            attempt = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertEqual(1, len(client.queue_calls))
        self.assertEqual(1001, attempt["runId"])
        self.assertEqual("admissionObserved", attempt["nativeEvidence"]["status"])

    def test_empty_active_timeline_is_provisionally_adopted(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient(complete_on_get=False)
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="timeline-pending",
                run_id=1000,
                state="inProgress",
                result=None,
            )
            client.queue_calls.clear()

            attempt = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertEqual([], client.queue_calls)
        self.assertEqual(1000, attempt["runId"])
        self.assertEqual("pending", attempt["nativeEvidence"]["status"])

    def test_null_active_timeline_is_provisionally_adopted(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient(complete_on_get=False)
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="timeline-null",
                run_id=1000,
                state="inProgress",
                result=None,
            )
            client.queue_calls.clear()

            attempt = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertEqual([], client.queue_calls)
        self.assertEqual(1000, attempt["runId"])
        self.assertEqual("pending", attempt["nativeEvidence"]["status"])

    def test_adopted_pending_forgery_is_rejected_when_graph_appears(
        self,
    ) -> None:
        client = FakeClient(complete_on_get=False)
        with tempfile.TemporaryDirectory() as directory:
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="timeline-pending",
                run_id=1000,
                state="inProgress",
                result=None,
            )
            client.queue_calls.clear()
            attempt = orchestrator.ensure_run(ROOTS[0], 1)
            client.runs[0]["nativeScenario"] = "baseline-forgery"

            run = orchestrator.refresh_attempt(ROOTS[0], attempt)
            evidence = attempt["nativeEvidence"]
            orchestrator.record_rejected_candidate(ROOTS[0], run, evidence)
            replacement = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertFalse(evidence["valid"])
        self.assertFalse(evidence["pending"])
        self.assertEqual(1, len(client.queue_calls))
        self.assertEqual(1001, replacement["runId"])

    def test_current_child_with_pending_timeline_fails_closed(self) -> None:
        client = FakeClient(complete_on_get=False)
        with tempfile.TemporaryDirectory() as directory:
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="timeline-pending",
                run_id=1000,
                state="inProgress",
                result=None,
            )
            admission = GcValidationAdmission(
                client,
                Path(directory) / "pending-admission",
                RUN_KIND_PARENT_CHILD,
                CAMPAIGN_ID,
                PARENT_BUILD_ID,
                ROOTS[0],
                1,
                SOURCE_REF,
                SOURCE_VERSION,
                1000,
                observation_seconds=0,
                poll_seconds=1,
            )

            with self.assertRaisesRegex(
                OrchestrationError,
                "did not provide valid active admission evidence",
            ):
                admission.run()

    def test_skipped_build_is_rejected_despite_succeeded_overall_result(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient()
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="skipped-build",
                run_id=1000,
                result="succeeded",
                build_result="skipped",
            )
            client.queue_calls.clear()

            attempt = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertEqual(1, len(client.queue_calls))
        self.assertEqual(1001, attempt["runId"])
        self.assertTrue(attempt["nativeEvidence"]["succeeded"])

    def test_forged_admission_artifact_is_rejected(self) -> None:
        for scenario in (
            "artifact-wrong-name",
            "artifact-mismatch",
            "artifact-hash-mismatch",
        ):
            with self.subTest(scenario=scenario):
                with tempfile.TemporaryDirectory() as directory:
                    client = FakeClient()
                    orchestrator = create_orchestrator(client, Path(directory))
                    seed_run(
                        client,
                        orchestrator,
                        scenario=scenario,
                        run_id=1000,
                    )
                    client.queue_calls.clear()

                    attempt = orchestrator.ensure_run(ROOTS[0], 1)

                self.assertEqual(1, len(client.queue_calls))
                self.assertEqual(1001, attempt["runId"])

    def test_incomplete_terminal_evidence_is_not_cached(self) -> None:
        for scenario in (
            "terminal-timeline-pending",
            "artifact-pending",
        ):
            with self.subTest(scenario=scenario):
                with tempfile.TemporaryDirectory() as directory:
                    client = FakeClient(complete_on_get=False)
                    orchestrator = create_orchestrator(client, Path(directory))
                    run = seed_run(
                        client,
                        orchestrator,
                        scenario=scenario,
                        run_id=1000,
                    )

                    first, _ = orchestrator.get_terminal_evidence(
                        ROOTS[0], run
                    )
                    run["nativeScenario"] = "genuine"
                    second, _ = orchestrator.get_terminal_evidence(
                        ROOTS[0], run
                    )

                self.assertTrue(first["pending"])
                self.assertFalse(first["valid"])
                self.assertTrue(second["valid"])
                self.assertEqual([1000, 1000], client.timeline_calls)

    def test_expired_incomplete_terminal_evidence_allows_replacement(
        self,
    ) -> None:
        for scenario in ("terminal-timeline-null", "artifact-pending"):
            with self.subTest(scenario=scenario):
                with tempfile.TemporaryDirectory() as directory:
                    client = FakeClient(complete_on_get=False)
                    orchestrator = create_orchestrator(
                        client,
                        Path(directory),
                        terminal_evidence_grace_seconds=0,
                    )
                    seed_run(
                        client,
                        orchestrator,
                        scenario=scenario,
                        run_id=1000,
                    )
                    client.queue_calls.clear()

                    attempt = orchestrator.ensure_run(ROOTS[0], 1)
                    receipt = json.loads(
                        orchestrator.receipt_path.read_text(encoding="utf-8")
                    )

                self.assertEqual(1, len(client.queue_calls))
                self.assertEqual(1001, attempt["runId"])
                rejected = receipt["children"][ROOTS[0]][
                    "rejectedCandidates"
                ]
                self.assertTrue(rejected[0]["nativeEvidence"]["pendingExpired"])

    def test_admission_ignores_completed_pending_candidate_after_deadline(
        self,
    ) -> None:
        client = FakeClient(complete_on_get=False)
        with tempfile.TemporaryDirectory() as directory:
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="terminal-timeline-null",
                run_id=1000,
            )
            seed_run(
                client,
                orchestrator,
                scenario="genuine",
                run_id=1001,
                state="inProgress",
                result=None,
            )
            admission = GcValidationAdmission(
                client,
                Path(directory) / "expired-terminal",
                RUN_KIND_PARENT_CHILD,
                CAMPAIGN_ID,
                PARENT_BUILD_ID,
                ROOTS[0],
                1,
                SOURCE_REF,
                SOURCE_VERSION,
                1001,
                observation_seconds=0,
                poll_seconds=1,
            )

            self.assertTrue(admission.run())
            receipt = json.loads(
                admission.receipt_path.read_text(encoding="utf-8")
            )

        self.assertEqual(1001, receipt["canonicalRunId"])
        self.assertTrue(
            receipt["rejectedCandidates"][0]["nativeEvidence"][
                "pendingExpired"
            ]
        )

    def test_valid_genuine_shard_uses_native_evidence_not_overall_result(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            client = FakeClient()
            orchestrator = create_orchestrator(client, Path(directory))
            seed_run(
                client,
                orchestrator,
                scenario="genuine",
                run_id=1000,
                result="failed",
                build_result="succeeded",
            )
            client.queue_calls.clear()

            attempt = orchestrator.ensure_run(ROOTS[0], 1)

        self.assertEqual([], client.queue_calls)
        self.assertEqual(1000, attempt["runId"])
        self.assertTrue(attempt["nativeEvidence"]["valid"])
        self.assertTrue(attempt["nativeEvidence"]["succeeded"])
        self.assertEqual("succeeded", attempt["nativeEvidence"]["buildResult"])

    def test_duplicate_canonical_selection_uses_only_native_candidates(
        self,
    ) -> None:
        scenarios = (
            ("baseline-forgery", "genuine", 1001, None),
            ("genuine", "genuine", 1000, [1001]),
        )
        for lower_scenario, higher_scenario, expected_id, duplicates in scenarios:
            with self.subTest(
                lower=lower_scenario,
                higher=higher_scenario,
            ):
                with tempfile.TemporaryDirectory() as directory:
                    client = FakeClient()
                    orchestrator = create_orchestrator(client, Path(directory))
                    seed_run(
                        client,
                        orchestrator,
                        scenario=lower_scenario,
                        run_id=1000,
                    )
                    seed_run(
                        client,
                        orchestrator,
                        scenario=higher_scenario,
                        run_id=1001,
                    )
                    client.queue_calls.clear()

                    attempt = orchestrator.ensure_run(ROOTS[0], 1)

                self.assertEqual([], client.queue_calls)
                self.assertEqual(expected_id, attempt["runId"])
                self.assertEqual(
                    duplicates,
                    attempt.get("duplicateRunIds"),
                )

    def test_two_concurrent_parents_adopt_same_canonical_run(self) -> None:
        class ConcurrentClient(FakeClient):
            def __init__(self):
                super().__init__(complete_on_get=False)
                self.initial_lists = 0
                self.lock = threading.Lock()
                self.list_barrier = threading.Barrier(2)

            def list_runs(self) -> list[dict]:
                with self.lock:
                    snapshot = super().list_runs()
                    synchronize = self.initial_lists < 2
                    self.initial_lists += 1
                if synchronize:
                    self.list_barrier.wait(timeout=5)
                return snapshot

            def queue_run(self, request: dict) -> dict:
                with self.lock:
                    return super().queue_run(request)

            def get_run(self, run_id: int) -> dict:
                with self.lock:
                    return super().get_run(run_id)

        client = ConcurrentClient()
        with (
            tempfile.TemporaryDirectory() as first_directory,
            tempfile.TemporaryDirectory() as second_directory,
        ):
            orchestrators = (
                create_orchestrator(client, Path(first_directory)),
                create_orchestrator(client, Path(second_directory)),
            )
            with ThreadPoolExecutor(max_workers=2) as executor:
                attempts = list(
                    executor.map(
                        lambda orchestrator: orchestrator.ensure_run(
                            ROOTS[0], 1
                        ),
                        orchestrators,
                    )
                )

        self.assertEqual(2, len(client.queue_calls))
        self.assertEqual([1000, 1000], [attempt["runId"] for attempt in attempts])
        self.assertEqual(
            [1000, 1001],
            sorted(attempt["returnedRunId"] for attempt in attempts),
        )
        self.assertEqual(1, sum(attempt["adopted"] for attempt in attempts))

    def test_direct_shard_admission_uses_direct_identity_contract(self) -> None:
        client = FakeClient(complete_on_get=False)
        campaign_id = "phase1-direct-campaign"
        with tempfile.TemporaryDirectory() as directory:
            request = create_orchestrator(
                client, Path(directory)
            ).build_request(ROOTS[0], 1)
            request["templateParameters"].update(
                {
                    "gcValidationCampaignId": campaign_id,
                    "gcValidationRunKind": RUN_KIND_DIRECT,
                    "gcValidationParentBuildId": RUN_KIND_DIRECT,
                }
            )
            client.queue_run(request)
            admission = GcValidationAdmission(
                client,
                Path(directory) / "direct",
                RUN_KIND_DIRECT,
                campaign_id,
                RUN_KIND_DIRECT,
                ROOTS[0],
                1,
                SOURCE_REF,
                SOURCE_VERSION,
                1000,
                observation_seconds=0,
                poll_seconds=1,
            )

            self.assertTrue(admission.run())
            receipt = json.loads(
                admission.receipt_path.read_text(encoding="utf-8")
            )

        self.assertEqual(2, receipt["schemaVersion"])
        self.assertEqual(RUN_KIND_DIRECT, receipt["runKind"])
        self.assertEqual(RUN_KIND_DIRECT, receipt["parentBuildId"])
        self.assertTrue(receipt["admitted"])

    def test_two_duplicate_children_admit_only_lowest_run_id(self) -> None:
        class SynchronizedAdmissionClient(FakeClient):
            def __init__(self):
                super().__init__(complete_on_get=False)
                self.initial_lists = 0
                self.lock = threading.Lock()
                self.list_barrier = threading.Barrier(2)

            def list_runs(self) -> list[dict]:
                snapshot = super().list_runs()
                with self.lock:
                    synchronize = self.initial_lists < 2
                    self.initial_lists += 1
                if synchronize:
                    self.list_barrier.wait(timeout=5)
                return snapshot

        client = SynchronizedAdmissionClient()
        with tempfile.TemporaryDirectory() as directory:
            request = create_orchestrator(
                client, Path(directory)
            ).build_request(ROOTS[0], 1)
            for run_id in (1000, 1001):
                client.queue_run(request)
                client.runs[-1]["id"] = run_id
            client.queue_calls.clear()
            admissions = {
                run_id: GcValidationAdmission(
                    client,
                    Path(directory) / str(run_id),
                    RUN_KIND_PARENT_CHILD,
                    CAMPAIGN_ID,
                    PARENT_BUILD_ID,
                    ROOTS[0],
                    1,
                    SOURCE_REF,
                    SOURCE_VERSION,
                    run_id,
                    observation_seconds=0,
                    poll_seconds=1,
                )
                for run_id in (1000, 1001)
            }
            with ThreadPoolExecutor(max_workers=2) as executor:
                results = dict(
                    zip(
                        admissions,
                        executor.map(
                            lambda admission: admission.run(),
                            admissions.values(),
                        ),
                    )
                )

        self.assertEqual({1000: True, 1001: False}, results)

    def test_higher_run_waits_for_delayed_canonical_visibility(self) -> None:
        class DelayedCanonicalAdmissionClient(FakeClient):
            def __init__(self):
                super().__init__(complete_on_get=False)
                self.list_count = 0

            def list_runs(self) -> list[dict]:
                self.list_count += 1
                if self.list_count == 1:
                    return [self.runs[1]]
                return super().list_runs()

        client = DelayedCanonicalAdmissionClient()
        with tempfile.TemporaryDirectory() as directory:
            request = create_orchestrator(
                client, Path(directory)
            ).build_request(ROOTS[0], 1)
            for run_id in (1000, 1001):
                client.queue_run(request)
                client.runs[-1]["id"] = run_id
            monotonic_values = iter((0.0, 1.0))
            admission = GcValidationAdmission(
                client,
                Path(directory) / "delayed",
                RUN_KIND_PARENT_CHILD,
                CAMPAIGN_ID,
                PARENT_BUILD_ID,
                ROOTS[0],
                1,
                SOURCE_REF,
                SOURCE_VERSION,
                1001,
                observation_seconds=10,
                poll_seconds=0,
                sleep=lambda _: None,
                monotonic=lambda: next(monotonic_values),
            )

            self.assertFalse(admission.run())
            receipt = json.loads(
                admission.receipt_path.read_text(encoding="utf-8")
            )

        self.assertEqual(2, client.list_count)
        self.assertEqual(1000, receipt["canonicalRunId"])
        self.assertEqual([1000, 1001], receipt["activeRunIds"])

    def test_completed_canonical_still_rejects_later_duplicate(self) -> None:
        client = FakeClient(complete_on_get=False)
        with tempfile.TemporaryDirectory() as directory:
            request = create_orchestrator(
                client, Path(directory)
            ).build_request(ROOTS[0], 1)
            for run_id in (1000, 1001):
                client.queue_run(request)
                client.runs[-1]["id"] = run_id
            client.runs[0]["state"] = "completed"
            client.runs[0]["result"] = "succeeded"
            admission = GcValidationAdmission(
                client,
                Path(directory) / "completed-canonical",
                RUN_KIND_PARENT_CHILD,
                CAMPAIGN_ID,
                PARENT_BUILD_ID,
                ROOTS[0],
                1,
                SOURCE_REF,
                SOURCE_VERSION,
                1001,
                observation_seconds=0,
                poll_seconds=1,
            )

            self.assertFalse(admission.run())
            receipt = json.loads(
                admission.receipt_path.read_text(encoding="utf-8")
            )

        self.assertEqual(1000, receipt["canonicalRunId"])
        self.assertEqual([1000, 1001], receipt["matchingRunIds"])
        self.assertEqual([1001], receipt["activeRunIds"])

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
            ("inProgress", None, "is not terminal", [1000, 1001]),
            (
                "completed",
                "succeeded",
                "did not finish unsuccessfully",
                [1000, 1001, 1000],
            ),
        )
        for state, result, expected, timeline_calls in scenarios:
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
                self.assertEqual(timeline_calls, client.timeline_calls)

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
        self.assertEqual([1000, 1001, 1000], client.timeline_calls)

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
