import argparse
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

import yaml


REPO_ROOT = Path(__file__).resolve().parents[4]
PIPELINE_PATH = REPO_ROOT / "eng" / "pipelines" / "runtimelab-official.yml"
MANIFEST_TEMPLATE_PATH = (
    REPO_ROOT / "eng" / "pipelines" / "runtimelab" / "publish-cohort-manifest-step.yml"
)
MANIFEST_GENERATOR_PATH = (
    REPO_ROOT / "eng" / "pipelines" / "runtimelab" / "generate_cohort_manifest.py"
)
RUN_TEST_TEMPLATE_PATH = (
    REPO_ROOT
    / "eng"
    / "pipelines"
    / "common"
    / "templates"
    / "runtimes"
    / "run-test-job.yml"
)
REPRESENTATIVE_CONDITION = (
    "and(succeeded(), eq(variables['Build.Reason'], 'Manual'), "
    "eq(variables['System.TeamProject'], 'internal'))"
)


def _load_yaml(path: Path) -> dict:
    return yaml.safe_load(path.read_text(encoding="utf-8"))


def _parameters(pipeline: dict) -> dict:
    return {parameter["name"]: parameter for parameter in pipeline["parameters"]}


def _build_jobs(pipeline: dict) -> list:
    stages = pipeline["extends"]["parameters"]["stages"]
    return next(stage["jobs"] for stage in stages if stage.get("stage") == "Build")


def _condition_matches(condition: str, values: dict) -> bool:
    expression = condition.removeprefix("${{ if ").removesuffix(" }}")
    if expression.startswith("or("):
        arguments = expression[3:-1]
        depth = 0
        for index, character in enumerate(arguments):
            if character == "(":
                depth += 1
            elif character == ")":
                depth -= 1
            elif character == "," and depth == 0:
                return _condition_matches(
                    "${{ if " + arguments[:index].strip() + " }}", values
                ) or _condition_matches(
                    "${{ if " + arguments[index + 1 :].strip() + " }}", values
                )
    for operation in ("eq", "ne"):
        prefix = f"{operation}(parameters."
        if expression.startswith(prefix):
            parameter, expected = expression[len(prefix) : -1].split(",", 1)
            expected = expected.strip().strip("'")
            matches = str(values[parameter]) == expected
            return matches if operation == "eq" else not matches
    prefix = "in(parameters."
    if expression.startswith(prefix):
        parameter, expected = expression[len(prefix) : -1].split(",", 1)
        expected_values = [value.strip().strip("'") for value in expected.split(",")]
        return str(values[parameter]) in expected_values
    raise AssertionError(f"unsupported test expression: {condition}")


def _expand_conditionals(items: list, values: dict) -> list:
    expanded = []
    for item in items:
        if isinstance(item, dict) and len(item) == 1:
            condition = next(iter(item))
            if condition.startswith("${{ if "):
                if _condition_matches(condition, values):
                    expanded.extend(_expand_conditionals(item[condition], values))
                continue
        expanded.append(item)
    return expanded


class GcExperimentPipelineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.pipeline = _load_yaml(PIPELINE_PATH)
        cls.parameters = _parameters(cls.pipeline)

    def test_default_off_preserves_canonical_build_jobs(self) -> None:
        defaults = {
            name: parameter["default"] for name, parameter in self.parameters.items()
        }
        jobs = _expand_conditionals(_build_jobs(self.pipeline), defaults)

        self.assertEqual("none", defaults["gcExperimentMode"])
        self.assertEqual("none", self.pipeline["trigger"])
        self.assertEqual("none", self.pipeline["pr"])
        self.assertNotIn("schedules", self.pipeline)
        self.assertEqual(2, len(jobs))

        runtime_job = jobs[0]["parameters"]
        self.assertEqual(["linux_x64", "windows_x64"], runtime_job["platforms"])
        self.assertEqual(
            "-s clr+libs+host+packs -c $(_BuildConfig) -restore -build -publish "
            "/p:PublishingVersion=4",
            runtime_job["jobParameters"]["buildArgs"],
        )
        self.assertTrue(runtime_job["jobParameters"]["enablePublishing"])
        self.assertEqual(4, runtime_job["jobParameters"]["publishingVersion"])

        libraries_job = jobs[1]["parameters"]["jobParameters"]
        self.assertIn("-pack -publish", libraries_job["buildArgs"])
        self.assertTrue(libraries_job["enablePublishing"])
        self.assertEqual(4, libraries_job["publishingVersion"])

    def test_performance_selector_expands_exactly_one_standard_lane(self) -> None:
        values = {
            "publishToExperimentalFeed": False,
            "gcExperimentMode": "representative",
        }
        jobs = _expand_conditionals(_build_jobs(self.pipeline), values)
        performance_jobs = [
            job
            for job in jobs
            if job.get("parameters", {}).get("jobTemplate")
            == "/eng/pipelines/templates/runtime-perf-job.yml@performance"
        ]

        self.assertEqual(1, len(performance_jobs))
        parameters = performance_jobs[0]["parameters"]
        self.assertEqual(["linux_x64"], parameters["platforms"])
        self.assertEqual("release", parameters["buildConfig"])
        self.assertEqual("coreclr", parameters["runtimeFlavor"])
        self.assertEqual("micro", parameters["jobParameters"]["runKind"])
        self.assertEqual("Runtime", parameters["jobParameters"]["runCategories"])
        self.assertEqual("perfviper", parameters["jobParameters"]["logicalMachine"])
        self.assertEqual("self", parameters["jobParameters"]["runtimeRepoAlias"])
        self.assertEqual(
            "performance", parameters["jobParameters"]["performanceRepoAlias"]
        )
        self.assertEqual(
            REPRESENTATIVE_CONDITION, parameters["jobParameters"]["condition"]
        )

    def test_performance_artifact_task_is_default_off(self) -> None:
        runtime_job = _build_jobs(self.pipeline)[1]["parameters"]["jobParameters"]
        post_build_steps = runtime_job["postBuildSteps"]
        defaults = {
            "gcExperimentMode": "none",
        }
        selected = {
            "gcExperimentMode": "representative",
        }

        default_steps = _expand_conditionals(post_build_steps, defaults)
        selected_steps = _expand_conditionals(post_build_steps, selected)

        self.assertEqual([], default_steps)
        self.assertEqual(
            [
                "/eng/pipelines/common/upload-artifact-step.yml",
                "/eng/pipelines/runtimelab/publish-cohort-manifest-step.yml",
            ],
            [step["template"] for step in selected_steps],
        )
        artifact_parameters = selected_steps[0]["parameters"]
        self.assertEqual(
            "BuildArtifacts_$(osGroup)$(osSubgroup)_$(archType)_$(_BuildConfig)_coreclr",
            artifact_parameters["artifactName"],
        )
        self.assertEqual(
            "and(succeeded(), eq(variables['Build.Reason'], 'Manual'), "
            "eq(variables['System.TeamProject'], 'internal'), "
            "eq(variables['osGroup'], 'linux'))",
            artifact_parameters["condition"],
        )
        libraries_job = _build_jobs(self.pipeline)[2]["parameters"]["jobParameters"]
        self.assertEqual(
            [],
            _expand_conditionals(libraries_job["postBuildSteps"], defaults),
        )

    def test_representative_mode_is_single_and_fail_closed(self) -> None:
        self.assertEqual(
            ["none", "representative"],
            self.parameters["gcExperimentMode"]["values"],
        )
        values = {
            "publishToExperimentalFeed": False,
            "gcExperimentMode": "representative",
        }
        jobs = _expand_conditionals(_build_jobs(self.pipeline), values)
        self.assertEqual(8, len(jobs))
        self.assertEqual(
            REPRESENTATIVE_CONDITION,
            jobs[0]["parameters"]["condition"],
        )
        for job in jobs[3:]:
            self.assertEqual(
                REPRESENTATIVE_CONDITION,
                job["parameters"]["jobParameters"]["condition"],
            )

    def test_gc_normal_is_the_filtered_standard_correctness_lane(self) -> None:
        values = {
            "publishToExperimentalFeed": False,
            "gcExperimentMode": "representative",
        }
        jobs = _expand_conditionals(_build_jobs(self.pipeline), values)
        correctness_jobs = [
            job
            for job in jobs
            if any(
                step.get("template")
                == "/eng/pipelines/common/templates/runtimes/build-runtime-tests-and-send-to-helix.yml"
                for step in job.get("parameters", {})
                .get("jobParameters", {})
                .get("postBuildSteps", [])
            )
        ]

        self.assertEqual(1, len(correctness_jobs))
        parameters = correctness_jobs[0]["parameters"]
        self.assertEqual(["linux_x64"], parameters["platforms"])
        self.assertEqual("checked", parameters["buildConfig"])
        post_build = parameters["jobParameters"]["postBuildSteps"][0]["parameters"]
        self.assertEqual("tree GC", post_build["testBuildArgs"])
        self.assertEqual(["normal"], post_build["scenarios"])
        self.assertEqual("coreclr", post_build["runtimeFlavor"])

    def test_gc_stress_uses_standard_plumbing_with_bounded_scenario(self) -> None:
        values = {
            "publishToExperimentalFeed": False,
            "gcExperimentMode": "representative",
        }
        jobs = _expand_conditionals(_build_jobs(self.pipeline), values)
        stress_jobs = [
            job
            for job in jobs
            if job.get("parameters", {})
            .get("jobParameters", {})
            .get("testGroup")
            == "gcstress0x3-gcstress0xc"
        ]

        self.assertEqual(2, len(stress_jobs))
        native_asset_groups = [
            template["parameters"]["testGroup"]
            for job in jobs
            for template in job.get("parameters", {})
            .get("jobParameters", {})
            .get("extraVariablesTemplates", [])
            if template["template"].endswith("native-test-assets-variables.yml")
        ]
        self.assertEqual(["gcstress0x3-gcstress0xc"], native_asset_groups)
        run_job = next(
            job
            for job in stress_jobs
            if job["parameters"]["jobTemplate"]
            == "/eng/pipelines/common/templates/runtimes/run-test-job.yml"
        )
        self.assertEqual(
            ["gcstress0xc"],
            run_job["parameters"]["jobParameters"]["gcStressScenarios"],
        )

        run_test_template = _load_yaml(RUN_TEST_TEMPLATE_PATH)
        self.assertEqual([], run_test_template["parameters"]["gcStressScenarios"])
        template_text = RUN_TEST_TEMPLATE_PATH.read_text(encoding="utf-8")
        self.assertIn("- gcstress0x3", template_text)
        self.assertIn("- gcstress0xc", template_text)

    def test_parameters_fail_closed(self) -> None:
        self.assertEqual("string", self.parameters["gcExperimentMode"]["type"])
        self.assertNotIn("gcValidationLane", self.parameters)
        self.assertNotIn("runtimePerformanceLane", self.parameters)
        self.assertEqual("boolean", self.parameters["publishToExperimentalFeed"]["type"])

    def test_manifest_template_passes_source_and_build_identity(self) -> None:
        template = MANIFEST_TEMPLATE_PATH.read_text(encoding="utf-8")
        for variable in (
            "$(Build.Repository.Uri)",
            "$(Build.SourceBranch)",
            "$(Build.SourceVersion)",
            "$(performanceRepositoryRef)",
            "$(performanceSourceVersion)",
            "$(Build.BuildId)",
            "$(Build.BuildNumber)",
            "$(System.JobName)",
        ):
            self.assertIn(variable, template)


class CohortManifestTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        spec = importlib.util.spec_from_file_location(
            "generate_cohort_manifest", MANIFEST_GENERATOR_PATH
        )
        cls.generator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.generator)

    def test_manifest_hashes_packages_and_records_identity(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package_root = Path(directory) / "packages"
            package_root.mkdir()
            package = package_root / "runtime.gcbase.1.0.0.nupkg"
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr(
                    "runtime.gcbase.nuspec",
                    """
<package>
  <metadata>
    <id>runtime.gcbase</id>
    <version>1.0.0</version>
  </metadata>
</package>
""".strip(),
                )
            artifact = Path(directory) / "BuildArtifacts_linux_x64_Release_coreclr.tar.gz"
            artifact.write_bytes(b"runtime artifact")
            args = argparse.Namespace(
                package_root=package_root,
                source_repository="https://github.com/dotnet/runtimelab",
                source_branch="refs/heads/feature/gc/baseline",
                source_version="2d1d63dae261a338888b91582c9b447e2b7445ad",
                performance_repository_ref="refs/heads/main",
                performance_source_version="1234567890abcdef1234567890abcdef12345678",
                build_id="123",
                build_number="20260923.1",
                job_name="build_linux_x64_release_coreclr",
                build_configuration="Release",
                os_group="linux",
                architecture="x64",
                cohort_role="runtime",
                artifact=[artifact],
            )

            manifest = self.generator.create_manifest(args)

            self.assertEqual(args.source_version, manifest["source"]["commit"])
            self.assertEqual(args.build_id, manifest["build"]["id"])
            self.assertEqual(args.build_number, manifest["build"]["number"])
            self.assertEqual(args.job_name, manifest["build"]["job"])
            self.assertEqual(
                args.performance_source_version,
                manifest["performanceSource"]["commit"],
            )
            self.assertEqual("runtime.gcbase", manifest["packages"][0]["id"])
            self.assertEqual("1.0.0", manifest["packages"][0]["version"])
            self.assertEqual(
                hashlib.sha256(package.read_bytes()).hexdigest(),
                manifest["packages"][0]["sha256"],
            )
            self.assertEqual(
                hashlib.sha256(artifact.read_bytes()).hexdigest(),
                manifest["artifacts"][0]["sha256"],
            )

    def test_manifest_rejects_missing_package_cohort(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            args = argparse.Namespace(
                package_root=Path(directory),
                source_repository="repo",
                source_branch="branch",
                source_version="2d1d63dae261a338888b91582c9b447e2b7445ad",
                performance_repository_ref="refs/heads/main",
                performance_source_version="1234567890abcdef1234567890abcdef12345678",
                build_id="123",
                build_number="1",
                job_name="job",
                build_configuration="Release",
                os_group="linux",
                architecture="x64",
                cohort_role="runtime",
                artifact=[],
            )

            with self.assertRaisesRegex(ValueError, "no NuGet packages"):
                self.generator.create_manifest(args)

    def test_source_version_validation_fails_closed(self) -> None:
        with self.assertRaises(argparse.ArgumentTypeError):
            self.generator._source_version("main")


if __name__ == "__main__":
    unittest.main()
