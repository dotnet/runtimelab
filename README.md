# GC baseline

A runtime-side baseline for running runtimelab GC experiments with
[dotnet/performance](https://github.com/dotnet/performance).
GC implementations are developed in runtimelab; dotnet/performance supplies
the workloads and runner that exercise those runtimes.

`feature/gc/baseline` starts from `dotnet/runtime` commit
`f92f93994a587f69d6f7510d5390e68bd5290b2b`. It includes the full runtime source
and runtimelab publishing setup, with no experimental GC changes.

The NuGet prerelease label is **`gcbase`**. Standard runtime package IDs are
unchanged. Future experiments must use a different unique label of at most
seven characters.

## Build and publish

[`eng/pipelines/runtimelab-official.yml`](eng/pipelines/runtimelab-official.yml)
builds unsigned Release packages and uses the existing Arcade publishing stages.
MicroBuild signing is disabled for this experimental baseline; normal publishing
permissions and production-access checks still apply.
CI is manual-only; `publishToExperimentalFeed` defaults to `false`.

**Do not queue CI or publish to `dotnet-experimental` without explicit approval
after the changes have been reviewed.** Build-only approval is not publishing
approval.

## Optional GC validation and performance

The official pipeline owns the complete experiment graph so every validation
run is tied to the same source commit and canonical package build.
`gcExperimentMode` is a closed string parameter that defaults to `none`.
Selecting `representative` on a manual internal run adds three Linux x64 proofs:

- Standard Checked CoreCLR correctness: the `normal` scenario filtered to the
  `GC` test tree.
- Standard Checked CoreCLR GCStress plumbing, bounded to the first-signal
  `gcstress0xc` scenario.
- Standard Release CoreCLR dotnet/performance Viper microbenchmarks filtered to
  the `Runtime` category. This job consumes a pipeline artifact from the
  existing Release build rather than creating a second runtime build.

Representative mode fails during template expansion unless the run is both
manual and internal. With publication disabled, it also compiles out the
unrelated Windows Release and Libraries AllConfigurations jobs. Those canonical
jobs remain unchanged for the default mode and are restored when publication is
explicitly requested.

The performance lane requires read access to the existing
`internal/dotnet-performance` Azure Repos resource and the standard performance
and Helix resources used by that template. GC validation requires the existing
`DotNet-HelixApi-Access` variable group and CoreCLR Helix queues. No schedules,
Azure definitions, service connections, permissions, variable groups, or queue
workloads are created by this source change.

When representative mode is selected, each selected canonical package job
publishes a `GCExperimentManifest_*` artifact. The manifest records runtime and
performance source commits, Azure build and job identity, NuGet package IDs and
versions, and SHA-256 evidence for every produced package and optional runtime
artifact in that job.
Run the local static contract tests with:

```powershell
python -m pip install -r eng\pipelines\runtimelab\tests\requirements.txt
python -m unittest discover -s eng\pipelines\runtimelab\tests -v
```

See [Create an experiment](https://github.com/dotnet/runtimelab/blob/docs/CreateAnExperiment.md)
for the standard runtimelab conventions.
