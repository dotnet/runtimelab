#!/usr/bin/env python3

import argparse
from datetime import datetime, timezone
import hashlib
import http.client
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import ssl
import sys
import time
from typing import Callable
import urllib.error
import urllib.parse
import urllib.request
import zipfile


ROOTS = (
    "gcstress0x3-gcstress0xc",
    "gcstress-extra",
    "gc-longrunning",
    "gc-simulator",
    "gc-standalone",
)
DIRECT_ROOTS = ROOTS + ("runtime-coreclr-correctness",)
EXPECTED_DEFINITION_ID = 163
BUILD_SECURITY_NAMESPACE_ID = "33344d9c-fc72-4d6f-aba5-fa317101a7e9"
RUN_KIND_DIRECT = "direct"
RUN_KIND_PARENT_CHILD = "parent-child"
RUN_KINDS = (RUN_KIND_DIRECT, RUN_KIND_PARENT_CHILD)
RUN_IDENTITY_TAG_PREFIXES = {
    "mode": "gc-validation.mode.",
    "runKind": "gc-validation.kind.",
    "campaignId": "gc-validation.campaign.",
    "parentBuildId": "gc-validation.parent.",
    "root": "gc-validation.root.",
    "attempt": "gc-validation.attempt.",
}
RUN_IDENTITY_TAG_FIELDS = (
    "mode",
    "runKind",
    "campaignId",
    "parentBuildId",
    "root",
    "attempt",
)
ADMISSION_STAGE_IDENTIFIER = "gcvalidationadmission"
ADMISSION_JOB_IDENTIFIER = "admitcanonicalgcvalidationshard"
BUILD_STAGE_IDENTIFIER = "build"
ADMISSION_ARTIFACT_PREFIX = "GCValidationAdmission_"
ADMISSION_RECEIPT_NAME = "gc-validation-admission.json"
MAX_ADMISSION_ARTIFACT_BYTES = 1024 * 1024
MAX_ADMISSION_RECEIPT_BYTES = 256 * 1024
TERMINAL_EVIDENCE_GRACE_SECONDS = 120
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


class EvidenceValidationError(OrchestrationError):
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


def repository_resource(run: dict) -> dict:
    resources = run.get("resources") or {}
    repositories = resources.get("repositories") or {}
    return repositories.get("self") or {}


def run_tags(run: dict) -> set[str]:
    tags = run.get("tags") or []
    return {
        str(tag).casefold()
        for tag in tags
        if isinstance(tag, str) and tag
    }


def identity_tags(
    *,
    run_kind: str,
    campaign_id: str,
    parent_build_id: str,
    root: str,
    attempt: int,
) -> tuple[str, ...]:
    values = {
        "mode": "correctness-shard",
        "runKind": run_kind,
        "campaignId": campaign_id,
        "parentBuildId": parent_build_id,
        "root": root,
        "attempt": str(attempt),
    }
    return tuple(
        RUN_IDENTITY_TAG_PREFIXES[name] + values[name]
        for name in RUN_IDENTITY_TAG_FIELDS
    )


def run_tag_value(run: dict, field: str) -> str:
    prefix = RUN_IDENTITY_TAG_PREFIXES[field]
    values = [
        tag[len(prefix) :]
        for tag in run.get("tags") or []
        if isinstance(tag, str)
        and tag.casefold().startswith(prefix.casefold())
    ]
    if len(values) != 1:
        raise ValueError(
            f"Run has {len(values)} {field} identity tags; expected one."
        )
    return values[0]


def has_complete_run_identity(run: dict) -> bool:
    tags = run_tags(run)
    return all(
        sum(tag.startswith(prefix.casefold()) for tag in tags) == 1
        for prefix in RUN_IDENTITY_TAG_PREFIXES.values()
    )


def run_attempt(run: dict) -> int:
    return int(run_tag_value(run, "attempt"))


def run_identity_matches(
    run: dict,
    *,
    run_kind: str,
    campaign_id: str,
    parent_build_id: str,
    root: str,
) -> bool:
    try:
        run_id = int(run.get("id", 0))
        actual_attempt = run_attempt(run)
    except (TypeError, ValueError):
        return False
    expected_tags = {
        tag.casefold()
        for tag in identity_tags(
            run_kind=run_kind,
            campaign_id=campaign_id,
            parent_build_id=parent_build_id,
            root=root,
            attempt=actual_attempt,
        )
    }
    return (
        run_id > 0
        and has_complete_run_identity(run)
        and expected_tags.issubset(run_tags(run))
    )


def run_matches(
    run: dict,
    *,
    run_kind: str,
    campaign_id: str,
    parent_build_id: str,
    root: str,
    source_ref: str,
    source_version: str,
    attempt: int | None = None,
) -> bool:
    repository = repository_resource(run)
    if not run_identity_matches(
        run,
        run_kind=run_kind,
        campaign_id=campaign_id,
        parent_build_id=parent_build_id,
        root=root,
    ):
        return False
    if (
        repository.get("refName") != source_ref
        or repository.get("version") != source_version
    ):
        return False
    try:
        actual_attempt = run_attempt(run)
    except (TypeError, ValueError):
        return False
    if actual_attempt not in (1, 2):
        return False
    return attempt is None or actual_attempt == attempt


def collect_timeline_errors(timeline: dict) -> list[str]:
    messages = []
    for record in timeline_records(timeline):
        for issue in record.get("issues", []):
            if issue.get("type", "").lower() == "error" and issue.get("message"):
                messages.append(issue["message"])
    return messages


def timeline_record_snapshot(record: dict) -> dict:
    return {
        key: record.get(key)
        for key in (
            "id",
            "parentId",
            "type",
            "name",
            "identifier",
            "state",
            "result",
            "order",
        )
    }


def timeline_records(timeline: dict) -> list[dict]:
    records = timeline.get("records")
    return records if isinstance(records, list) else []


def timeline_record_identifier(record: dict) -> str:
    identifier = str(record.get("identifier", ""))
    parts = identifier.split(".")
    if parts and parts[-1].casefold() == "__default":
        parts.pop()
    return (parts[-1] if parts else "").casefold()


def find_timeline_record(
    timeline: dict, record_type: str, identifier: str
) -> tuple[dict | None, str | None]:
    matches = [
        record
        for record in timeline_records(timeline)
        if str(record.get("type", "")).casefold() == record_type.casefold()
        and timeline_record_identifier(record) == identifier.casefold()
    ]
    if len(matches) != 1:
        return (
            None,
            f"expected one {record_type} timeline record for {identifier}, "
            f"found {len(matches)}",
        )
    return matches[0], None


def active_admission_evidence(timeline: dict) -> dict:
    errors = []
    stage, stage_error = find_timeline_record(
        timeline, "Stage", ADMISSION_STAGE_IDENTIFIER
    )
    job, job_error = find_timeline_record(
        timeline, "Job", ADMISSION_JOB_IDENTIFIER
    )
    stage_or_job_records = [
        record
        for record in timeline_records(timeline)
        if str(record.get("type", "")).casefold() in ("stage", "job")
    ]
    pending = (
        stage is None
        and job is None
        and not stage_or_job_records
    ) or (stage is not None and job is None)
    if not pending:
        errors.extend(error for error in (stage_error, job_error) if error)
    for label, record in (("admission stage", stage), ("admission job", job)):
        if record is not None and record.get("result") not in (None, "succeeded"):
            errors.append(
                f"{label} has disqualifying result {record.get('result')!r}"
            )
            pending = False
    valid = not pending and not errors
    return {
        "valid": valid,
        "pending": pending,
        "terminal": False,
        "status": (
            "admissionObserved"
            if valid
            else "pending"
            if pending
            else "invalid"
        ),
        "errors": errors,
        "admissionStage": timeline_record_snapshot(stage) if stage else None,
        "admissionJob": timeline_record_snapshot(job) if job else None,
    }


def terminal_timeline_evidence(timeline: dict) -> dict:
    errors = []
    stage, stage_error = find_timeline_record(
        timeline, "Stage", ADMISSION_STAGE_IDENTIFIER
    )
    job, job_error = find_timeline_record(
        timeline, "Job", ADMISSION_JOB_IDENTIFIER
    )
    build, build_error = find_timeline_record(
        timeline, "Stage", BUILD_STAGE_IDENTIFIER
    )
    stage_or_job_records = [
        record
        for record in timeline_records(timeline)
        if str(record.get("type", "")).casefold() in ("stage", "job")
    ]
    pending = (
        not stage_or_job_records
        or (stage is not None and job is None)
        or (stage is not None and job is not None and build is None)
    )
    if not pending:
        errors.extend(
            error for error in (stage_error, job_error, build_error) if error
        )
    for label, record in (("admission stage", stage), ("admission job", job)):
        if record is None:
            continue
        if record.get("result") not in (None, "succeeded"):
            errors.append(
                f"{label} has disqualifying terminal state "
                f"({record.get('state')!r}, {record.get('result')!r})"
            )
            pending = False
        elif (
            record.get("state") != "completed"
            or record.get("result") != "succeeded"
        ):
            pending = True
    build_result = build.get("result") if build else None
    if build is not None and build_result == "skipped":
        errors.append(
            "Build stage is skipped "
            f"({build.get('state')!r}, {build_result!r})"
        )
        pending = False
    elif build is not None and (
        build.get("state") != "completed" or not build_result
    ):
        pending = True
    if errors:
        pending = False
    valid = not pending and not errors
    return {
        "valid": valid,
        "pending": pending,
        "terminal": True,
        "status": "valid" if valid else "pending" if pending else "invalid",
        "errors": errors,
        "buildResult": build_result,
        "succeeded": valid and build_result == "succeeded",
        "admissionStage": timeline_record_snapshot(stage) if stage else None,
        "admissionJob": timeline_record_snapshot(job) if job else None,
        "buildStage": timeline_record_snapshot(build) if build else None,
    }


def validate_admission_receipt(
    receipt: object,
    *,
    definition_id: int,
    run_kind: str,
    campaign_id: str,
    parent_build_id: str,
    root: str,
    attempt: int,
    source_ref: str,
    source_version: str,
    run_id: int,
) -> dict:
    if not isinstance(receipt, dict):
        return {
            "valid": False,
            "errors": ["admission receipt is not a JSON object"],
        }
    errors = []
    expected = {
        "schemaVersion": 2,
        "definitionId": definition_id,
        "runKind": run_kind,
        "campaignId": campaign_id,
        "parentBuildId": parent_build_id,
        "root": root,
        "attempt": attempt,
        "sourceRef": source_ref,
        "sourceVersion": source_version,
        "currentRunId": run_id,
        "canonicalRunId": run_id,
        "status": "admitted",
        "admitted": True,
    }
    for name, value in expected.items():
        actual = receipt.get(name)
        if type(actual) is not type(value) or actual != value:
            errors.append(
                f"{name} is {actual!r}, expected {value!r}"
            )
    for name in ("matchingRunIds", "activeRunIds"):
        values = receipt.get(name)
        if not isinstance(values, list) or run_id not in values:
            errors.append(f"{name} does not contain current run {run_id}")
    claimed_hash = receipt.get("canonicalSha256")
    canonical = dict(receipt)
    canonical.pop("canonicalSha256", None)
    actual_hash = sha256_bytes(canonical_json_bytes(canonical))
    if claimed_hash != actual_hash:
        errors.append(
            f"canonicalSha256 is {claimed_hash!r}, expected {actual_hash}"
        )
    return {
        "valid": not errors,
        "errors": errors,
        "claimedCanonicalSha256": claimed_hash,
        "actualCanonicalSha256": actual_hash,
    }


def extract_admission_receipt(content: bytes) -> tuple[dict, dict]:
    if len(content) > MAX_ADMISSION_ARTIFACT_BYTES:
        raise EvidenceValidationError(
            "Admission artifact exceeds the maximum allowed size."
        )
    try:
        with zipfile.ZipFile(io.BytesIO(content)) as archive:
            files = [entry for entry in archive.infolist() if not entry.is_dir()]
            if (
                len(files) != 1
                or PurePosixPath(files[0].filename).name
                != ADMISSION_RECEIPT_NAME
            ):
                raise EvidenceValidationError(
                    "Admission artifact must contain exactly "
                    f"{ADMISSION_RECEIPT_NAME}."
                )
            if files[0].file_size > MAX_ADMISSION_RECEIPT_BYTES:
                raise EvidenceValidationError(
                    "Admission receipt exceeds the maximum allowed size."
                )
            receipt_bytes = archive.read(files[0])
    except (OSError, zipfile.BadZipFile) as error:
        raise EvidenceValidationError(
            f"Admission artifact is not a valid ZIP file: {error}"
        ) from error
    try:
        receipt = json.loads(receipt_bytes)
    except (json.JSONDecodeError, UnicodeDecodeError) as error:
        raise EvidenceValidationError(
            f"Admission receipt is not valid UTF-8 JSON: {error}"
        ) from error
    return receipt, {
        "artifactSha256": sha256_bytes(content),
        "receiptSha256": sha256_bytes(receipt_bytes),
        "receiptPath": files[0].filename,
    }


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

    def request_content(
        self,
        method: str,
        url: str,
        body: dict | None = None,
        timeout_seconds: int = 60,
        max_bytes: int | None = None,
        accept: str = "application/json",
    ) -> bytes:
        data = canonical_json_bytes(body) if body is not None else None
        authorization_header = " ".join(("Bearer", self.access_token))
        request = urllib.request.Request(
            url,
            data=data,
            method=method,
            headers={
                "Authorization": authorization_header,
                "Accept": accept,
                "Content-Type": "application/json",
            },
        )
        try:
            with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
                content = (
                    response.read(max_bytes + 1)
                    if max_bytes is not None
                    else response.read()
                )
                if max_bytes is not None and len(content) > max_bytes:
                    raise EvidenceValidationError(
                        f"Response from {url} exceeds {max_bytes} bytes."
                    )
                return content
        except urllib.error.HTTPError as error:
            try:
                detail = error.read().decode("utf-8", errors="replace")
            except (
                http.client.HTTPException,
                ssl.SSLError,
                OSError,
                UnicodeDecodeError,
            ) as body_error:
                if method == "POST":
                    raise AmbiguousQueueResponse(
                        "Queue response was ambiguous while reading the HTTP "
                        f"{error.code} error body: {body_error}"
                    ) from body_error
                raise ApiRequestError(
                    method,
                    url,
                    error.code,
                    f"error response body could not be read: {body_error}",
                ) from body_error
            if method == "POST" and (
                error.code in (408, 429) or error.code >= 500
            ):
                raise AmbiguousQueueResponse(
                    f"Queue response was ambiguous after HTTP {error.code}: {detail}"
                ) from error
            raise ApiRequestError(method, url, error.code, detail) from error
        except (
            urllib.error.URLError,
            TimeoutError,
            ConnectionError,
            http.client.HTTPException,
            ssl.SSLError,
            OSError,
        ) as error:
            if method == "POST":
                raise AmbiguousQueueResponse(
                    f"Queue response was ambiguous: {error}"
                ) from error
            raise ApiRequestError(method, url, None, str(error)) from error

    def request_json(
        self,
        method: str,
        url: str,
        body: dict | None = None,
        timeout_seconds: int = 60,
    ) -> dict:
        content = self.request_content(method, url, body, timeout_seconds)
        if not content:
            return {}
        try:
            return json.loads(content)
        except (json.JSONDecodeError, UnicodeDecodeError) as error:
            if method == "POST":
                raise AmbiguousQueueResponse(
                    f"Queue response was ambiguous: {error}"
                ) from error
            raise ApiRequestError(method, url, None, str(error)) from error

    def request_bytes(
        self,
        url: str,
        *,
        max_bytes: int,
        timeout_seconds: int = 60,
    ) -> bytes:
        return self.request_content(
            "GET",
            url,
            timeout_seconds=timeout_seconds,
            max_bytes=max_bytes,
            accept="application/zip",
        )

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

    def list_artifacts(self, run_id: int) -> list[dict]:
        url = (
            f"{self.collection_uri}{self.project_id}/_apis/build/builds/"
            f"{run_id}/artifacts?api-version=7.1"
        )
        response = self.request_json("GET", url)
        return response.get("value", [])

    def download_artifact(self, artifact: dict) -> bytes:
        resource = artifact.get("resource") or {}
        if resource.get("type") != "PipelineArtifact":
            raise EvidenceValidationError(
                f"Artifact {artifact.get('name')!r} is not a PipelineArtifact."
            )
        artifact_size = (resource.get("properties") or {}).get("artifactsize")
        try:
            if (
                artifact_size is not None
                and int(artifact_size) > MAX_ADMISSION_ARTIFACT_BYTES
            ):
                raise EvidenceValidationError(
                    f"Artifact {artifact.get('name')!r} exceeds the "
                    "maximum allowed size."
                )
        except (TypeError, ValueError) as error:
            raise EvidenceValidationError(
                f"Artifact {artifact.get('name')!r} has invalid size "
                f"{artifact_size!r}."
            ) from error
        download_url = resource.get("downloadUrl")
        if not isinstance(download_url, str) or not download_url.startswith(
            "https://"
        ):
            raise EvidenceValidationError(
                f"Artifact {artifact.get('name')!r} has no HTTPS download URL."
            )
        return self.request_bytes(
            download_url,
            max_bytes=MAX_ADMISSION_ARTIFACT_BYTES,
        )


def active_candidate_evidence(
    client: AzureDevOpsClient,
    run_id: int,
) -> dict:
    timeline = client.get_timeline(run_id)
    evidence = active_admission_evidence(timeline)
    evidence["runId"] = run_id
    return evidence


def terminal_candidate_evidence(
    client: AzureDevOpsClient,
    *,
    run_id: int,
    run_kind: str,
    campaign_id: str,
    parent_build_id: str,
    root: str,
    attempt: int,
    source_ref: str,
    source_version: str,
) -> tuple[dict, dict]:
    timeline = client.get_timeline(run_id)
    timeline_evidence = terminal_timeline_evidence(timeline)
    artifact_evidence = {
        "valid": False,
        "pending": False,
        "errors": ["timeline evidence is invalid"],
    }
    if timeline_evidence.get("pending") is True:
        artifact_evidence = {
            "valid": False,
            "pending": True,
            "errors": ["timeline evidence is not complete"],
        }
    elif timeline_evidence["valid"]:
        try:
            expected_name = f"{ADMISSION_ARTIFACT_PREFIX}{run_id}"
            artifacts = client.list_artifacts(run_id)
            if not artifacts:
                artifact_evidence = {
                    "valid": False,
                    "pending": True,
                    "errors": [
                        f"{expected_name} artifact is not visible yet."
                    ],
                }
            else:
                matches = [
                    artifact
                    for artifact in artifacts
                    if artifact.get("name") == expected_name
                ]
                if len(matches) != 1:
                    raise EvidenceValidationError(
                        f"Expected one {expected_name} artifact, found "
                        f"{len(matches)}."
                    )
                artifact = matches[0]
                receipt, content_evidence = extract_admission_receipt(
                    client.download_artifact(artifact)
                )
                receipt_evidence = validate_admission_receipt(
                    receipt,
                    definition_id=client.pipeline_id,
                    run_kind=run_kind,
                    campaign_id=campaign_id,
                    parent_build_id=parent_build_id,
                    root=root,
                    attempt=attempt,
                    source_ref=source_ref,
                    source_version=source_version,
                    run_id=run_id,
                )
                resource = artifact.get("resource") or {}
                artifact_evidence = {
                    **receipt_evidence,
                    **content_evidence,
                    "artifactId": artifact.get("id"),
                    "artifactName": artifact.get("name"),
                    "artifactSource": artifact.get("source"),
                    "artifactType": resource.get("type"),
                    "artifactData": resource.get("data"),
                    "artifactSize": (resource.get("properties") or {}).get(
                        "artifactsize"
                    ),
                }
        except EvidenceValidationError as error:
            artifact_evidence = {
                "valid": False,
                "pending": False,
                "errors": [str(error)],
            }
    valid = timeline_evidence["valid"] and artifact_evidence["valid"]
    pending = (
        timeline_evidence.get("pending") is True
        or artifact_evidence.get("pending") is True
    )
    evidence = {
        "valid": valid,
        "pending": pending,
        "terminal": True,
        "status": "valid" if valid else "pending" if pending else "invalid",
        "runId": run_id,
        "buildResult": timeline_evidence.get("buildResult"),
        "succeeded": valid and timeline_evidence["succeeded"],
        "timeline": timeline_evidence,
        "admissionArtifact": artifact_evidence,
    }
    return evidence, timeline


def expire_pending_terminal_evidence(evidence: dict, reason: str) -> dict:
    expired = dict(evidence)
    expired.update(
        {
            "valid": False,
            "pending": False,
            "status": "invalid",
            "pendingExpired": True,
            "pendingExpirationReason": reason,
        }
    )
    return expired


def hydrated_matching_runs(
    client: AzureDevOpsClient,
    *,
    run_kind: str,
    campaign_id: str,
    parent_build_id: str,
    root: str,
    source_ref: str,
    source_version: str,
    attempt: int | None = None,
) -> list[dict]:
    matches = []
    parent_run_id = (
        int(parent_build_id)
        if run_kind == RUN_KIND_PARENT_CHILD
        else 0
    )
    for summary in client.list_runs():
        try:
            run_id = int(summary.get("id", 0))
        except (TypeError, ValueError):
            continue
        # Azure build IDs increase project-wide, so a child queued by this
        # parent must have a larger ID. This avoids hydrating historical List
        # entries whose live response shape can omit tags and resources.
        if run_id <= parent_run_id:
            continue
        if has_complete_run_identity(summary):
            if not run_identity_matches(
                summary,
                run_kind=run_kind,
                campaign_id=campaign_id,
                parent_build_id=parent_build_id,
                root=root,
            ):
                continue
        detail = client.get_run(run_id)
        run = dict(summary)
        run.update(detail)
        if run_matches(
            run,
            run_kind=run_kind,
            campaign_id=campaign_id,
            parent_build_id=parent_build_id,
            root=root,
            source_ref=source_ref,
            source_version=source_version,
            attempt=attempt,
        ):
            matches.append(run)
    return sorted(matches, key=lambda run: int(run["id"]))


def positive_run_id(response: object) -> int | None:
    if not isinstance(response, dict):
        return None
    value = response.get("id")
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        return None
    return value


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
        terminal_evidence_grace_seconds: int = TERMINAL_EVIDENCE_GRACE_SECONDS,
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
        self.terminal_evidence_grace_seconds = terminal_evidence_grace_seconds
        self.sleep = sleep
        self.monotonic = monotonic
        self.terminal_evidence_cache: dict[int, tuple[dict, dict]] = {}
        self.terminal_pending_since: dict[int, float] = {}
        self.receipt = {
            "schemaVersion": 2,
            "definitionId": client.pipeline_id,
            "childRunKind": RUN_KIND_PARENT_CHILD,
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
                "gcValidationRunKind": RUN_KIND_PARENT_CHILD,
                "gcValidationParentBuildId": self.parent_build_id,
                "gcValidationAttempt": str(attempt),
            },
        }

    def matching_runs(self, root: str) -> list[dict]:
        return hydrated_matching_runs(
            self.client,
            run_kind=RUN_KIND_PARENT_CHILD,
            campaign_id=self.campaign_id,
            parent_build_id=self.parent_build_id,
            root=root,
            source_ref=self.source_ref,
            source_version=self.source_version,
        )

    def get_terminal_evidence(
        self,
        root: str,
        run: dict,
        *,
        force_refresh: bool = False,
    ) -> tuple[dict, dict]:
        run_id = int(run["id"])
        if not force_refresh and run_id in self.terminal_evidence_cache:
            return self.terminal_evidence_cache[run_id]
        result = terminal_candidate_evidence(
            self.client,
            run_id=run_id,
            run_kind=RUN_KIND_PARENT_CHILD,
            campaign_id=self.campaign_id,
            parent_build_id=self.parent_build_id,
            root=root,
            attempt=run_attempt(run),
            source_ref=self.source_ref,
            source_version=self.source_version,
        )
        evidence, timeline = result
        if evidence.get("pending") is True:
            now = self.monotonic()
            pending_since = self.terminal_pending_since.setdefault(run_id, now)
            pending_seconds = max(0.0, now - pending_since)
            if pending_seconds >= self.terminal_evidence_grace_seconds:
                evidence = expire_pending_terminal_evidence(
                    evidence,
                    "terminal timeline or admission artifact remained "
                    f"incomplete for {pending_seconds:.1f} seconds",
                )
                result = evidence, timeline
        else:
            self.terminal_pending_since.pop(run_id, None)
        if result[0]["valid"]:
            self.terminal_evidence_cache[run_id] = result
        else:
            self.terminal_evidence_cache.pop(run_id, None)
        return result

    def candidate_evidence(self, root: str, run: dict) -> dict:
        if run.get("state") == "completed":
            evidence, _ = self.get_terminal_evidence(root, run)
            return evidence
        return active_candidate_evidence(self.client, int(run["id"]))

    def record_rejected_candidate(
        self,
        root: str,
        run: dict,
        evidence: dict,
    ) -> None:
        child = self.receipt["children"].setdefault(
            root,
            {
                "root": root,
                "attempts": [],
                "terminalReceiptPresent": False,
                "succeeded": False,
            },
        )
        rejected = {
            "runId": int(run["id"]),
            "attempt": run_attempt(run),
            "state": run.get("state"),
            "result": run.get("result"),
            "nativeEvidence": evidence,
        }
        child["rejectedCandidates"] = [
            candidate
            for candidate in child.get("rejectedCandidates", [])
            if candidate["runId"] != rejected["runId"]
        ]
        child["rejectedCandidates"].append(rejected)
        child["rejectedCandidates"].sort(
            key=lambda candidate: candidate["runId"]
        )

    def validate_attempt_one_for_retry(self, root: str, run: dict) -> dict:
        fresh = dict(run)
        fresh.update(self.client.get_run(int(run["id"])))
        if not run_matches(
            fresh,
            run_kind=RUN_KIND_PARENT_CHILD,
            campaign_id=self.campaign_id,
            parent_build_id=self.parent_build_id,
            root=root,
            source_ref=self.source_ref,
            source_version=self.source_version,
            attempt=1,
        ):
            raise OrchestrationError(
                f"{root} attempt 1 changed identity while validating retry state."
            )
        if fresh.get("state") != "completed":
            raise OrchestrationError(
                f"{root} attempt 2 is invalid because attempt 1 is not terminal."
            )
        evidence, timeline = self.get_terminal_evidence(
            root, fresh, force_refresh=True
        )
        if not evidence["valid"]:
            raise OrchestrationError(
                f"{root} attempt 1 lacks valid native child evidence."
            )
        if evidence["buildResult"] == "succeeded":
            raise OrchestrationError(
                f"{root} attempt 2 is invalid because attempt 1 did not "
                "finish unsuccessfully."
            )
        classification = classify_transient_infrastructure_failure(
            timeline
        )
        if not classification["isTransientInfrastructureFailure"]:
            raise OrchestrationError(
                f"{root} attempt 2 is invalid because attempt 1 is not a "
                "proven transient infrastructure failure."
            )
        fresh["_nativeEvidence"] = evidence
        fresh["_failureClassification"] = classification
        return fresh

    def validated_matching_runs(self, root: str) -> list[dict]:
        matches = []
        for run in self.matching_runs(root):
            evidence = self.candidate_evidence(root, run)
            run = dict(run)
            run["_nativeEvidence"] = evidence
            if evidence["valid"] or evidence.get("pending") is True:
                matches.append(run)
            else:
                self.record_rejected_candidate(root, run, evidence)
        by_attempt = {}
        for run in matches:
            by_attempt.setdefault(run_attempt(run), []).append(run)
        canonical_matches = []
        for runs in by_attempt.values():
            canonical = dict(min(runs, key=lambda run: int(run["id"])))
            duplicates = sorted(
                int(run["id"])
                for run in runs
                if int(run["id"]) != int(canonical["id"])
            )
            if duplicates:
                canonical["_duplicateRunIds"] = duplicates
            canonical_matches.append(canonical)
        if 2 in by_attempt:
            if 1 not in by_attempt:
                raise OrchestrationError(
                    f"{root} attempt 2 exists without attempt 1."
                )
            attempt_one = next(
                run for run in canonical_matches if run_attempt(run) == 1
            )
            validated_attempt_one = self.validate_attempt_one_for_retry(
                root, attempt_one
            )
            canonical_matches = [
                validated_attempt_one if run_attempt(run) == 1 else run
                for run in canonical_matches
            ]
        return sorted(
            canonical_matches,
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
        returned_run_id: int | None = None,
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
        if returned_run_id is not None:
            attempt_receipt["returnedRunId"] = returned_run_id
        if run.get("_duplicateRunIds"):
            attempt_receipt["duplicateRunIds"] = run["_duplicateRunIds"]
        if run.get("_nativeEvidence") is not None:
            attempt_receipt["nativeEvidence"] = run["_nativeEvidence"]
        if run.get("_failureClassification") is not None:
            attempt_receipt["failureClassification"] = run[
                "_failureClassification"
            ]
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

    def adopt_canonical_after_queue_response(
        self, root: str, attempt: int
    ) -> dict:
        deadline = self.monotonic() + self.adoption_timeout_seconds
        last_read_error = None
        while True:
            try:
                matches = [
                    run
                    for run in self.validated_matching_runs(root)
                    if run_attempt(run) == attempt
                ]
                last_read_error = None
            except ApiRequestError as error:
                matches = []
                last_read_error = str(error)
            active = [run for run in matches if run.get("state") != "completed"]
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
                    "the queue response; the queue request will not be retried."
                    f"{detail}"
                )
            self.sleep(min(self.poll_seconds, 5))

    def ensure_run(self, root: str, attempt: int) -> dict:
        all_matches = self.validated_matching_runs(root)
        active = [run for run in all_matches if run.get("state") != "completed"]
        if len(active) > 1:
            raise OrchestrationError(
                f"{root} has multiple active matching runs: "
                + ", ".join(str(run["id"]) for run in active)
            )
        if active:
            active_attempt = run_attempt(active[0])
            if active_attempt != attempt:
                raise OrchestrationError(
                    f"{root} cannot start attempt {attempt} while attempt "
                    f"{active_attempt} is active."
                )
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

        if attempt == 2:
            attempt_one = [
                run for run in all_matches if run_attempt(run) == 1
            ]
            if not attempt_one:
                raise OrchestrationError(
                    f"{root} cannot start attempt 2 without attempt 1."
                )
            self.validate_attempt_one_for_retry(root, attempt_one[0])

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
        returned_run_id = None
        try:
            response = self.client.queue_run(request)
            returned_run_id = positive_run_id(response)
            if returned_run_id is None:
                ambiguous_response = True
        except AmbiguousQueueResponse:
            ambiguous_response = True
        if returned_run_id is not None:
            self.receipt["children"][root]["pendingRequest"][
                "returnedRunId"
            ] = returned_run_id
            self.write_receipt()

        run = self.adopt_canonical_after_queue_response(root, attempt)
        adopted = (
            ambiguous_response
            or returned_run_id is None
            or int(run["id"]) != returned_run_id
        )

        self.receipt["runsQueued"] += 1
        self.receipt["children"][root].pop("pendingRequest", None)
        return self.persist_attempt(
            root,
            request,
            run,
            adopted=adopted,
            ambiguous_response=ambiguous_response,
            returned_run_id=returned_run_id,
        )

    def refresh_attempt(self, root: str, attempt_receipt: dict) -> dict:
        run = self.client.get_run(attempt_receipt["runId"])
        attempt_receipt["state"] = run.get("state", "unknown")
        attempt_receipt["result"] = run.get("result")
        attempt_receipt["terminal"] = run.get("state") == "completed"
        if attempt_receipt["terminal"]:
            evidence, timeline = self.get_terminal_evidence(
                root, run, force_refresh=True
            )
            attempt_receipt["nativeEvidence"] = evidence
        else:
            attempt_receipt["nativeEvidence"] = active_candidate_evidence(
                self.client, int(run["id"])
            )
        if (
            attempt_receipt["terminal"]
            and attempt_receipt["nativeEvidence"]["valid"]
            and not attempt_receipt["nativeEvidence"]["succeeded"]
        ):
            classification = classify_transient_infrastructure_failure(
                timeline
            )
            attempt_receipt["failureClassification"] = classification
        self.write_receipt()
        return run

    def refresh_terminal_attempt_evidence(
        self,
        root: str,
        run: dict,
        attempt_receipt: dict,
    ) -> tuple[dict, dict]:
        evidence, timeline = self.get_terminal_evidence(
            root, run, force_refresh=True
        )
        attempt_receipt["nativeEvidence"] = evidence
        attempt_receipt.pop("failureClassification", None)
        if evidence["valid"] and not evidence["succeeded"]:
            attempt_receipt["failureClassification"] = (
                classify_transient_infrastructure_failure(timeline)
            )
        self.write_receipt()
        return evidence, timeline

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
        evidence = attempt_receipt.get("nativeEvidence") or {}
        child["finalResult"] = evidence.get("buildResult")
        child["succeeded"] = (
            attempt_receipt["terminal"]
            and evidence.get("valid") is True
            and evidence.get("succeeded") is True
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
            matches = self.validated_matching_runs(root)
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
                    evidence = attempt_receipt["nativeEvidence"]
                    if (
                        not evidence["valid"]
                        and evidence.get("pending") is not True
                    ):
                        self.record_rejected_candidate(root, run, evidence)
                        attempt_receipt["rejected"] = True
                        self.write_receipt()
                        self.ensure_run(root, attempt_receipt["attempt"])
                    unfinished.append(root)
                    continue

                evidence, _ = self.refresh_terminal_attempt_evidence(
                    root, run, attempt_receipt
                )
                if evidence.get("pending") is True:
                    unfinished.append(root)
                    continue
                if not evidence["valid"]:
                    self.record_rejected_candidate(root, run, evidence)
                    attempt_receipt["rejected"] = True
                    self.write_receipt()
                    self.ensure_run(root, attempt_receipt["attempt"])
                    unfinished.append(root)
                    continue

                classification = attempt_receipt.get("failureClassification", {})
                if (
                    not evidence["succeeded"]
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


class GcValidationAdmission:
    def __init__(
        self,
        client: AzureDevOpsClient,
        output_directory: Path,
        run_kind: str,
        campaign_id: str,
        parent_build_id: str,
        root: str,
        attempt: int,
        source_ref: str,
        source_version: str,
        current_run_id: int,
        observation_seconds: int,
        poll_seconds: int,
        sleep: Callable[[float], None] = time.sleep,
        monotonic: Callable[[], float] = time.monotonic,
    ):
        self.client = client
        self.output_directory = output_directory
        self.receipt_path = output_directory / "gc-validation-admission.json"
        self.run_kind = run_kind
        self.campaign_id = campaign_id
        self.parent_build_id = parent_build_id
        self.root = root
        self.attempt = attempt
        self.source_ref = source_ref
        self.source_version = source_version
        self.current_run_id = current_run_id
        self.observation_seconds = observation_seconds
        self.poll_seconds = poll_seconds
        self.sleep = sleep
        self.monotonic = monotonic
        self.terminal_evidence_cache: dict[int, tuple[dict, dict]] = {}
        self.receipt = {
            "schemaVersion": 2,
            "definitionId": client.pipeline_id,
            "runKind": run_kind,
            "campaignId": campaign_id,
            "parentBuildId": parent_build_id,
            "root": root,
            "attempt": attempt,
            "sourceRef": source_ref,
            "sourceVersion": source_version,
            "currentRunId": current_run_id,
            "status": "observing",
            "admitted": False,
            "startedAt": utc_now(),
        }

    def write_receipt(self) -> None:
        self.receipt["updatedAt"] = utc_now()
        canonical = dict(self.receipt)
        canonical.pop("canonicalSha256", None)
        self.receipt["canonicalSha256"] = sha256_bytes(
            canonical_json_bytes(canonical)
        )
        atomic_write_json(self.receipt_path, self.receipt)

    def run(self) -> bool:
        self.output_directory.mkdir(parents=True, exist_ok=True)
        deadline = self.monotonic() + self.observation_seconds
        while True:
            matches = hydrated_matching_runs(
                self.client,
                run_kind=self.run_kind,
                campaign_id=self.campaign_id,
                parent_build_id=self.parent_build_id,
                root=self.root,
                source_ref=self.source_ref,
                source_version=self.source_version,
                attempt=self.attempt,
            )
            eligible = []
            rejected = []
            evidence_by_run_id = {}
            completed_pending_ids = set()
            deadline_reached = None
            for run in matches:
                run_id = int(run["id"])
                if run.get("state") == "completed":
                    cached = self.terminal_evidence_cache.get(run_id)
                    if cached is None or not cached[0]["valid"]:
                        evidence_result = terminal_candidate_evidence(
                            self.client,
                            run_id=run_id,
                            run_kind=self.run_kind,
                            campaign_id=self.campaign_id,
                            parent_build_id=self.parent_build_id,
                            root=self.root,
                            attempt=self.attempt,
                            source_ref=self.source_ref,
                            source_version=self.source_version,
                        )
                        if evidence_result[0]["valid"]:
                            self.terminal_evidence_cache[run_id] = (
                                evidence_result
                            )
                        evidence = evidence_result[0]
                    else:
                        evidence = cached[0]
                    if evidence.get("pending") is True:
                        completed_pending_ids.add(run_id)
                        if deadline_reached is None:
                            deadline_reached = self.monotonic() >= deadline
                        if deadline_reached:
                            completed_pending_ids.remove(run_id)
                            evidence = expire_pending_terminal_evidence(
                                evidence,
                                "terminal timeline or admission artifact "
                                "remained incomplete through the admission "
                                "observation window",
                            )
                else:
                    evidence = active_candidate_evidence(
                        self.client, run_id
                    )
                evidence_by_run_id[run_id] = evidence
                if evidence["valid"] or evidence.get("pending") is True:
                    eligible.append(run)
                else:
                    rejected.append(
                        {
                            "runId": run_id,
                            "state": run.get("state"),
                            "result": run.get("result"),
                            "nativeEvidence": evidence,
                        }
                    )
            active = [
                run for run in eligible if run.get("state") != "completed"
            ]
            matching_ids = sorted(int(run["id"]) for run in matches)
            eligible_ids = sorted(int(run["id"]) for run in eligible)
            active_ids = sorted(int(run["id"]) for run in active)
            self.receipt["matchingRunIds"] = matching_ids
            self.receipt["eligibleRunIds"] = eligible_ids
            self.receipt["activeRunIds"] = active_ids
            self.receipt["rejectedCandidates"] = rejected
            if self.current_run_id in eligible_ids:
                canonical_run_id = eligible_ids[0]
                self.receipt["canonicalRunId"] = canonical_run_id
                if self.current_run_id != canonical_run_id:
                    if canonical_run_id in completed_pending_ids:
                        self.write_receipt()
                        self.sleep(self.poll_seconds)
                        continue
                    self.receipt["status"] = "rejectedDuplicate"
                    self.receipt["finishedAt"] = utc_now()
                    self.write_receipt()
                    return False
                if deadline_reached is None:
                    deadline_reached = self.monotonic() >= deadline
                if (
                    self.current_run_id in active_ids
                    and evidence_by_run_id[self.current_run_id]["valid"]
                    and deadline_reached
                ):
                    self.receipt["status"] = "admitted"
                    self.receipt["admitted"] = True
                    self.receipt["finishedAt"] = utc_now()
                    self.write_receipt()
                    return True
                if (
                    deadline_reached
                    and (
                        self.current_run_id not in active_ids
                        or not evidence_by_run_id[self.current_run_id]["valid"]
                    )
                ):
                    raise OrchestrationError(
                        f"Current run {self.current_run_id} did not provide "
                        "valid active admission evidence."
                    )
            elif self.monotonic() >= deadline:
                raise OrchestrationError(
                    f"Current run {self.current_run_id} did not appear as an "
                    f"active matching {self.root} attempt {self.attempt} run."
                )
            self.write_receipt()
            self.sleep(self.poll_seconds)


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


def validate_shard_identity(
    *,
    run_kind: str,
    campaign_id: str,
    parent_build_id: str,
    root: str | None,
    attempt: int | None,
    current_run_id: int | None = None,
) -> None:
    if run_kind not in RUN_KINDS:
        raise OrchestrationError(
            f"Run kind must be one of {', '.join(RUN_KINDS)}."
        )
    if not campaign_id:
        raise OrchestrationError(
            "Correctness shard campaign ID must be explicit."
        )
    resolve_campaign_id(campaign_id, parent_build_id)
    if root not in DIRECT_ROOTS:
        raise OrchestrationError("Correctness shard root is missing or invalid.")
    if attempt not in (1, 2):
        raise OrchestrationError("Correctness shard attempt must be 1 or 2.")
    if run_kind == RUN_KIND_DIRECT:
        if parent_build_id != RUN_KIND_DIRECT:
            raise OrchestrationError(
                "Direct correctness shards must use parent build ID 'direct'."
            )
        if attempt != 1:
            raise OrchestrationError(
                "Direct correctness shards permit attempt 1 only."
            )
        return
    if root not in ROOTS:
        raise OrchestrationError(
            "Parent-child correctness shards must use a parent-orchestrated root."
        )
    if not parent_build_id.isdigit() or int(parent_build_id) <= 0:
        raise OrchestrationError(
            "Parent-child correctness shards require a positive numeric "
            "parent build ID."
        )
    if current_run_id is not None and int(parent_build_id) >= current_run_id:
        raise OrchestrationError(
            "Parent-child correctness shard parent build ID must precede "
            "the child run ID."
        )


def emit_run_identity_tags(
    *,
    run_kind: str,
    campaign_id: str,
    parent_build_id: str,
    root: str,
    attempt: int,
) -> None:
    validate_shard_identity(
        run_kind=run_kind,
        campaign_id=campaign_id,
        parent_build_id=parent_build_id,
        root=root,
        attempt=attempt,
    )
    for tag in identity_tags(
        run_kind=run_kind,
        campaign_id=campaign_id,
        parent_build_id=parent_build_id,
        root=root,
        attempt=attempt,
    ):
        print(f"##vso[build.addbuildtag]{tag}")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Queue and monitor definition 163 GC validation shard runs."
    )
    parser.add_argument("--output-directory", type=Path)
    parser.add_argument("--campaign-id", default="")
    parser.add_argument("--parent-build-id", default="")
    parser.add_argument("--run-kind", choices=RUN_KINDS)
    parser.add_argument("--monitor-timeout-minutes", type=int, default=1380)
    parser.add_argument("--poll-seconds", type=int, default=30)
    parser.add_argument("--adoption-timeout-seconds", type=int, default=900)
    parser.add_argument("--admit-child", action="store_true")
    parser.add_argument("--emit-run-identity-tags", action="store_true")
    parser.add_argument("--root", choices=DIRECT_ROOTS)
    parser.add_argument("--attempt", type=int, choices=(1, 2))
    parser.add_argument("--admission-observation-seconds", type=int, default=60)
    parser.add_argument("--admission-poll-seconds", type=int, default=5)
    args = parser.parse_args()

    if args.emit_run_identity_tags:
        if args.admit_child:
            parser.error(
                "--emit-run-identity-tags and --admit-child are mutually exclusive."
            )
        try:
            emit_run_identity_tags(
                run_kind=args.run_kind or "",
                campaign_id=args.campaign_id,
                parent_build_id=args.parent_build_id,
                root=args.root or "",
                attempt=args.attempt or 0,
            )
            return 0
        except Exception as error:
            print(
                f"GC validation identity publication failed: {error}",
                file=sys.stderr,
            )
            return 1

    if args.output_directory is None:
        parser.error("--output-directory is required unless publishing identity tags.")
    output_directory = args.output_directory
    output_directory.mkdir(parents=True, exist_ok=True)
    fallback_receipt = output_directory / (
        "gc-validation-admission.json"
        if args.admit_child
        else "gc-validation-children.json"
    )
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
        client = AzureDevOpsClient(
            collection_uri,
            project_id,
            definition_id,
            access_token,
        )
        if args.admit_child:
            if (
                args.run_kind is None
                or args.root is None
                or args.attempt is None
            ):
                raise OrchestrationError(
                    "--run-kind, --root, and --attempt are required with "
                    "--admit-child."
                )
            parent_build_id = args.parent_build_id
            campaign_id = args.campaign_id
            validate_shard_identity(
                run_kind=args.run_kind,
                campaign_id=campaign_id,
                parent_build_id=parent_build_id,
                root=args.root,
                attempt=args.attempt,
                current_run_id=int(build_id),
            )
            if args.admission_observation_seconds < 0:
                raise OrchestrationError(
                    "Admission observation seconds cannot be negative."
                )
            if args.admission_poll_seconds <= 0:
                raise OrchestrationError(
                    "Admission poll seconds must be positive."
                )
            admission = GcValidationAdmission(
                client,
                output_directory,
                args.run_kind,
                campaign_id,
                parent_build_id,
                args.root,
                args.attempt,
                source_ref,
                source_version,
                int(build_id),
                args.admission_observation_seconds,
                args.admission_poll_seconds,
            )
            return 0 if admission.run() else 1

        parent_build_id = args.parent_build_id or build_id
        if not parent_build_id.isdigit():
            raise OrchestrationError("Parent build ID must be numeric.")
        campaign_id = resolve_campaign_id(args.campaign_id, parent_build_id)
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
                "schemaVersion": 2,
                "status": "failedBeforeInitialization",
                "error": str(error),
                "finishedAt": utc_now(),
            }
            if args.admit_child:
                receipt["admitted"] = False
            else:
                receipt.update(
                    {
                        "runsQueued": 0,
                        "allTerminalReceipts": False,
                        "succeeded": False,
                    }
                )
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
            if args.admit_child:
                receipt["admitted"] = False
            else:
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
