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

See [Create an experiment](https://github.com/dotnet/runtimelab/blob/docs/CreateAnExperiment.md)
for the standard runtimelab conventions.
