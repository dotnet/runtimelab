#!/usr/bin/env python3

import argparse
import copy
from datetime import datetime, timezone
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
import ssl
import time
from typing import Callable
import urllib.error
import urllib.parse
import urllib.request


BUILD_SECURITY_NAMESPACE_ID = "33344d9c-fc72-4d6f-aba5-fa317101a7e9"
CONSUMER_DEFINITION_IDS = (702, 1012, 306, 1208, 1209, 1505)
IMPLEMENTED_DEFINITION_IDS = (702, 1012, 306)
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")
TRANSIENT_INFRASTRUCTURE_PATTERNS = (
    re.compile(r"\bwe stopped hearing from agent\b", re.IGNORECASE),
    re.compile(r"\bagent\b.*\blost communication\b", re.IGNORECASE),
    re.compile(r"\bagent\b.*\bdisconnected\b", re.IGNORECASE),
    re.compile(
        r"\bjob has been abandoned because agent\b.*\bdid not renew the lock\b",
        re.IGNORECASE,
    ),
)
RUN_VARIABLES = {
    "mode": "STANDARD_GC_PERFORMANCE_MODE",
    "campaign": "STANDARD_GC_CAMPAIGN_ID",
    "cohort": "STANDARD_GC_COHORT_ID",
    "manifest": "STANDARD_GC_COHORT_MANIFEST_SHA256",
    "request": "STANDARD_GC_REQUEST_SHA256",
    "attempt": "STANDARD_GC_ATTEMPT",
    "controller": "STANDARD_GC_CONTROLLER_BUILD_ID",
}


class CoordinationError(RuntimeError):
    pass


class PermissionPreflightError(CoordinationError):
    pass


class AmbiguousQueueResponse(CoordinationError):
    pass


class ApiRequestError(CoordinationError):
    def __init__(self, method: str, url: str, status: int | None, detail: str):
        super().__init__(f"{method} {url} failed ({status or 'no status'}): {detail}")
        self.method = method
        self.url = url
        self.status = status


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def canonical_json_bytes(value: object) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def atomic_write_json(path: Path, value: object) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    content = canonical_json_bytes(value) + b"\n"
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_bytes(content)
    temporary.replace(path)
    return sha256_bytes(content)


def variable_value(run: dict, name: str) -> str:
    variables = {
        str(key).casefold(): value for key, value in (run.get("variables") or {}).items()
    }
    value = variables.get(name.casefold(), "")
    if isinstance(value, dict):
        value = value.get("value", "")
    return str(value)


def repository_resources(run: dict) -> dict:
    return (run.get("resources") or {}).get("repositories") or {}


def collect_timeline_errors(timeline: dict) -> list[str]:
    return [
        issue["message"]
        for record in timeline.get("records", [])
        for issue in record.get("issues", [])
        if issue.get("type", "").lower() == "error" and issue.get("message")
    ]


def classify_transient_infrastructure_failure(timeline: dict) -> dict:
    messages = collect_timeline_errors(timeline)
    matched = [
        message
        for message in messages
        if any(pattern.search(message) for pattern in TRANSIENT_INFRASTRUCTURE_PATTERNS)
    ]
    return {
        "isTransientInfrastructureFailure": bool(matched)
        and len(matched) == len(messages),
        "errorMessages": messages,
        "matchedInfrastructureMessages": matched,
    }


def _positive_run_id(value: object) -> int | None:
    if not isinstance(value, dict):
        return None
    run_id = value.get("id")
    if isinstance(run_id, bool) or not isinstance(run_id, int) or run_id <= 0:
        return None
    return run_id


class AzureDevOpsClient:
    def __init__(
        self,
        collection_uri: str,
        project_id: str,
        access_token: str,
    ):
        self.collection_uri = collection_uri.rstrip("/") + "/"
        self.project_id = project_id
        self.access_token = access_token

    def request_json(
        self,
        method: str,
        url: str,
        body: dict | None = None,
        timeout_seconds: int = 60,
    ) -> dict:
        request = urllib.request.Request(
            url,
            data=canonical_json_bytes(body) if body is not None else None,
            method=method,
            headers={
                "Authorization": f"Bearer {self.access_token}",
                "Accept": "application/json",
                "Content-Type": "application/json; charset=utf-8",
            },
        )
        try:
            with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
                content = response.read()
                return json.loads(content) if content else {}
        except urllib.error.HTTPError as error:
            try:
                detail = error.read().decode("utf-8", errors="replace")
            except (http.client.HTTPException, ssl.SSLError, OSError) as body_error:
                if method == "POST":
                    raise AmbiguousQueueResponse(str(body_error)) from body_error
                raise ApiRequestError(method, url, error.code, str(body_error)) from body_error
            if method == "POST" and (error.code in (408, 429) or error.code >= 500):
                raise AmbiguousQueueResponse(detail) from error
            raise ApiRequestError(method, url, error.code, detail) from error
        except (
            urllib.error.URLError,
            TimeoutError,
            ConnectionError,
            http.client.HTTPException,
            ssl.SSLError,
            OSError,
            json.JSONDecodeError,
            UnicodeDecodeError,
        ) as error:
            if method == "POST":
                raise AmbiguousQueueResponse(str(error)) from error
            raise ApiRequestError(method, url, None, str(error)) from error

    def connection_data(self) -> dict:
        return self.request_json(
            "GET",
            self.collection_uri
            + "_apis/connectionData?connectOptions=1&lastChangeId=-1&lastChangeId64=-1",
        )

    def namespace(self) -> dict:
        return self.request_json(
            "GET",
            self.collection_uri
            + f"_apis/securitynamespaces/{BUILD_SECURITY_NAMESPACE_ID}?api-version=7.1",
        )

    def queue_permission(self, definition_id: int, queue_bit: int) -> dict:
        token = f"{self.project_id}/{definition_id}"
        query = urllib.parse.urlencode(
            {
                "tokens": token,
                "alwaysAllowAdministrators": "false",
                "api-version": "7.1",
            }
        )
        response = self.request_json(
            "GET",
            self.collection_uri
            + f"_apis/permissions/{BUILD_SECURITY_NAMESPACE_ID}/{queue_bit}?{query}",
        )
        values = response.get("value", [])
        if len(values) != 1 or not isinstance(values[0], bool):
            raise PermissionPreflightError(
                f"definition {definition_id} permission response was not one boolean"
            )
        return {"definitionId": definition_id, "token": token, "allowed": values[0]}

    def runs_url(self, definition_id: int) -> str:
        return (
            f"{self.collection_uri}{self.project_id}/_apis/pipelines/"
            f"{definition_id}/runs?api-version=7.1"
        )

    def list_runs(self, definition_id: int) -> list[dict]:
        response = self.request_json("GET", self.runs_url(definition_id))
        return response if isinstance(response, list) else response.get("value", [])

    def get_run(self, definition_id: int, run_id: int) -> dict:
        return self.request_json(
            "GET",
            f"{self.collection_uri}{self.project_id}/_apis/pipelines/"
            f"{definition_id}/runs/{run_id}?api-version=7.1",
        )

    def queue_run(self, definition_id: int, request: dict) -> dict:
        return self.request_json("POST", self.runs_url(definition_id), request)

    def timeline(self, run_id: int) -> dict:
        return self.request_json(
            "GET",
            f"{self.collection_uri}{self.project_id}/_apis/build/builds/"
            f"{run_id}/timeline?api-version=7.1",
        )

    def artifacts(self, run_id: int) -> list[dict]:
        response = self.request_json(
            "GET",
            f"{self.collection_uri}{self.project_id}/_apis/build/builds/"
            f"{run_id}/artifacts?api-version=7.1",
        )
        return response.get("value", [])


def validate_identity_posture(
    client: AzureDevOpsClient,
    posture_path: Path,
    contract: dict,
) -> dict:
    posture = json.loads(posture_path.read_text(encoding="utf-8"))
    if file_sha256(posture_path) not in contract["permissionDecision"][
        "approvedPostureFileSha256"
    ]:
        raise PermissionPreflightError(
            "identity posture file hash is not frozen as owner-approved"
        )
    required = {
        "schemaVersion",
        "ownerDecisionCanonicalSha256",
        "decision",
        "approvedBy",
        "approvedAtUtc",
        "expectedPrincipalDescriptor",
        "expectedPrincipalDisplayName",
    }
    if set(posture) != required or posture["schemaVersion"] != 1:
        raise PermissionPreflightError("identity posture input has an unknown schema")
    permission = contract["permissionDecision"]
    if (
        posture["ownerDecisionCanonicalSha256"]
        != permission["ownerDecisionCanonicalSha256"]
        or posture["decision"] not in permission["approvedChoices"]
        or not posture["approvedBy"].strip()
        or not posture["approvedAtUtc"].strip()
    ):
        raise PermissionPreflightError("identity posture does not match an approved owner decision")

    connection = client.connection_data()
    authenticated = connection.get("authenticatedUser") or {}
    descriptor = authenticated.get("descriptor", "")
    display_name = authenticated.get("providerDisplayName") or authenticated.get(
        "displayName", ""
    )
    if (
        descriptor != posture["expectedPrincipalDescriptor"]
        or display_name != posture["expectedPrincipalDisplayName"]
    ):
        raise PermissionPreflightError("live Azure identity does not match approved posture")

    namespaces = client.namespace().get("value", [])
    if len(namespaces) != 1:
        raise PermissionPreflightError("Build security namespace was not returned")
    actions = {item["name"]: int(item["bit"]) for item in namespaces[0]["actions"]}
    queue_bit = actions.get("QueueBuilds")
    if queue_bit is None:
        raise PermissionPreflightError("Build security namespace has no QueueBuilds action")
    permissions = [
        client.queue_permission(definition_id, queue_bit)
        for definition_id in CONSUMER_DEFINITION_IDS
    ]
    if not all(item["allowed"] for item in permissions):
        raise PermissionPreflightError("approved live identity cannot queue every consumer")
    return {
        "decision": posture["decision"],
        "descriptor": descriptor,
        "displayName": display_name,
        "queueBuildsBit": queue_bit,
        "definitions": permissions,
    }


def validate_manifest(manifest_path: Path, requirements_path: Path, contract: dict) -> dict:
    if file_sha256(requirements_path) != contract["producerContract"][
        "requirementsFileSha256"
    ]:
        raise CoordinationError("producer requirements hash does not match the frozen contract")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest["contract"]["coverageRowsSha256"] != contract["coverageRowsSha256"]:
        raise CoordinationError("producer manifest coverage identity does not match")
    identity = manifest.get("identity") or {}
    if (
        identity.get("producerDefinitionId") != 895
        or identity.get("sourceRepository") != "dotnet/runtimelab"
        or identity.get("sourceBranch") != "refs/heads/feature/gc/baseline"
        or not SHA_PATTERN.fullmatch(identity.get("sourceCommit", ""))
    ):
        raise CoordinationError("producer manifest source identity is invalid")
    if manifest.get("errors"):
        raise CoordinationError("producer manifest contains artifact errors")

    requirements = json.loads(requirements_path.read_text(encoding="utf-8"))
    expected_rows = {
        row["rowId"]: row
        for row in requirements["downstreamRowMappings"]
        if row["definitionId"] in CONSUMER_DEFINITION_IDS
    }
    rows = {row["rowId"]: row for row in manifest.get("rows", [])}
    missing = sorted(set(expected_rows) - set(rows))
    if missing:
        raise CoordinationError(f"producer manifest is missing {len(missing)} performance rows")
    blocked = sorted(
        row_id for row_id in expected_rows if rows[row_id].get("status") != "ready"
    )
    if blocked:
        raise CoordinationError(f"producer manifest has {len(blocked)} blocked performance rows")
    return {"manifest": manifest, "requirements": requirements, "rows": rows}


def _artifact_map_702_1012(
    definition_id: int,
    manifest: dict,
    requirements: dict,
) -> dict:
    entries = []
    evidence = manifest["artifacts"]
    for mapping in requirements["downstreamRowMappings"]:
        if mapping["definitionId"] != definition_id:
            continue
        requirement_id = mapping["requirementIds"][0]
        artifact = evidence[requirement_id]
        identity = mapping["consumerArtifactIdentity"]
        entries.append(
            {
                "rowId": mapping["rowId"],
                "definitionId": definition_id,
                "platform": identity["platform"],
                "controllerPlatform": identity["controllerPlatform"],
                "rid": mapping["rid"],
                "flavor": identity["runtimeFlavor"],
                "variant": identity["variant"],
                "configuration": mapping["configuration"]["build"],
                "runKind": mapping["configuration"]["runKind"],
                "artifact": {
                    "kind": "azurePipelineArtifactArchive",
                    "organization": "dnceng",
                    "project": "internal",
                    "producerDefinitionId": 895,
                    "producerBuildId": manifest["identity"]["producerBuildId"],
                    "name": artifact["artifactName"],
                    "fileName": artifact["fileName"],
                    "path": None,
                },
                "hash": {
                    "algorithm": "sha256",
                    "value": artifact["sha256"],
                    "required": True,
                },
                "sourceCommit": manifest["identity"]["sourceCommit"],
                "availability": {"status": "validated", "queueEligible": True},
            }
        )
    return {"schemaVersion": 1, "entries": entries}


def _artifact_map_306(
    manifest: dict,
    requirements: dict,
    validation_receipt_path: Path,
) -> dict:
    receipt = json.loads(validation_receipt_path.read_text(encoding="utf-8"))
    identity = manifest["identity"]
    for name, expected in (
        ("definitionId", 306),
        ("coverageRowsSha256", manifest["contract"]["coverageRowsSha256"]),
        ("sourceCommit", identity["sourceCommit"]),
        ("producerDefinitionId", 895),
        ("producerBuildId", identity["producerBuildId"]),
        ("campaignId", identity["campaignId"]),
        ("cohortId", identity["cohortId"]),
        ("cohortManifestSha256", file_sha256(validation_receipt_path.parent.parent / "standard-gc-cohort-manifest.json")),
    ):
        if receipt.get(name) != expected:
            raise CoordinationError(f"definition 306 validation receipt mismatch: {name}")
    proofs = {row["rowId"]: row for row in receipt["rows"]}
    evidence = manifest["artifacts"]
    entries = []
    for mapping in requirements["downstreamRowMappings"]:
        if mapping["definitionId"] != 306:
            continue
        proof = proofs.get(mapping["rowId"])
        if not proof or proof.get("status") != "passed" or not SHA256_PATTERN.fullmatch(
            proof.get("compatibilityProofSha256", "")
        ):
            raise CoordinationError(f"definition 306 row proof is invalid: {mapping['rowId']}")
        requirement_id = next(
            item
            for item in mapping["requirementIds"]
            if item != "validation-receipt:definition306"
        )
        artifact = evidence[requirement_id]
        configuration = mapping["configuration"]
        if artifact["kind"] == "nugetRuntimePack":
            artifact_value = {
                "kind": "nugetRuntimePack",
                "feed": "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json",
                "packageId": artifact["package"]["id"],
                "version": artifact["package"]["version"],
                "sha512": artifact["package"]["sha512"],
                "producerDefinitionId": 895,
                "producerBuildId": identity["producerBuildId"],
            }
        else:
            artifact_value = {
                "kind": "azurePipelineArtifactArchive",
                "organization": "dnceng",
                "project": "internal",
                "producerDefinitionId": 895,
                "producerBuildId": identity["producerBuildId"],
                "name": artifact["artifactName"],
                "fileName": artifact["fileName"],
                "path": None,
                "sha256": artifact["sha256"],
            }
        entries.append(
            {
                "definitionId": 306,
                "classId": (
                    "bdn-micro-corerun"
                    if mapping["consumptionMode"] == "native-v4-runtime-package"
                    else "desktop-fdd-scd-startup"
                ),
                "channel": configuration["channel"],
                "osGroup": "ubuntu" if mapping["rid"].startswith("linux") else "windows",
                "architecture": mapping["rid"].rsplit("-", 1)[-1],
                "rid": mapping["rid"],
                "queue": _definition_306_queue(mapping),
                "rowId": mapping["rowId"],
                "sourceCommit": identity["sourceCommit"],
                "compatibilityProofSha256": proof["compatibilityProofSha256"],
                "artifact": artifact_value,
                "availability": {"queueEligible": True, "status": "validated"},
            }
        )
    return {
        "schemaVersion": 1,
        "validationArtifact": {
            "kind": "azurePipelineArtifactFile",
            "organization": "dnceng",
            "project": "internal",
            "producerDefinitionId": 895,
            "producerBuildId": identity["producerBuildId"],
            "name": "ExternalRuntime306Validation",
            "fileName": validation_receipt_path.name,
            "path": None,
            "sha256": file_sha256(validation_receipt_path),
        },
        "entries": entries,
    }


def _definition_306_queue(mapping: dict) -> str:
    rid = mapping["rid"]
    if mapping["consumptionMode"] == "native-v4-runtime-package":
        return {
            "linux-x64": "Ubuntu.2204.Amd64.Open",
            "win-x64": "Windows.11.Amd64.Client.Open",
            "win-x86": "Windows.11.Amd64.Client.Open",
        }[rid]
    return {
        "win-x64": "Windows.11.Amd64.Viper.Perf",
        "win-arm64": "Windows.Server.Arm64.Perf",
    }[rid]


def build_request(
    definition_id: int,
    contract: dict,
    manifest: dict,
    artifact_map: dict,
    manifest_sha256: str,
    controller_build_id: str,
    attempt: int,
) -> dict:
    consumer = contract["consumers"][str(definition_id)]
    identity = manifest["identity"]
    artifact_map_json = canonical_json_bytes(artifact_map).decode("utf-8")
    logical_key = sha256_bytes(
        canonical_json_bytes(
            {
                "definitionId": definition_id,
                "contractFileSha256": consumer["contractFileSha256"],
                "resources": consumer["repositories"],
                "campaignId": identity["campaignId"],
                "cohortId": identity["cohortId"],
                "manifestSha256": manifest_sha256,
                "artifactMapSha256": sha256_bytes(canonical_json_bytes(artifact_map)),
            }
        )
    )
    parameters = dict(consumer["existingParameters"])
    parameters.update(
        {
            "externalRuntimeMode": True,
            "externalRuntimeContractVersion": "1.0.0",
            "externalRuntimeCoverageRowsSha256": contract["coverageRowsSha256"],
            "externalRuntimeProducerDefinitionId": 895,
            "externalRuntimeProducerBuildId": identity["producerBuildId"],
            "externalRuntimeProducerBuildNumber": str(
                identity.get("producerBuildNumber", identity["producerBuildId"])
            ),
            "externalRuntimeSourceRepository": identity["sourceRepository"],
            "externalRuntimeSourceBranch": identity["sourceBranch"],
            "externalRuntimeSourceCommit": identity["sourceCommit"],
            "externalRuntimeCampaignId": identity["campaignId"],
            "externalRuntimeCohortId": identity["cohortId"],
            "externalRuntimeCohortManifestSha256": manifest_sha256,
            "externalRuntimeArtifactMapJson": artifact_map_json,
            "externalRuntimeSkipPerfLabUpload": True,
            "externalRuntimeIdempotencyKey": logical_key,
            "externalRuntimeAttempt": attempt,
        }
    )
    request = {
        "resources": {"repositories": consumer["repositories"]},
        "variables": {
            RUN_VARIABLES["mode"]: {"value": "external-runtime", "isSecret": False},
            RUN_VARIABLES["campaign"]: {
                "value": identity["campaignId"],
                "isSecret": False,
            },
            RUN_VARIABLES["cohort"]: {"value": identity["cohortId"], "isSecret": False},
            RUN_VARIABLES["manifest"]: {"value": manifest_sha256, "isSecret": False},
            RUN_VARIABLES["attempt"]: {"value": str(attempt), "isSecret": False},
            RUN_VARIABLES["controller"]: {
                "value": controller_build_id,
                "isSecret": False,
            },
        },
        "templateParameters": parameters,
    }
    request_sha256 = sha256_bytes(canonical_json_bytes(request))
    request["variables"][RUN_VARIABLES["request"]] = {
        "value": request_sha256,
        "isSecret": False,
    }
    return request


def run_matches(run: dict, request: dict) -> bool:
    repositories = repository_resources(run)
    for name, expected in request["resources"]["repositories"].items():
        actual = repositories.get(name) or {}
        if (
            actual.get("refName") != expected["refName"]
            or actual.get("version") != expected["version"]
        ):
            return False
    return all(
        variable_value(run, name) == str(value["value"])
        for name, value in request["variables"].items()
    )


class PerformanceCoordinator:
    def __init__(
        self,
        client: AzureDevOpsClient,
        output_directory: Path,
        requests: dict[int, dict],
        queue_enabled: bool,
        permission_preflight: dict | None,
        monitor_timeout_seconds: int = 36000,
        poll_seconds: int = 30,
        adoption_timeout_seconds: int = 120,
        sleep: Callable[[float], None] = time.sleep,
        monotonic: Callable[[], float] = time.monotonic,
    ):
        self.client = client
        self.output_directory = output_directory
        self.requests = requests
        self.queue_enabled = queue_enabled
        self.monitor_timeout_seconds = monitor_timeout_seconds
        self.poll_seconds = poll_seconds
        self.adoption_timeout_seconds = adoption_timeout_seconds
        self.sleep = sleep
        self.monotonic = monotonic
        self.receipt_path = output_directory / "standard-gc-performance-coordination.json"
        self.receipt = {
            "schemaVersion": 1,
            "startedAtUtc": utc_now(),
            "queueEnabled": queue_enabled,
            "permissionPreflight": permission_preflight,
            "requests": {},
            "runs": {},
            "status": "materializing",
            "succeeded": False,
        }

    def write_receipt(self) -> None:
        canonical = dict(self.receipt)
        canonical.pop("canonicalSha256", None)
        self.receipt["updatedAtUtc"] = utc_now()
        self.receipt["canonicalSha256"] = sha256_bytes(canonical_json_bytes(canonical))
        atomic_write_json(self.receipt_path, self.receipt)

    def materialize(self) -> None:
        self.output_directory.mkdir(parents=True, exist_ok=True)
        for definition_id, request in self.requests.items():
            path = self.output_directory / f"definition-{definition_id}-request.json"
            request_sha256 = atomic_write_json(path, request)
            self.receipt["requests"][str(definition_id)] = {
                "file": path.name,
                "sha256": request_sha256,
                "canonicalRequestSha256": sha256_bytes(canonical_json_bytes(request)),
            }
        self.receipt["status"] = "nonqueueable" if not self.queue_enabled else "queueing"
        self.write_receipt()

    def request_for_attempt(self, request: dict, attempt: int) -> dict:
        updated = copy.deepcopy(request)
        updated["templateParameters"]["externalRuntimeAttempt"] = attempt
        updated["variables"][RUN_VARIABLES["attempt"]]["value"] = str(attempt)
        updated["variables"].pop(RUN_VARIABLES["request"], None)
        request_sha256 = sha256_bytes(canonical_json_bytes(updated))
        updated["variables"][RUN_VARIABLES["request"]] = {
            "value": request_sha256,
            "isSecret": False,
        }
        return updated

    def matching_runs(self, definition_id: int, request: dict) -> list[dict]:
        matches = []
        for summary in self.client.list_runs(definition_id):
            try:
                run_id = int(summary.get("id", 0))
            except (TypeError, ValueError):
                continue
            if run_id <= 0:
                continue
            detail = self.client.get_run(definition_id, run_id)
            run = dict(summary)
            run.update(detail)
            if run_matches(run, request):
                matches.append(run)
        return sorted(matches, key=lambda run: int(run["id"]))

    def adopt_after_post(self, definition_id: int, request: dict) -> dict:
        deadline = self.monotonic() + self.adoption_timeout_seconds
        while True:
            matches = self.matching_runs(definition_id, request)
            if matches:
                return next(
                    (run for run in matches if run.get("state") != "completed"),
                    matches[0],
                )
            if self.monotonic() >= deadline:
                raise AmbiguousQueueResponse(
                    f"definition {definition_id} POST is ambiguous; adoption only"
                )
            self.sleep(min(self.poll_seconds, 5))

    def ensure_run(self, definition_id: int, request: dict) -> dict:
        matches = self.matching_runs(definition_id, request)
        active = [run for run in matches if run.get("state") != "completed"]
        if len(active) > 1:
            raise CoordinationError(
                f"definition {definition_id} has multiple active matching controllers"
            )
        if active:
            return {"run": active[0], "adopted": True, "ambiguous": False}
        if matches:
            return {"run": matches[0], "adopted": True, "ambiguous": False}

        ambiguous = False
        returned_id = None
        try:
            response = self.client.queue_run(definition_id, request)
            returned_id = _positive_run_id(response)
            ambiguous = returned_id is None
        except AmbiguousQueueResponse:
            ambiguous = True
        run = self.adopt_after_post(definition_id, request)
        return {
            "run": run,
            "adopted": ambiguous or returned_id != int(run["id"]),
            "ambiguous": ambiguous,
            "returnedRunId": returned_id,
        }

    def run(self) -> bool:
        self.materialize()
        if not self.queue_enabled:
            return False
        for definition_id, request in self.requests.items():
            selected = self.ensure_run(definition_id, request)
            run = selected["run"]
            run_id = int(run["id"])
            self.receipt["runs"][str(definition_id)] = {
                "runId": run_id,
                "adopted": selected["adopted"],
                "ambiguousQueueResponse": selected["ambiguous"],
                "returnedRunId": selected.get("returnedRunId"),
                "state": run.get("state"),
                "result": run.get("result"),
            }
            self.write_receipt()

        self.receipt["status"] = "monitoring"
        self.write_receipt()
        deadline = self.monotonic() + self.monitor_timeout_seconds
        while True:
            unfinished = []
            for definition_id in self.requests:
                request = self.requests[definition_id]
                item = self.receipt["runs"][str(definition_id)]
                run = self.client.get_run(definition_id, item["runId"])
                item["state"] = run.get("state")
                item["result"] = run.get("result")
                if run.get("state") != "completed":
                    unfinished.append(definition_id)
                    continue
                timeline = self.client.timeline(item["runId"])
                artifacts = self.client.artifacts(item["runId"])
                item["timelineRecordIds"] = [
                    record.get("id") for record in timeline.get("records", [])
                ]
                item["jobIds"] = [
                    record.get("id")
                    for record in timeline.get("records", [])
                    if record.get("type") == "Job"
                ]
                item["artifacts"] = [
                    {"name": artifact.get("name"), "resource": artifact.get("resource")}
                    for artifact in artifacts
                ]
                native_receipts = [
                    artifact
                    for artifact in artifacts
                    if str(artifact.get("name", "")).startswith("ExternalRuntimeCompletion")
                ]
                item["nativeCompletionReceiptCount"] = len(native_receipts)
                item["terminalReceiptPresent"] = len(native_receipts) == 1
                if run.get("result") != "succeeded":
                    item["failureClassification"] = (
                        classify_transient_infrastructure_failure(timeline)
                    )
                    if (
                        request["templateParameters"]["externalRuntimeAttempt"] == 1
                        and item["failureClassification"][
                            "isTransientInfrastructureFailure"
                        ]
                    ):
                        retry_request = self.request_for_attempt(request, 2)
                        retry_path = (
                            self.output_directory
                            / f"definition-{definition_id}-attempt-2-request.json"
                        )
                        retry_sha256 = atomic_write_json(retry_path, retry_request)
                        self.receipt["requests"][str(definition_id)][
                            "retryFile"
                        ] = retry_path.name
                        self.receipt["requests"][str(definition_id)][
                            "retrySha256"
                        ] = retry_sha256
                        selected = self.ensure_run(definition_id, retry_request)
                        retry_run = selected["run"]
                        item.setdefault("attempts", []).append(
                            {
                                "attempt": 1,
                                "runId": item["runId"],
                                "result": item["result"],
                                "failureClassification": item[
                                    "failureClassification"
                                ],
                            }
                        )
                        item["runId"] = int(retry_run["id"])
                        item["adopted"] = selected["adopted"]
                        item["ambiguousQueueResponse"] = selected["ambiguous"]
                        item["returnedRunId"] = selected.get("returnedRunId")
                        item["state"] = retry_run.get("state")
                        item["result"] = retry_run.get("result")
                        self.requests[definition_id] = retry_request
                        unfinished.append(definition_id)
                        self.write_receipt()
                        continue
                if not item["terminalReceiptPresent"]:
                    raise CoordinationError(
                        f"definition {definition_id} completed without one native receipt"
                    )
                item["succeeded"] = run.get("result") == "succeeded"
                self.write_receipt()
            if not unfinished:
                break
            if self.monotonic() >= deadline:
                self.receipt["status"] = "timedOut"
                self.receipt["succeeded"] = False
                self.write_receipt()
                return False
            self.sleep(self.poll_seconds)

        self.receipt["succeeded"] = all(
            item.get("succeeded") and item.get("terminalReceiptPresent")
            for item in self.receipt["runs"].values()
        )
        self.receipt["status"] = "succeeded" if self.receipt["succeeded"] else "failed"
        self.receipt["finishedAtUtc"] = utc_now()
        self.write_receipt()
        return self.receipt["succeeded"]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Coordinate standard-GC performance consumers from definition 895."
    )
    parser.add_argument("--contract", type=Path, required=True)
    parser.add_argument("--requirements", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--definition306-validation", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--controller-build-id", required=True)
    parser.add_argument("--queue", action="store_true")
    parser.add_argument("--identity-posture", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    contract = json.loads(args.contract.read_text(encoding="utf-8"))
    validated = validate_manifest(args.manifest, args.requirements, contract)
    manifest = validated["manifest"]
    requirements = validated["requirements"]
    manifest_sha256 = file_sha256(args.manifest)

    requests = {
        definition_id: build_request(
            definition_id,
            contract,
            manifest,
            (
                _artifact_map_306(
                    manifest, requirements, args.definition306_validation
                )
                if definition_id == 306
                else _artifact_map_702_1012(
                    definition_id, manifest, requirements
                )
            ),
            manifest_sha256,
            args.controller_build_id,
            1,
        )
        for definition_id in IMPLEMENTED_DEFINITION_IDS
    }

    aspnet = contract["aspNetBoundary"]
    queue_blockers = [
        "ASP.NET final scalar binding is not frozen and implemented"
        if (
            aspnet["status"] != "ready"
            or not aspnet["approvedBindingFileSha256"]
        )
        else None
    ]
    queue_blockers = [item for item in queue_blockers if item]
    access_token = os.environ.get("SYSTEM_ACCESSTOKEN", "")
    client = AzureDevOpsClient(
        os.environ.get("SYSTEM_COLLECTIONURI", "https://dev.azure.com/dnceng/"),
        os.environ.get("SYSTEM_TEAMPROJECTID", "internal"),
        access_token,
    )
    permission_preflight = None
    if args.queue:
        if queue_blockers:
            raise CoordinationError("; ".join(queue_blockers))
        if not args.identity_posture:
            raise PermissionPreflightError("queue mode requires approved identity posture")
        if not access_token:
            raise PermissionPreflightError("queue mode requires System.AccessToken")
        permission_preflight = validate_identity_posture(
            client, args.identity_posture, contract
        )

    coordinator = PerformanceCoordinator(
        client,
        args.output_directory,
        requests,
        args.queue and not queue_blockers,
        permission_preflight,
    )
    succeeded = coordinator.run()
    if not args.queue:
        receipt = json.loads(coordinator.receipt_path.read_text(encoding="utf-8"))
        receipt["queueBlockers"] = queue_blockers
        receipt["aspNetBoundary"] = aspnet
        coordinator.receipt = receipt
        coordinator.write_receipt()
        return 0
    return 0 if succeeded else 1


if __name__ == "__main__":
    raise SystemExit(main())
