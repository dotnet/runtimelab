import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import uuid
import zipfile

import yaml


REPO_ROOT = Path(__file__).resolve().parents[4]
PIPELINE_PATH = REPO_ROOT / "eng" / "pipelines" / "runtimelab-official.yml"
PRODUCER_TEMPLATE_PATH = (
    REPO_ROOT
    / "eng"
    / "pipelines"
    / "runtimelab"
    / "standard-gc-artifact-producer-jobs.yml"
)
MANIFEST_JOB_PATH = (
    REPO_ROOT / "eng" / "pipelines" / "runtimelab" / "standard-gc-manifest-job.yml"
)
REQUIREMENTS_PATH = (
    REPO_ROOT
    / "eng"
    / "pipelines"
    / "runtimelab"
    / "standard-gc-artifact-requirements-v2.json"
)
GENERATOR_PATH = (
    REPO_ROOT
    / "eng"
    / "pipelines"
    / "runtimelab"
    / "generate_standard_gc_manifest.py"
)
LAYOUT_GENERATOR_PATH = (
    REPO_ROOT / "eng" / "pipelines" / "runtimelab" / "create_dotnet_layout.py"
)
BASE_SHA = "5ffec89f0944c7ca7001bd3c57713f4fa0fe9492"


def _load_module(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


manifest_generator = _load_module(GENERATOR_PATH, "standard_gc_manifest")
layout_generator = _load_module(LAYOUT_GENERATOR_PATH, "standard_gc_layout")


def _load_yaml_text(text: str):
    return yaml.safe_load(text)


def _load_yaml(path: Path):
    return _load_yaml_text(path.read_text(encoding="utf-8"))


def _parameters(document):
    return {item["name"]: item for item in document["parameters"]}


def _normalize_default_graph(value):
    if isinstance(value, list):
        result = []
        for item in value:
            if isinstance(item, dict) and len(item) == 1:
                key = next(iter(item))
                if (
                    key.startswith("${{ if ")
                    and "standardGcArtifactProducer" in key
                ):
                    continue
            normalized = _normalize_default_graph(item)
            if normalized is not None:
                result.append(normalized)
        return result
    if isinstance(value, dict):
        result = {}
        for key, item in value.items():
            if key.startswith("${{ if ") and "standardGcArtifactProducer" in key:
                continue
            normalized = _normalize_default_graph(item)
            if key == "templateContext" and normalized == {"outputs": []}:
                continue
            if normalized not in ({}, []) or key != "postBuildSteps":
                result[key] = normalized
        return result
    return value


def _write_package(path: Path, package_id: str, version: str, files=None):
    files = files or {}
    nuspec = (
        '<?xml version="1.0"?>'
        '<package><metadata>'
        f"<id>{package_id}</id><version>{version}</version>"
        "</metadata></package>"
    )
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        for name, content in files.items():
            archive.writestr(name, content)


class StandardGcArtifactProducerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pipeline = _load_yaml(PIPELINE_PATH)
        cls.requirements = json.loads(REQUIREMENTS_PATH.read_text(encoding="utf-8"))

    def test_yaml_files_parse(self):
        for path in (
            PIPELINE_PATH,
            PRODUCER_TEMPLATE_PATH,
            MANIFEST_JOB_PATH,
            REPO_ROOT
            / "eng"
            / "pipelines"
            / "runtimelab"
            / "create-dotnet-layout-step.yml",
            REPO_ROOT
            / "eng"
            / "pipelines"
            / "performance"
            / "templates"
            / "build-perf-sample-apps.yml",
            REPO_ROOT
            / "eng"
            / "pipelines"
            / "performance"
            / "templates"
            / "perf-wasm-prepare-artifacts-steps.yml",
        ):
            self.assertIsNotNone(_load_yaml(path), path)

    def test_default_mode_preserves_exact_baseline_graph(self):
        baseline_text = subprocess.run(
            ["git", "show", f"{BASE_SHA}:eng/pipelines/runtimelab-official.yml"],
            cwd=REPO_ROOT,
            check=True,
            capture_output=True,
            text=True,
        ).stdout
        baseline = _load_yaml_text(baseline_text)
        current = json.loads(json.dumps(self.pipeline))
        producer_parameters = {
            "standardGcArtifactProducer",
            "standardGcCampaignId",
            "standardGcCohortId",
            "externalRuntimeAspNetValidationBinding",
        }
        current["parameters"] = [
            item for item in current["parameters"] if item["name"] not in producer_parameters
        ]
        self.assertEqual(baseline, _normalize_default_graph(current))

    def test_mode_is_closed_and_default_off(self):
        parameters = _parameters(self.pipeline)
        self.assertEqual("none", parameters["standardGcArtifactProducer"]["default"])
        self.assertEqual(
            ["none", "full"], parameters["standardGcArtifactProducer"]["values"]
        )
        self.assertEqual("", parameters["standardGcCampaignId"]["default"])
        self.assertEqual("", parameters["standardGcCohortId"]["default"])
        pipeline_text = PIPELINE_PATH.read_text(encoding="utf-8")
        self.assertIn("standardGcArtifactProducerRequiresManualInternal", pipeline_text)
        self.assertIn("standardGcArtifactProducerRequiresCampaignId", pipeline_text)
        self.assertIn("standardGcArtifactProducerRequiresCohortId", pipeline_text)

    def test_frozen_requirements_cover_exact_v2_contract(self):
        self.assertEqual(197, self.requirements["requiredRowCount"])
        self.assertEqual(197, len(self.requirements["downstreamRowMappings"]))
        self.assertEqual(
            "d65c179aacba063fea6a7bf1d62c1ff7499320f979bb161dad09c25da0442aba",
            self.requirements["contractFileSha256"],
        )
        self.assertEqual(
            "52fe6234b9d3223ed51da4ad107a58ed55c27fa837f11e6d17ef8554625b6f7b",
            self.requirements["contractCanonicalPayloadSha256"],
        )
        self.assertEqual(
            "e49b7ec1f45de576ae709ca25ef7715012ec25fdd425ef93633dce5b2d29245f",
            self.requirements["coverageRowsSha256"],
        )
        self.assertEqual(
            manifest_generator.REQUIREMENTS_FILE_SHA256,
            hashlib.sha256(REQUIREMENTS_PATH.read_bytes()).hexdigest(),
        )
        self.assertEqual(
            5,
            len(
                self.requirements["producerRequirements"][
                    "runtimePackagesAndSymbols"
                ]
            ),
        )
        self.assertEqual(
            12,
            len(
                self.requirements["producerRequirements"][
                    "pipelineArtifactArchives"
                ]
            ),
        )
        self.assertEqual(
            2,
            len(
                self.requirements["producerRequirements"][
                    "definition306DotnetLayouts"
                ]
            ),
        )

    def test_official_outputs_cover_every_native_archive(self):
        authored = (
            PIPELINE_PATH.read_text(encoding="utf-8")
            + PRODUCER_TEMPLATE_PATH.read_text(encoding="utf-8")
        )
        for requirement in self.requirements["producerRequirements"][
            "pipelineArtifactArchives"
        ]:
            artifact_name = requirement["artifactName"]
            if artifact_name.startswith("BuildArtifacts_") and artifact_name.endswith(
                "_Release_coreclr"
            ):
                self.assertIn(
                    "BuildArtifacts_$(osGroup)$(osSubgroup)_$(archType)_$(_BuildConfig)_coreclr",
                    authored,
                )
            elif artifact_name.endswith("_coreclr_r2r_interpreter"):
                self.assertIn(
                    "BuildArtifacts_$(osGroup)$(osSubgroup)_$(archType)_$(_BuildConfig)_coreclr_r2r_interpreter",
                    authored,
                )
            else:
                self.assertIn(artifact_name, authored)
        for requirement in self.requirements["producerRequirements"][
            "definition306DotnetLayouts"
        ]:
            self.assertIn(requirement["artifact"]["name"], authored)
        self.assertNotIn("PublishPipelineArtifact@1", PRODUCER_TEMPLATE_PATH.read_text())
        self.assertIn("templateContext:", authored)
        self.assertIn("outputs:", authored)

    def test_definition163_orchestration_is_not_changed(self):
        changed = subprocess.run(
            ["git", "diff", "--name-only", BASE_SHA, "--"],
            cwd=REPO_ROOT,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.splitlines()
        self.assertFalse(
            any(
                path == "eng/pipelines/runtimelab.yml"
                or "gc-validation" in path
                or path.endswith("orchestrate_gc_validation.py")
                for path in changed
            ),
            changed,
        )

    def _create_complete_artifact_fixture(self, root: Path):
        version = "12.0.0-test.1"
        for requirement in self.requirements["producerRequirements"][
            "runtimePackagesAndSymbols"
        ]:
            package_id = requirement["package"]["id"]
            runtime_files = {
                name: f"{requirement['rid']}:{name}".encode("utf-8")
                for name in requirement["measuredRuntimeFileSha256"]["requiredFiles"]
            }
            _write_package(root / f"{package_id}.{version}.nupkg", package_id, version, runtime_files)
            _write_package(
                root / f"{package_id}.{version}.symbols.nupkg",
                package_id,
                version,
                {"symbols.txt": b"symbols"},
            )
        for requirement in self.requirements["producerRequirements"][
            "pipelineArtifactArchives"
        ]:
            (root / requirement["fileName"]).write_bytes(
                requirement["artifactName"].encode("utf-8")
            )
        for requirement in self.requirements["producerRequirements"][
            "definition306DotnetLayouts"
        ]:
            (root / requirement["artifact"]["fileName"]).write_bytes(
                requirement["id"].encode("utf-8")
            )

    def _manifest_args(self, root: Path, output: Path):
        return argparse.Namespace(
            requirements=REQUIREMENTS_PATH,
            artifact_root=root,
            output_root=output,
            source_repository="dotnet/runtimelab",
            source_branch="refs/heads/feature/gc/baseline",
            source_commit="a" * 40,
            producer_definition_id=895,
            producer_build_id=12345,
            campaign_id=str(uuid.UUID("daeaf7a1-2d3c-59e7-a1bc-1d5c0630fd4a")),
            cohort_id="cohort",
            coverage_rows_sha256=self.requirements["coverageRowsSha256"],
            aspnet_validation_binding=None,
        )

    def test_manifest_and_definition306_receipts_are_exact_and_hashed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "artifacts"
            output = Path(directory) / "output"
            root.mkdir()
            self._create_complete_artifact_fixture(root)
            manifest, receipt, errors = manifest_generator.generate(
                self._manifest_args(root, output)
            )

            self.assertEqual([], errors)
            self.assertEqual(197, len(manifest["rows"]))
            self.assertEqual(
                {"a" * 40}, {row["sourceCommit"] for row in manifest["rows"]}
            )
            self.assertEqual(15, receipt["rowCount"])
            self.assertEqual(15, len(receipt["rows"]))
            self.assertEqual(
                {"passed"}, {row["status"] for row in receipt["rows"]}
            )
            for row in receipt["rows"]:
                self.assertRegex(row["compatibilityProofSha256"], r"^[0-9a-f]{64}$")
                proof = output / "ExternalRuntime306Validation" / row[
                    "rowCompatibilityProof"
                ]
                self.assertTrue(proof.is_file())
                self.assertEqual(
                    row["compatibilityProofSha256"],
                    hashlib.sha256(proof.read_bytes()).hexdigest(),
                )
            manifest_path = output / "standard-gc-cohort-manifest.json"
            self.assertEqual(
                hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
                receipt["cohortManifestSha256"],
            )
            self.assertEqual(
                85,
                sum(row["status"] == "blocked" for row in manifest["rows"]),
            )

    def test_missing_artifact_writes_blocked_rows_and_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "artifacts"
            output = Path(directory) / "output"
            root.mkdir()
            self._create_complete_artifact_fixture(root)
            missing = next(root.glob("BuildArtifacts_linux_x64*"))
            missing.unlink()
            _, _, errors = manifest_generator.generate(
                self._manifest_args(root, output)
            )
            self.assertTrue(errors)
            manifest = json.loads(
                (output / "standard-gc-cohort-manifest.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertEqual(197, len(manifest["rows"]))
            self.assertTrue(manifest["errors"])
            self.assertTrue(
                any(
                    requirement["status"] == "blocked-missing-artifact"
                    for row in manifest["rows"]
                    for requirement in row["requirements"]
                )
            )

    def test_duplicate_or_unknown_contract_mapping_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            requirements = json.loads(REQUIREMENTS_PATH.read_text(encoding="utf-8"))
            requirements["downstreamRowMappings"][1]["rowId"] = requirements[
                "downstreamRowMappings"
            ][0]["rowId"]
            duplicate = root / "duplicate.json"
            duplicate.write_text(json.dumps(requirements), encoding="utf-8")
            args = self._manifest_args(root, root / "output")
            args.requirements = duplicate
            with self.assertRaisesRegex(ValueError, "authority hash"):
                manifest_generator.generate(args)

            requirements = json.loads(REQUIREMENTS_PATH.read_text(encoding="utf-8"))
            requirements["downstreamRowMappings"][0]["requirementIds"].append(
                "unknown-requirement"
            )
            unknown = root / "unknown.json"
            unknown.write_text(json.dumps(requirements), encoding="utf-8")
            args.requirements = unknown
            with self.assertRaisesRegex(ValueError, "authority hash"):
                manifest_generator.generate(args)

    def test_aspnet_binding_boundary_is_explicitly_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            binding = root / "aspnet.json"
            binding.write_text('{"rows":[]}', encoding="utf-8")
            args = self._manifest_args(root, root / "output")
            args.aspnet_validation_binding = binding
            with self.assertRaisesRegex(ValueError, "intentionally unbound"):
                manifest_generator.generate(args)

    def test_dotnet_layout_requires_runtime_package_byte_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "artifacts" / "bin" / "testhost" / "net11.0-windows-Release-x64"
            framework = source / "shared" / "Microsoft.NETCore.App" / "12.0.0"
            fxr = source / "host" / "fxr" / "12.0.0"
            framework.mkdir(parents=True)
            fxr.mkdir(parents=True)
            (source / "dotnet.exe").write_bytes(b"host")
            (fxr / "hostfxr.dll").write_bytes(b"fxr")
            package_root = root / "packages"
            package_root.mkdir()
            files = {
                "runtimes/win-x64/lib/net11.0/System.Private.CoreLib.dll": b"corelib",
                "runtimes/win-x64/native/coreclr.dll": b"coreclr",
                "runtimes/win-x64/native/clrgc.dll": b"clrgc",
            }
            for package_path, content in files.items():
                (framework / Path(package_path).name).write_bytes(content)
            _write_package(
                package_root / "runtime.nupkg",
                "Microsoft.NETCore.App.Runtime.win-x64",
                "12.0.0",
                files,
            )
            output = root / "layout"
            manifest = layout_generator.create_layout(
                root / "artifacts" / "bin", package_root, output, "win-x64"
            )
            self.assertEqual("win-x64", manifest["rid"])
            self.assertTrue((output / "external-runtime-layout.json").is_file())


if __name__ == "__main__":
    unittest.main()
