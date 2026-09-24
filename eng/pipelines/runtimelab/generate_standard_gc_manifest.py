#!/usr/bin/env python3

import argparse
import hashlib
import json
from pathlib import Path
import re
from typing import Dict, Iterable, List, Optional, Tuple
import uuid
import xml.etree.ElementTree as ET
import zipfile


SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")
REQUIREMENTS_FILE_SHA256 = (
    "d8eb7c11b408f3e5c3f3883e276fb9c270f5a129edf816c43ec87bc2e619366e"
)
ASPNET_BINDING_REPORTED_FILE_SHA256 = (
    "8e13df1a77f3797e67d601ad87fa58c3e3ff5f8d925c84284d576161fc08e746"
)
ASPNET_BINDING_CANONICAL_SHA256 = (
    "20e5f054b149bb2f6b69a371322e5063ba4c024f2f8e34f070919998efd7e7f5"
)
ASPNET_BINDING_ROW_IDS_SHA256 = (
    "6c8b97a75234d2e2ba73e1c1898dcf789dbb306352ded1e03647a9468c2d85c6"
)
ASPNET_BINDING_TOKEN_MAP_SHA256 = (
    "c7875c7efc0d48336336dd8027b808c97dfb5726d913d02422cef707fad90efe"
)
ASPNET_BINDING_SOURCE_COMMIT = "40545f37bf4d3d5c81d79b8c33ce8381080c8b07"


def _canonical_bytes(value: object) -> bytes:
    return json.dumps(value, separators=(",", ":"), sort_keys=True).encode("utf-8")


def _canonical_sha256(value: object) -> str:
    return hashlib.sha256(_canonical_bytes(value)).hexdigest()


def _hash_file(path: Path, algorithm: str) -> str:
    digest = hashlib.new(algorithm)
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _write_json(path: Path, value: object) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(_canonical_bytes(value))
    digest = _hash_file(path, "sha256")
    path.with_suffix(path.suffix + ".sha256").write_text(digest + "\n", encoding="utf-8")
    return digest


def _package_identity(path: Path) -> Tuple[str, str]:
    with zipfile.ZipFile(path) as archive:
        nuspecs = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(nuspecs) != 1:
            raise ValueError(f"expected one nuspec in {path}, found {len(nuspecs)}")
        root = ET.fromstring(archive.read(nuspecs[0]))
    values = {}
    for element in root.iter():
        name = element.tag.rsplit("}", 1)[-1]
        if name in ("id", "version") and element.text:
            values[name] = element.text.strip()
    if set(values) != {"id", "version"}:
        raise ValueError(f"package identity is incomplete in {path}")
    return values["id"], values["version"]


def _find_exact_file(root: Path, file_name: str) -> Path:
    matches = [path for path in root.rglob(file_name) if path.is_file()]
    if len(matches) != 1:
        raise ValueError(
            f"expected exactly one {file_name} under {root}, found {len(matches)}"
        )
    return matches[0]


def _find_package(root: Path, package_id: str, symbols: bool) -> Path:
    matches = []
    for path in root.rglob("*.nupkg"):
        is_symbols = ".symbols." in path.name
        if is_symbols != symbols:
            continue
        identity, _ = _package_identity(path)
        if identity == package_id:
            matches.append(path)
    if len(matches) != 1:
        kind = "symbols" if symbols else "runtime"
        raise ValueError(
            f"expected exactly one {kind} package {package_id}, found {len(matches)}"
        )
    return matches[0]


def _runtime_file_hashes(package: Path, required_files: Iterable[str]) -> Dict[str, str]:
    with zipfile.ZipFile(package) as archive:
        names = set(archive.namelist())
        missing = [name for name in required_files if name not in names]
        if missing:
            raise ValueError(f"{package.name} is missing runtime files: {missing}")
        return {
            name: hashlib.sha256(archive.read(name)).hexdigest()
            for name in required_files
        }


def _artifact_evidence(path: Path, kind: str, identity: Dict) -> Dict:
    evidence = {
        "kind": kind,
        "fileName": path.name,
        "sha256": _hash_file(path, "sha256"),
        "sha512": _hash_file(path, "sha512"),
        "size": path.stat().st_size,
    }
    evidence.update(identity)
    return evidence


def _collect_artifacts(
    requirements: Dict, artifact_root: Path, required_ids: set[str]
) -> Tuple[Dict, List[str]]:
    evidence = {}
    errors = []

    for requirement in requirements["producerRequirements"]["runtimePackagesAndSymbols"]:
        requirement_id = requirement["id"]
        try:
            runtime = _find_package(artifact_root, requirement["package"]["id"], False)
            symbols = _find_package(artifact_root, requirement["package"]["id"], True)
            runtime_id, version = _package_identity(runtime)
            symbol_id, symbol_version = _package_identity(symbols)
            if runtime_id != requirement["package"]["id"] or symbol_id != runtime_id:
                raise ValueError(f"runtime and symbol package identity mismatch for {requirement_id}")
            if version != symbol_version:
                raise ValueError(f"runtime and symbol package version mismatch for {requirement_id}")
            evidence[requirement_id] = {
                "kind": requirement["kind"],
                "rid": requirement["rid"],
                "configuration": requirement["configuration"],
                "runtimeFlavor": requirement["runtimeFlavor"],
                "package": _artifact_evidence(
                    runtime, "runtimeNuGet", {"id": runtime_id, "version": version}
                ),
                "symbols": _artifact_evidence(
                    symbols, "symbolNuGet", {"id": symbol_id, "version": symbol_version}
                ),
                "measuredRuntimeFileSha256": _runtime_file_hashes(
                    runtime, requirement["measuredRuntimeFileSha256"]["requiredFiles"]
                ),
            }
        except (OSError, ValueError, zipfile.BadZipFile) as error:
            if requirement_id in required_ids:
                errors.append(f"{requirement_id}: {error}")

    for requirement in requirements["producerRequirements"]["pipelineArtifactArchives"]:
        requirement_id = requirement["id"]
        try:
            path = _find_exact_file(artifact_root, requirement["fileName"])
            evidence[requirement_id] = _artifact_evidence(
                path,
                requirement["kind"],
                {
                    "artifactName": requirement["artifactName"],
                    "rid": requirement["rid"],
                    "configuration": requirement["configuration"],
                    "runtimeFlavor": requirement["runtimeFlavor"],
                    "consumerVariants": requirement["consumerVariants"],
                },
            )
        except (OSError, ValueError) as error:
            if requirement_id in required_ids:
                errors.append(f"{requirement_id}: {error}")

    for requirement in requirements["producerRequirements"]["definition306DotnetLayouts"]:
        requirement_id = requirement["id"]
        try:
            path = _find_exact_file(artifact_root, requirement["artifact"]["fileName"])
            evidence[requirement_id] = _artifact_evidence(
                path,
                requirement["kind"],
                {
                    "artifactName": requirement["artifact"]["name"],
                    "rid": requirement["rid"],
                    "configuration": requirement["configuration"],
                    "runtimeFlavor": requirement["runtimeFlavor"],
                    "compatibleChannels": requirement["compatibleChannels"],
                },
            )
        except (OSError, ValueError) as error:
            if requirement_id in required_ids:
                errors.append(f"{requirement_id}: {error}")

    return evidence, errors


def _validate_identity(args: argparse.Namespace) -> None:
    if args.producer_definition_id != 895:
        raise ValueError("producer definition identity must be 895")
    if args.producer_build_id < 1:
        raise ValueError("producer build identity must be a positive integer")
    if args.source_repository != "dotnet/runtimelab":
        raise ValueError("source repository identity must be dotnet/runtimelab")
    if args.source_branch != "refs/heads/feature/gc/baseline":
        raise ValueError("source branch identity must be refs/heads/feature/gc/baseline")
    if not SHA_PATTERN.fullmatch(args.source_commit):
        raise ValueError("source commit must be a lowercase 40-character Git SHA")
    for name in ("campaign_id", "cohort_id", "source_repository", "source_branch"):
        if not getattr(args, name).strip():
            raise ValueError(f"{name.replace('_', '-')} must not be empty")
    try:
        uuid.UUID(args.campaign_id)
    except ValueError as error:
        raise ValueError("campaign identity must be a UUID") from error


def _validate_aspnet_binding(path: Path) -> Dict:
    binding = json.loads(path.read_text(encoding="utf-8"))
    if _canonical_sha256(binding) != ASPNET_BINDING_CANONICAL_SHA256:
        raise ValueError("ASP.NET validation binding authority hash does not match")
    if binding.get("schemaVersion") != 1:
        raise ValueError("ASP.NET validation binding schema must be version 1")
    if binding.get("coverageRowsSha256") != (
        "e49b7ec1f45de576ae709ca25ef7715012ec25fdd425ef93633dce5b2d29245f"
    ):
        raise ValueError("ASP.NET validation binding coverage identity does not match")

    rows = binding.get("rows")
    if not isinstance(rows, list) or len(rows) != 76:
        raise ValueError("ASP.NET validation binding must contain exactly 76 rows")
    row_ids = [row.get("rowId") for row in rows]
    if len(set(row_ids)) != 76 or _canonical_sha256(sorted(row_ids)) != (
        ASPNET_BINDING_ROW_IDS_SHA256
    ):
        raise ValueError("ASP.NET validation binding row identity does not match")
    definition_counts = {
        definition_id: sum(row.get("definitionId") == definition_id for row in rows)
        for definition_id in (1208, 1209, 1505)
    }
    if definition_counts != {1208: 2, 1209: 26, 1505: 48}:
        raise ValueError("ASP.NET validation binding definition row counts do not match")

    token_map = {row.get("scalarToken"): row.get("rowId") for row in rows}
    if len(token_map) != 76 or _canonical_sha256(token_map) != (
        ASPNET_BINDING_TOKEN_MAP_SHA256
    ):
        raise ValueError("ASP.NET validation binding scalar token map does not match")
    for row in rows:
        row_id = row["rowId"]
        if row["scalarToken"] != "r" + hashlib.sha256(row_id.encode()).hexdigest()[:16]:
            raise ValueError(f"ASP.NET validation binding scalar token mismatch: {row_id}")
        for field in ("rowProjectionSha256", "selectorId", "profileId"):
            if not row.get(field):
                raise ValueError(f"ASP.NET validation binding row is missing {field}: {row_id}")

    selectors = binding.get("selectors")
    if not isinstance(selectors, dict) or set(selectors) != {
        "linux-x64-coreclr-release-runtime-pack",
        "win-x64-coreclr-release-runtime-pack",
        "linux-arm64-coreclr-release-runtime-pack",
    }:
        raise ValueError("ASP.NET validation binding selector identities do not match")
    profiles = binding.get("transportProfiles")
    if not isinstance(profiles, dict) or len(profiles) != 7:
        raise ValueError("ASP.NET validation binding must contain exactly seven profiles")
    if {row["profileId"] for row in rows} != set(profiles):
        raise ValueError("ASP.NET validation binding profile row mapping does not match")
    for profile_id, scope in profiles.items():
        if profile_id != "p" + _canonical_sha256(scope)[:16]:
            raise ValueError(f"ASP.NET validation binding profile identity mismatch: {profile_id}")
    return {
        "reportedFileSha256": ASPNET_BINDING_REPORTED_FILE_SHA256,
        "canonicalSha256": ASPNET_BINDING_CANONICAL_SHA256,
        "sourceCommit": ASPNET_BINDING_SOURCE_COMMIT,
        "rowCount": 76,
        "rowIdsSha256": ASPNET_BINDING_ROW_IDS_SHA256,
        "scalarTokenMapSha256": ASPNET_BINDING_TOKEN_MAP_SHA256,
        "profileCount": 7,
        "definitionRowCounts": {str(key): value for key, value in definition_counts.items()},
    }


def _validate_requirements(requirements: Dict) -> None:
    groups = requirements["producerRequirements"]
    requirement_ids = []
    for group_name in (
        "runtimePackagesAndSymbols",
        "pipelineArtifactArchives",
        "sourceBuildRequirements",
        "definition306DotnetLayouts",
        "validationReceipts",
    ):
        requirement_ids.extend(item["id"] for item in groups[group_name])
    if len(requirement_ids) != len(set(requirement_ids)):
        raise ValueError("producer requirements contain duplicate requirement identities")

    rows = requirements["downstreamRowMappings"]
    row_ids = [row["rowId"] for row in rows]
    required_row_count = requirements["requiredRowCount"]
    if len(rows) != required_row_count or len(row_ids) != len(set(row_ids)):
        raise ValueError(
            f"producer requirements must contain {required_row_count} unique downstream rows"
        )
    known_requirements = set(requirement_ids)
    for row in rows:
        if not row["requirementIds"]:
            raise ValueError(f"downstream row has no requirements: {row['rowId']}")
        unknown = set(row["requirementIds"]) - known_requirements
        if unknown:
            raise ValueError(
                f"downstream row {row['rowId']} has unknown requirements: {sorted(unknown)}"
            )


def _row_statuses(
    requirements: Dict,
    evidence: Dict,
    source_commit: str,
    producer_scope: str,
    required_ids: set[str],
) -> List[Dict]:
    rows = []
    for row in requirements["downstreamRowMappings"]:
        statuses = []
        for requirement_id in row["requirementIds"]:
            if requirement_id.startswith("source-build:"):
                status = "blocked-external-definition163"
            elif requirement_id == "validation-receipt:definition306":
                status = (
                    "generated-after-cohort-manifest"
                    if producer_scope == "full"
                    else "blocked-out-of-scope"
                )
            elif requirement_id in evidence:
                status = "ready"
            elif requirement_id not in required_ids:
                status = "blocked-out-of-scope"
            else:
                status = "blocked-missing-artifact"
            statuses.append({"requirementId": requirement_id, "status": status})
        rows.append(
            {
                "rowId": row["rowId"],
                "predecessorRowId": row["predecessorRowId"],
                "definitionId": row["definitionId"],
                "coverageDomain": row["coverageDomain"],
                "rid": row["rid"],
                "sourceCommit": source_commit,
                "consumptionMode": row["consumptionMode"],
                "consumerArtifactIdentity": row.get("consumerArtifactIdentity"),
                "requirements": statuses,
                "status": (
                    "ready"
                    if all(item["status"] in ("ready", "generated-after-cohort-manifest") for item in statuses)
                    else "blocked"
                ),
            }
        )
    return rows


def _definition306_receipt(
    requirements: Dict,
    evidence: Dict,
    identity: Dict,
    cohort_manifest_sha256: str,
    output_root: Path,
) -> Dict:
    receipt_requirement = requirements["producerRequirements"]["validationReceipts"][0]
    mappings = {
        row["rowId"]: row
        for row in requirements["downstreamRowMappings"]
        if row["definitionId"] == 306
    }
    expected_rows = receipt_requirement["consumerRows"]
    if set(mappings) != set(expected_rows) or len(mappings) != 15:
        raise ValueError("definition306 mapping must contain the exact 15 frozen v5 rows")

    proof_root = output_root / "proofs"
    rows = []
    for row_id in expected_rows:
        mapping = mappings[row_id]
        artifact_requirements = [
            requirement_id
            for requirement_id in mapping["requirementIds"]
            if requirement_id != receipt_requirement["id"]
        ]
        if len(artifact_requirements) != 1 or artifact_requirements[0] not in evidence:
            raise ValueError(f"definition306 row has no unique artifact evidence: {row_id}")
        artifact = evidence[artifact_requirements[0]]
        consumer_binding = dict(mapping)
        consumer_binding["sourceCommit"] = identity["sourceCommit"]
        proof = {
            "schemaVersion": 1,
            "rowId": row_id,
            "definitionId": 306,
            "status": "passed",
            "identity": identity,
            "cohortManifestSha256": cohort_manifest_sha256,
            "artifactRequirementId": artifact_requirements[0],
            "artifact": artifact,
            "consumerBinding": consumer_binding,
        }
        proof_sha256 = _canonical_sha256(proof)
        proof_name = hashlib.sha256(row_id.encode("utf-8")).hexdigest() + ".json"
        _write_json(proof_root / proof_name, proof)
        rows.append(
            {
                "rowId": row_id,
                "status": "passed",
                "artifactRequirementId": artifact_requirements[0],
                "rowCompatibilityProof": f"proofs/{proof_name}",
                "compatibilityProofSha256": proof_sha256,
            }
        )

    receipt = {
        "schemaVersion": 1,
        "definitionId": 306,
        "coverageRowsSha256": requirements["coverageAuthority"][
            "contractRowsSha256"
        ],
        "sourceCommit": identity["sourceCommit"],
        "producerDefinitionId": identity["producerDefinitionId"],
        "producerBuildId": identity["producerBuildId"],
        "campaignId": identity["campaignId"],
        "cohortId": identity["cohortId"],
        "cohortManifestSha256": cohort_manifest_sha256,
        "rowCount": 15,
        "rowIdsSha256": receipt_requirement["rowIdsSha256"],
        "rowCompatibilityStatus": "passed",
        "rows": rows,
    }
    _write_json(output_root / receipt_requirement["artifact"]["fileName"], receipt)
    return receipt


def generate(args: argparse.Namespace) -> Tuple[Dict, Optional[Dict], List[str]]:
    _validate_identity(args)
    if _hash_file(args.requirements, "sha256") != REQUIREMENTS_FILE_SHA256:
        raise ValueError("producer requirements authority hash does not match contract v5")
    requirements = json.loads(args.requirements.read_text(encoding="utf-8"))
    coverage_authority = requirements["coverageAuthority"]
    if coverage_authority["contractRowsSha256"] != args.coverage_rows_sha256:
        raise ValueError("coverage contract hash does not match producer requirements")
    _validate_requirements(requirements)
    if args.producer_scope not in requirements["producerScopes"]:
        raise ValueError(f"unsupported producer scope: {args.producer_scope}")
    required_ids = set(
        requirements["producerScopes"][args.producer_scope][
            "requiredArtifactRequirementIds"
        ]
    )

    evidence, errors = _collect_artifacts(requirements, args.artifact_root, required_ids)
    aspnet_validation = {
        "status": "blocked-unbound",
        "reason": "authoritative ExternalRuntimeAspNetValidation binding not supplied",
    }
    if args.aspnet_validation_binding:
        binding = _validate_aspnet_binding(args.aspnet_validation_binding)
        aspnet_validation = {
            "status": "blocked-missing-transport-payloads",
            "binding": binding,
            "reason": (
                "the finalized binding freezes rows and profile scopes but does not contain "
                "the seven candidateRuntimeArgumentsBase/candidateFilesBase payloads required "
                "to generate consumer-verifiable profile and row proof sidecars"
            ),
        }
        errors.append(
            "ExternalRuntimeAspNetValidation: finalized binding is valid, but authoritative "
            "transport payload bases are not supplied"
        )
    identity = {
        "sourceRepository": args.source_repository,
        "sourceBranch": args.source_branch,
        "sourceCommit": args.source_commit,
        "producerDefinitionId": args.producer_definition_id,
        "producerBuildId": args.producer_build_id,
        "campaignId": args.campaign_id,
        "cohortId": args.cohort_id,
    }
    manifest = {
        "schemaVersion": 3,
        "contract": {
            "id": requirements["contractId"],
            "version": requirements["contractVersion"],
            "fileSha256": REQUIREMENTS_FILE_SHA256,
            "coverageContractVersion": coverage_authority["contractVersion"],
            "coverageContractFileSha256": coverage_authority["fileSha256"],
            "coverageRowsSha256": coverage_authority["contractRowsSha256"],
        },
        "identity": identity,
        "producerScope": args.producer_scope,
        "requiredArtifactRequirementIds": sorted(required_ids),
        "artifacts": {key: evidence[key] for key in sorted(evidence)},
        "rows": _row_statuses(
            requirements,
            evidence,
            args.source_commit,
            args.producer_scope,
            required_ids,
        ),
        "aspNetValidation": aspnet_validation,
        "errors": errors,
    }
    manifest_sha256 = _write_json(args.output_root / "standard-gc-cohort-manifest.json", manifest)
    receipt = None
    if args.producer_scope == "full":
        receipt = _definition306_receipt(
            requirements,
            evidence,
            identity,
            manifest_sha256,
            args.output_root / "ExternalRuntime306Validation",
        )
    return manifest, receipt, errors


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Generate the source-bound standard-GC cohort manifest and validation receipts."
    )
    parser.add_argument("--requirements", required=True, type=Path)
    parser.add_argument("--artifact-root", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--source-repository", required=True)
    parser.add_argument("--source-branch", required=True)
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--producer-definition-id", required=True, type=int)
    parser.add_argument("--producer-build-id", required=True, type=int)
    parser.add_argument("--campaign-id", required=True)
    parser.add_argument("--cohort-id", required=True)
    parser.add_argument("--coverage-rows-sha256", required=True)
    parser.add_argument("--producer-scope", required=True, choices=("x64", "full"))
    parser.add_argument("--aspnet-validation-binding", type=Path)
    args = parser.parse_args()
    _, _, errors = generate(args)
    if errors:
        raise SystemExit("standard-GC cohort is incomplete:\n" + "\n".join(errors))


if __name__ == "__main__":
    main()
