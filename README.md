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

## GC correctness and stress validation

Definition 163 exposes three `gcValidationMode` values:

- `baseline` preserves the reviewed baseline graph.
- `correctness-shard` imports one of the five closed `gcValidationRoot` graphs.
- `correctness-stress` queues and monitors all five shard roots as child runs of
  definition 163.

The parent passes its exact source ref and 40-character source version to every
child without a YAML override. `gcValidationCampaignId` can identify an
explicit campaign; when empty, the parent derives `gc-validation-<build ID>`.
`gcValidationParentBuildId` is likewise optional for the parent and is always
populated in child template parameters. `gcValidationAttempt` is restricted to
1 or 2 and is used only for the single permitted infrastructure retry.
The same campaign, parent, root, mode, and attempt identity is persisted in
non-secret run variables because the Azure Runs List/Get responses do not
return submitted template parameters. The controller hydrates List entries
with individual Get responses before matching the exact source ref and commit.

The parent uses only `System.AccessToken` and checks its effective
`QueueBuilds` permission on definition 163 before submitting a child. It
publishes `GCValidationChildren_<parent build ID>`, containing each canonical
queue request, its SHA-256 hash, the adopted or returned child build ID,
terminal results and failure classifications, plus
`gc-validation-children.json`. The parent succeeds only when that receipt
contains all five terminal child receipts and every final child result
succeeded. The receipt also distinguishes submitted queue requests from
confirmed new runs and records transient monitoring API errors.
An attempt 2 run is accepted only when a unique attempt 1 is freshly confirmed
terminal and unsuccessful and its timeline contains only proven transient
agent-loss evidence. A successful POST whose response cannot be read or decoded
is treated as ambiguous and adopted by identity without repeating the POST.
