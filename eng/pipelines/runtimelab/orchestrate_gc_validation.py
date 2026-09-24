#!/usr/bin/env python3

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import time
from typing import Callable
import urllib.error
import urllib.parse
import urllib.request


ROOTS = (
    "gcstress0x3-gcstress0xc",
    "gcstress-extra",
    "gc-longrunning",
    "gc-simulator",
    "gc-standalone",
)
EXPECTED_DEFINITION_ID = 163
BUILD_SECURITY_NAMESPACE_ID = "33344d9c-fc72-4d6f-aba5-fa317101a7e9"
TRANSIENT_INFRASTRUCTURE_PATTERNS = (
    re.compile(r"\bwe stopped hearing from agent\b", re.IGNORECASE),
    re.compile(r"\bagent\b.*\blost communication\b", re.IGNORECASE),
    re.compile(r"\bagent\b.*\bwas not heard from\b", re.IGNORECASE),
    re.compile(r"\bagent\b.*\bdisconnected\b", re.IGNORECASE),
    re.compile(
        r"\bjob has been abandoned because agent\b.*\bdid not renew the lock\b",
        re.IGNORECASE,
    ),
)
TRANSIENT_COMPANION_PATTERNS = (
    re.compile(r"\boperation was canceled\b", re.IGNORECASE),
    re.compile(r"\bjob was canceled\b", re.IGNORECASE),
)


class OrchestrationError(RuntimeError):
    pass


class PermissionPreflightError(OrchestrationError):
    pass


class AmbiguousQueueResponse(OrchestrationError):
    pass


class ApiRequestError(OrchestrationError):
    def __init__(self, method: str, url: str, status: int | None, detail: str):
        super().__init__(f"{method} {url} failed ({status or 'no status'}): {detail}")
        self.method = method
        self.url = url
        self.status = status


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def canonical_json_bytes(value: object) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def sha256_bytes(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def atomic_write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path = path.with_suffix(f"{path.suffix}.tmp")
    temporary_path.write_bytes(canonical_json_bytes(value) + b"\n")
    temporary_path.replace(path)


def variable_value(value: object) -> str:
    if isinstance(value, dict):
        value = value.get("value", "")
    return str(value)


def repository_resource(run: dict) -> dict:
    return run.get("resources", {}).get("repositories", {}).get("self", {})


def run_attempt(run: dict) -> int:
    value = run.get("templateParameters", {}).get("gcValidationAttempt", 1)
    return int(variable_value(value))


def run_matches(
    run: dict,
    *,
    campaign_id: str,
    parent_build_id: str,
    root: str,
    source_ref: str,
    source_version: str,
    attempt: int | None = None,
) -> bool:
    parameters = run.get("templateParameters", {})
    repository = repository_resource(run)
    if (
        int(run.get("id", 0)) <= 0
        or variable_value(parameters.get("gcValidationMode")) != "correctness-shard"
        or variable_value(parameters.get("gcValidationCampaignId")) != campaign_id
        or variable_value(parameters.get("gcValidationParentBuildId"))
        != parent_build_id
        or variable_value(parameters.get("gcValidationRoot")) != root
        or repository.get("refName") != source_ref
        or repository.get("version") != source_version
    ):
        return False
    return attempt is None or run_attempt(run) == attempt


def collect_timeline_errors(timeline: dict) -> list[str]:
    messages = []
    for record in timeline.get("records", []):
        for issue in record.get("issues", []):
            if issue.get("type", "").lower() == "error" and issue.get("message"):
                messages.append(issue["message"])
    return messages


def classify_transient_infrastructure_failure(timeline: dict) -> dict:
    messages = collect_timeline_errors(timeline)
    strong_matches = [
        message
        for message in messages
        if any(pattern.search(message) for pattern in TRANSIENT_INFRASTRUCTURE_PATTERNS)
    ]
    unmatched = [
        message
        for message in messages
        if message not in strong_matches
        and not any(pattern.search(message) for pattern in TRANSIENT_COMPANION_PATTERNS)
    ]
    return {
        "isTransientInfrastructureFailure": bool(strong_matches) and not unmatched,
        "errorMessages": messages,
        "matchedInfrastructureMessages": strong_matches,
        "unmatchedErrorMessages": unmatched,
    }


def evaluate_queue_permission(
    namespace_response: dict,
    connection_data: dict,
    permission_response: dict,
    permission_token: str,
) -> dict:
    namespaces = namespace_response.get("value", [])
    if len(namespaces) != 1:
        raise PermissionPreflightError("Build security namespace was not returned.")

    actions = {
        action["name"]: int(action["bit"])
        for action in namespaces[0].get("actions", [])
    }
    queue_bit = actions.get("QueueBuilds")
    if queue_bit is None:
        raise PermissionPreflightError(
            "Build security namespace does not define QueueBuilds."
        )

    authenticated_user = connection_data.get("authenticatedUser", {})
    descriptor = authenticated_user.get("descriptor")
    if not descriptor:
        raise PermissionPreflightError(
            "Azure DevOps did not identify the System.AccessToken principal."
        )

    values = permission_response.get("value", [])
    if len(values) != 1 or not isinstance(values[0], bool):
        raise PermissionPreflightError(
            "Azure DevOps did not return one QueueBuilds permission result."
        )
    allowed = values[0]
    return {
        "allowed": allowed,
        "descriptor": descriptor,
        "displayName": authenticated_user.get("providerDisplayName")
        or authenticated_user.get("displayName"),
        "permissionToken": permission_token,
        "queueBuildsBit": queue_bit,
    }


class AzureDevOpsClient:
    def __init__(
        self,
        collection_uri: str,
        project_id: str,
        pipeline_id: int,
        access_token: str,
    ):
        self.collection_uri = collection_uri.rstrip("/") + "/"
        self.project_id = project_id
        self.pipeline_id = pipeline_id
        self.access_token = access_token

    def request_json(
        self,
        method: str,
        url: str,
        body: dict | None = None,
        timeout_seconds: int = 60,
    ) -> dict:
        data = canonical_json_bytes(body) if body is not None else None
        authorization_header = " ".join(("Bearer", self.access_token))
        request = urllib.request.Request(
            url,
            data=data,
            method=method,
            headers={
                "Authorization": authorization_header,
                "Accept": "application/json",
                "Content-Type": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
                content = response.read()
                return json.loads(content) if content else {}
        except urllib.error.HTTPError as error:
            detail = error.read().decode("utf-8", errors="replace")
            if method == "POST" and (
                error.code in (408, 429) or error.code >= 500
            ):
                raise AmbiguousQueueResponse(
                    f"Queue response was ambiguous after HTTP {error.code}: {detail}"
                ) from error
            raise ApiRequestError(method, url, error.code, detail) from error
        except (urllib.error.URLError, TimeoutError, ConnectionError) as error:
            if method == "POST":
                raise AmbiguousQueueResponse(
                    f"Queue response was ambiguous: {error}"
                ) from error
            raise ApiRequestError(method, url, None, str(error)) from error

    @property
    def runs_url(self) -> str:
        return (
            f"{self.collection_uri}{self.project_id}/_apis/pipelines/"
            f"{self.pipeline_id}/runs?api-version=7.1"
        )

    def preflight_queue_permission(self) -> dict:
        namespace_url = (
            f"{self.collection_uri}_apis/securitynamespaces/"
            f"{BUILD_SECURITY_NAMESPACE_ID}?api-version=7.1"
        )
        connection_url = (
            f"{self.collection_uri}_apis/connectionData"
            "?connectOptions=1&lastChangeId=-1&lastChangeId64=-1"
        )
        namespace_response = self.request_json("GET", namespace_url)
        connection_data = self.request_json("GET", connection_url)
        descriptor = connection_data.get("authenticatedUser", {}).get("descriptor")
        if not descriptor:
            raise PermissionPreflightError(
                "Azure DevOps did not identify the System.AccessToken principal."
            )

        permission_token = f"{self.project_id}/{self.pipeline_id}"
        query = urllib.parse.urlencode(
            {
                "tokens": permission_token,
                "alwaysAllowAdministrators": "false",
                "api-version": "7.1",
            }
        )
        permission_url = (
            f"{self.collection_uri}_apis/permissions/"
            f"{BUILD_SECURITY_NAMESPACE_ID}/{actions_queue_bit(namespace_response)}"
            f"?{query}"
        )
        permission_response = self.request_json("GET", permission_url)
        result = evaluate_queue_permission(
            namespace_response,
            connection_data,
            permission_response,
            permission_token,
        )
        if not result["allowed"]:
            raise PermissionPreflightError(
                "QueueBuilds is not effectively allowed for "
                f"{result['displayName'] or result['descriptor']} on "
                f"pipeline {self.pipeline_id} (token {permission_token})."
            )
        return result

    def list_runs(self) -> list[dict]:
        response = self.request_json("GET", self.runs_url)
        if isinstance(response, list):
            return response
        return response.get("value", [])

    def get_run(self, run_id: int) -> dict:
        url = (
            f"{self.collection_uri}{self.project_id}/_apis/pipelines/"
            f"{self.pipeline_id}/runs/{run_id}?api-version=7.1"
        )
        return self.request_json("GET", url)

    def queue_run(self, request: dict) -> dict:
        return self.request_json("POST", self.runs_url, request)

    def get_timeline(self, run_id: int) -> dict:
        url = (
            f"{self.collection_uri}{self.project_id}/_apis/build/builds/"
            f"{run_id}/timeline?api-version=7.1"
        )
        return self.request_json("GET", url)


class GcValidationOrchestrator:
    def __init__(
        self,
        client: AzureDevOpsClient,
        output_directory: Path,
        campaign_id: str,
        parent_build_id: str,
        source_ref: str,
        source_version: str,
        monitor_timeout_seconds: int,
        poll_seconds: int,
        adoption_timeout_seconds: int,
        sleep: Callable[[float], None] = time.sleep,
        monotonic: Callable[[], float] = time.monotonic,
    ):
        self.client = client
        self.output_directory = output_directory
        self.receipt_path = output_directory / "gc-validation-children.json"
        self.campaign_id = campaign_id
        self.parent_build_id = parent_build_id
        self.source_ref = source_ref
        self.source_version = source_version
        self.monitor_timeout_seconds = monitor_timeout_seconds
        self.poll_seconds = poll_seconds
        self.adoption_timeout_seconds = adoption_timeout_seconds
        self.sleep = sleep
        self.monotonic = monotonic
        self.receipt = {
            "schemaVersion": 1,
            "definitionId": client.pipeline_id,
            "campaignId": campaign_id,
            "parentBuildId": parent_build_id,
            "sourceRef": source_ref,
            "sourceVersion": source_version,
            "startedAt": utc_now(),
            "status": "initializing",
            "permissionPreflight": None,
            "children": {},
            "queueRequestsSubmitted": 0,
            "runsQueued": 0,
            "allTerminalReceipts": False,
            "succeeded": False,
        }

    def write_receipt(self) -> None:
        self.receipt["updatedAt"] = utc_now()
        canonical = dict(self.receipt)
        canonical.pop("canonicalSha256", None)
        self.receipt["canonicalSha256"] = sha256_bytes(
            canonical_json_bytes(canonical)
        )
        atomic_write_json(self.receipt_path, self.receipt)

    def build_request(self, root: str, attempt: int) -> dict:
        return {
            "resources": {
                "repositories": {
                    "self": {
                        "refName": self.source_ref,
                        "version": self.source_version,
                    }
                }
            },
            "templateParameters": {
                "gcValidationMode": "correctness-shard",
                "gcValidationRoot": root,
                "gcValidationCampaignId": self.campaign_id,
                "gcValidationParentBuildId": self.parent_build_id,
                "gcValidationAttempt": str(attempt),
            },
        }

    def matching_runs(self, root: str, attempt: int | None = None) -> list[dict]:
        matches = [
            run
            for run in self.client.list_runs()
            if run_matches(
                run,
                campaign_id=self.campaign_id,
                parent_build_id=self.parent_build_id,
                root=root,
                source_ref=self.source_ref,
                source_version=self.source_version,
                attempt=attempt,
            )
        ]
        return sorted(
            matches,
            key=lambda run: (run_attempt(run), int(run["id"])),
            reverse=True,
        )

    def persist_attempt(
        self,
        root: str,
        request: dict,
        run: dict,
        *,
        adopted: bool,
        ambiguous_response: bool,
    ) -> dict:
        attempt = int(request["templateParameters"]["gcValidationAttempt"])
        run_id = int(run["id"])
        if run_id <= 0:
            raise OrchestrationError(
                f"{root} attempt {attempt} returned invalid run ID {run_id}."
            )
        request_path = self.output_directory / f"{root}-attempt{attempt}-request.json"
        request_bytes = canonical_json_bytes(request) + b"\n"
        request_path.write_bytes(request_bytes)
        attempt_receipt = {
            "attempt": attempt,
            "requestFile": request_path.name,
            "requestSha256": sha256_bytes(request_bytes),
            "runId": run_id,
            "runName": run.get("name"),
            "runUrl": run.get("url"),
            "adopted": adopted,
            "ambiguousQueueResponse": ambiguous_response,
            "state": run.get("state", "unknown"),
            "result": run.get("result"),
            "terminal": run.get("state") == "completed",
        }
        child = self.receipt["children"].setdefault(
            root,
            {
                "root": root,
                "attempts": [],
                "terminalReceiptPresent": False,
                "succeeded": False,
            },
        )
        child["attempts"] = [
            item
            for item in child["attempts"]
            if item["attempt"] != attempt_receipt["attempt"]
        ]
        child["attempts"].append(attempt_receipt)
        child["attempts"].sort(key=lambda item: item["attempt"])
        child["currentRunId"] = attempt_receipt["runId"]
        child["currentAttempt"] = attempt
        self.write_receipt()
        return attempt_receipt

    def adopt_after_ambiguous_response(
        self, root: str, attempt: int
    ) -> dict:
        deadline = self.monotonic() + self.adoption_timeout_seconds
        last_read_error = None
        while True:
            try:
                matches = self.matching_runs(root, attempt)
                last_read_error = None
            except ApiRequestError as error:
                matches = []
                last_read_error = str(error)
            active = [run for run in matches if run.get("state") != "completed"]
            if len(active) > 1:
                raise OrchestrationError(
                    f"{root} has multiple active attempt {attempt} runs: "
                    + ", ".join(str(run["id"]) for run in active)
                )
            if active:
                return active[0]
            if matches:
                return matches[0]
            if self.monotonic() >= deadline:
                detail = (
                    f" The last adoption read failed with: {last_read_error}"
                    if last_read_error
                    else ""
                )
                raise AmbiguousQueueResponse(
                    f"No matching {root} attempt {attempt} run appeared after "
                    "an ambiguous queue response; the queue request will not be "
                    f"retried.{detail}"
                )
            self.sleep(min(self.poll_seconds, 5))

    def ensure_run(self, root: str, attempt: int) -> dict:
        all_matches = self.matching_runs(root)
        active = [run for run in all_matches if run.get("state") != "completed"]
        if len(active) > 1:
            raise OrchestrationError(
                f"{root} has multiple active matching runs: "
                + ", ".join(str(run["id"]) for run in active)
            )
        if active:
            active_attempt = run_attempt(active[0])
            request = self.build_request(root, active_attempt)
            return self.persist_attempt(
                root,
                request,
                active[0],
                adopted=True,
                ambiguous_response=False,
            )

        attempt_matches = [
            run for run in all_matches if run_attempt(run) == attempt
        ]
        if attempt_matches:
            request = self.build_request(root, attempt)
            return self.persist_attempt(
                root,
                request,
                attempt_matches[0],
                adopted=True,
                ambiguous_response=False,
            )

        request = self.build_request(root, attempt)
        request_path = self.output_directory / f"{root}-attempt{attempt}-request.json"
        request_bytes = canonical_json_bytes(request) + b"\n"
        request_path.write_bytes(request_bytes)
        self.receipt["children"].setdefault(
            root,
            {
                "root": root,
                "attempts": [],
                "terminalReceiptPresent": False,
                "succeeded": False,
            },
        )
        self.receipt["children"][root]["pendingRequest"] = {
            "attempt": attempt,
            "requestFile": request_path.name,
            "requestSha256": sha256_bytes(request_bytes),
        }
        self.receipt["queueRequestsSubmitted"] += 1
        self.write_receipt()

        ambiguous_response = False
        try:
            run = self.client.queue_run(request)
            if "id" not in run:
                ambiguous_response = True
                run = self.adopt_after_ambiguous_response(root, attempt)
        except AmbiguousQueueResponse:
            ambiguous_response = True
            run = self.adopt_after_ambiguous_response(root, attempt)

        self.receipt["runsQueued"] += 1
        self.receipt["children"][root].pop("pendingRequest", None)
        return self.persist_attempt(
            root,
            request,
            run,
            adopted=ambiguous_response,
            ambiguous_response=ambiguous_response,
        )

    def refresh_attempt(self, root: str, attempt_receipt: dict) -> dict:
        run = self.client.get_run(attempt_receipt["runId"])
        attempt_receipt["state"] = run.get("state", "unknown")
        attempt_receipt["result"] = run.get("result")
        attempt_receipt["terminal"] = run.get("state") == "completed"
        if attempt_receipt["terminal"] and run.get("result") != "succeeded":
            classification = classify_transient_infrastructure_failure(
                self.client.get_timeline(attempt_receipt["runId"])
            )
            attempt_receipt["failureClassification"] = classification
        self.write_receipt()
        return run

    def current_attempt_receipt(self, root: str) -> dict:
        child = self.receipt["children"][root]
        run_id = child["currentRunId"]
        return next(
            attempt for attempt in child["attempts"] if attempt["runId"] == run_id
        )

    def finalize_child(self, root: str, attempt_receipt: dict) -> None:
        child = self.receipt["children"][root]
        child["terminalReceiptPresent"] = bool(attempt_receipt["terminal"])
        child["finalRunId"] = attempt_receipt["runId"]
        child["finalAttempt"] = attempt_receipt["attempt"]
        child["finalResult"] = attempt_receipt["result"]
        child["succeeded"] = (
            attempt_receipt["terminal"]
            and attempt_receipt["result"] == "succeeded"
        )
        self.write_receipt()

    def run(self) -> bool:
        self.output_directory.mkdir(parents=True, exist_ok=True)
        self.write_receipt()
        preflight = self.client.preflight_queue_permission()
        self.receipt["permissionPreflight"] = preflight
        self.receipt["status"] = "queueing"
        self.write_receipt()

        for root in ROOTS:
            matches = self.matching_runs(root)
            active = [run for run in matches if run.get("state") != "completed"]
            if len(active) > 1:
                raise OrchestrationError(
                    f"{root} has multiple active matching runs: "
                    + ", ".join(str(run["id"]) for run in active)
                )
            by_attempt = {}
            for run in matches:
                by_attempt.setdefault(run_attempt(run), run)
            for attempt in sorted(by_attempt):
                run = by_attempt[attempt]
                self.persist_attempt(
                    root,
                    self.build_request(root, attempt),
                    run,
                    adopted=True,
                    ambiguous_response=False,
                )
            if active:
                active_attempt = run_attempt(active[0])
                self.persist_attempt(
                    root,
                    self.build_request(root, active_attempt),
                    active[0],
                    adopted=True,
                    ambiguous_response=False,
                )
                continue

            if not matches:
                self.ensure_run(root, 1)

        self.receipt["status"] = "monitoring"
        self.write_receipt()
        deadline = self.monotonic() + self.monitor_timeout_seconds
        while True:
            unfinished = []
            for root in ROOTS:
                child = self.receipt["children"][root]
                if child.get("terminalReceiptPresent"):
                    continue

                attempt_receipt = self.current_attempt_receipt(root)
                try:
                    run = self.refresh_attempt(root, attempt_receipt)
                except ApiRequestError as error:
                    child.setdefault("monitoringErrors", []).append(
                        {"at": utc_now(), "error": str(error)}
                    )
                    self.write_receipt()
                    unfinished.append(root)
                    continue
                if run.get("state") != "completed":
                    unfinished.append(root)
                    continue

                classification = attempt_receipt.get("failureClassification", {})
                if (
                    run.get("result") != "succeeded"
                    and attempt_receipt["attempt"] == 1
                    and classification.get("isTransientInfrastructureFailure")
                ):
                    self.ensure_run(root, 2)
                    unfinished.append(root)
                    continue

                self.finalize_child(root, attempt_receipt)

            if not unfinished:
                break
            if self.monotonic() >= deadline:
                self.receipt["status"] = "timedOut"
                self.receipt["timedOutRoots"] = unfinished
                self.receipt["allTerminalReceipts"] = False
                self.receipt["succeeded"] = False
                self.receipt["finishedAt"] = utc_now()
                self.write_receipt()
                return False
            self.sleep(self.poll_seconds)

        self.receipt["allTerminalReceipts"] = all(
            child.get("terminalReceiptPresent")
            for child in self.receipt["children"].values()
        ) and set(self.receipt["children"]) == set(ROOTS)
        self.receipt["succeeded"] = self.receipt["allTerminalReceipts"] and all(
            child.get("succeeded") for child in self.receipt["children"].values()
        )
        self.receipt["status"] = (
            "succeeded" if self.receipt["succeeded"] else "failed"
        )
        self.receipt["finishedAt"] = utc_now()
        self.write_receipt()
        return self.receipt["succeeded"]


def require_environment(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        raise OrchestrationError(f"Required environment variable {name} is empty.")
    return value


def actions_queue_bit(namespace_response: dict) -> int:
    namespaces = namespace_response.get("value", [])
    if len(namespaces) != 1:
        raise PermissionPreflightError("Build security namespace was not returned.")
    action = next(
        (
            action
            for action in namespaces[0].get("actions", [])
            if action.get("name") == "QueueBuilds"
        ),
        None,
    )
    if action is None:
        raise PermissionPreflightError(
            "Build security namespace does not define QueueBuilds."
        )
    return int(action["bit"])


def resolve_campaign_id(explicit_campaign_id: str, build_id: str) -> str:
    campaign_id = explicit_campaign_id or f"gc-validation-{build_id}"
    if not re.fullmatch(r"[A-Za-z0-9._-]{1,128}", campaign_id):
        raise OrchestrationError(
            "Campaign ID must contain only letters, digits, '.', '_', or '-' "
            "and be at most 128 characters."
        )
    return campaign_id


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Queue and monitor definition 163 GC validation shard runs."
    )
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--campaign-id", default="")
    parser.add_argument("--parent-build-id", default="")
    parser.add_argument("--monitor-timeout-minutes", type=int, default=1380)
    parser.add_argument("--poll-seconds", type=int, default=30)
    parser.add_argument("--adoption-timeout-seconds", type=int, default=120)
    args = parser.parse_args()

    output_directory = args.output_directory
    output_directory.mkdir(parents=True, exist_ok=True)
    fallback_receipt = output_directory / "gc-validation-children.json"
    try:
        collection_uri = require_environment("SYSTEM_COLLECTIONURI")
        project_id = require_environment("SYSTEM_TEAMPROJECTID")
        definition_id = int(require_environment("SYSTEM_DEFINITIONID"))
        if definition_id != EXPECTED_DEFINITION_ID:
            raise OrchestrationError(
                f"This controller must run in definition {EXPECTED_DEFINITION_ID}, "
                f"not {definition_id}."
            )
        build_id = require_environment("BUILD_BUILDID")
        source_ref = require_environment("BUILD_SOURCEBRANCH")
        source_version = require_environment("BUILD_SOURCEVERSION")
        access_token = require_environment("SYSTEM_ACCESSTOKEN")
        if not re.fullmatch(r"[0-9a-fA-F]{40}", source_version):
            raise OrchestrationError(
                "BUILD_SOURCEVERSION must be the parent's full 40-character commit ID."
            )
        if not source_ref.startswith("refs/"):
            raise OrchestrationError("BUILD_SOURCEBRANCH must be a full refs/* name.")
        parent_build_id = args.parent_build_id or build_id
        if not parent_build_id.isdigit():
            raise OrchestrationError("Parent build ID must be numeric.")
        campaign_id = resolve_campaign_id(args.campaign_id, parent_build_id)

        client = AzureDevOpsClient(
            collection_uri,
            project_id,
            definition_id,
            access_token,
        )
        orchestrator = GcValidationOrchestrator(
            client,
            output_directory,
            campaign_id,
            parent_build_id,
            source_ref,
            source_version,
            args.monitor_timeout_minutes * 60,
            args.poll_seconds,
            args.adoption_timeout_seconds,
        )
        return 0 if orchestrator.run() else 1
    except Exception as error:
        if not fallback_receipt.exists():
            receipt = {
                "schemaVersion": 1,
                "status": "failedBeforeInitialization",
                "error": str(error),
                "finishedAt": utc_now(),
                "runsQueued": 0,
                "allTerminalReceipts": False,
                "succeeded": False,
            }
            receipt["canonicalSha256"] = sha256_bytes(canonical_json_bytes(receipt))
            atomic_write_json(fallback_receipt, receipt)
        else:
            receipt = json.loads(fallback_receipt.read_text(encoding="utf-8"))
            receipt["status"] = (
                "permissionDenied"
                if isinstance(error, PermissionPreflightError)
                else "failed"
            )
            receipt["error"] = str(error)
            receipt["finishedAt"] = utc_now()
            receipt["allTerminalReceipts"] = False
            receipt["succeeded"] = False
            canonical = dict(receipt)
            canonical.pop("canonicalSha256", None)
            receipt["canonicalSha256"] = sha256_bytes(
                canonical_json_bytes(canonical)
            )
            atomic_write_json(fallback_receipt, receipt)
        print(f"GC validation orchestration failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
