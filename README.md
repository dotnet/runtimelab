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
Those caller-supplied variables are discovery hints, not trusted child
evidence.
When concurrent parents create duplicate children, the lowest matching run ID
is canonical, even after it reaches a terminal state. Every shard first runs
`GCValidationAdmission`, observes exact matching children for 60 seconds, and
permits only the active canonical run to enter the unchanged authoritative root
stage. A duplicate fails before any standard build or Helix job and publishes
`GCValidationAdmission_<child build ID>/gc-validation-admission.json`.
The parent accepts a terminal child only after its native Azure timeline shows
the `GCValidationAdmission` stage and job succeeded and the authoritative
`Build` stage is present, terminal, non-skipped, and succeeded. It also lists
and downloads the exact admission artifact, then validates the receipt's
campaign, parent, root, attempt, source ref and commit, current and canonical
run IDs, admitted status, and canonical SHA-256. A baseline run, skipped Build,
missing artifact, or mismatched receipt is recorded as a rejected candidate and
does not prevent the parent from queueing or adopting a genuine shard. The
overall pipeline result alone is never accepted as proof of shard success.
Incomplete terminal timeline or artifact visibility is retried for a bounded
two-minute consistency window; evidence that remains incomplete is rejected so
it cannot indefinitely suppress a genuine replacement shard.

The parent uses only `System.AccessToken` and checks its effective
`QueueBuilds` permission on definition 163 before submitting a child. It
publishes `GCValidationChildren_<parent build ID>`, containing each canonical
queue request, its SHA-256 hash, the adopted or returned child build ID,
terminal results and failure classifications, plus
`gc-validation-children.json`. The parent succeeds only when that receipt
contains all five terminal child receipts and every final child result
succeeded. The receipt also distinguishes submitted queue requests from
confirmed new runs, records rejected candidates and their native evidence, and
records transient monitoring API errors.
An attempt 2 run is accepted only when the canonical attempt 1 is freshly
confirmed by the same native evidence as terminal and unsuccessful and its
timeline contains only proven transient agent-loss evidence. A successful POST
whose response cannot be read or decoded is treated as ambiguous and adopted
by identity without repeating the POST. A returned ID is trusted only when it
is a positive JSON integer; all other response shapes use the same
adoption-only path.
