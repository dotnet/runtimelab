# Create an experiment

Experiments should be contained within a branch in the dotnet/runtimelab repository. Keeping all experiments branches in one repository helps with community visibility.

## Steps to setup a new experiment

- Pick a good name for your experiment and create branch for it in dotnet/runtimelab. Branch names should be prefixed with `feature/` in order to have official build support.
   - If the experiment is expected to require changes of .NET runtime itself, it should be branched off of [dotnet/runtimelab:runtime-main](https://github.com/dotnet/runtimelab/tree/runtime-main) that is a automatically maintained mirror of [dotnet/runtime:main](https://github.com/dotnet/runtime/tree/main).
   - Otherwise, the experiment should be branched off of [dotnet/runtimelab:standalone-template](https://github.com/dotnet/runtimelab/tree/standalone-template) to get CI and all publishing infrastructure for your experiment.
- Submit a PR to update the [README.MD](https://github.com/dotnet/runtimelab/blob/docs/README.md#active-experimental-projects) with the name of your branch and a brief description of the experiment. Example: [#19](https://github.com/dotnet/runtimelab/pull/19/files)
- Create label `area-<your experiment name>` for tagging issues. The label should use color `#d4c5f9`. 
- Edit `README.MD` in your experiment branch to include details about the experiment. Example: [README.md](https://github.com/dotnet/runtimelab/blob/feature/NativeAOT/README.md).
- If your experiment is branched from dotnet/runtime:
   - Update the pre-release label to include a unique identifier representing the name of the experiment to avoid package clashes given that all experiments publish to the same [feed](https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-experimental). To do this you need to update the versioning properties in [`Versions.props`](https://github.com/dotnet/runtimelab/blob/0cf87055346fd12fb22478f17521ebeb28a6d323/eng/Versions.props#L9). Make sure the label you choose is maximum 7 chars long as NuGet has a limit on the version length so the official build would fail.
   - Update the `GitHubRepositoryName` property in [`Directory.Build.Props`](https://github.com/dotnet/runtimelab/blob/a4f11b05c8a76564a88ae060fd75894ca9202d12/Directory.Build.props#L219) to `runtimelab`. This is needed for the produced packages to have the right repository information and for source link to work correctly.
   - Edit `eng/pipelines/runtimelab.yml` in your branch to just build what your experiment needs on CI.
   - To avoid spurious github notifications for merges from upstream, delete `.github/CODEOWNERS` from your branch or replace it with setting specific to your experiment. Example: [#26](https://github.com/dotnet/runtimelab/pull/26/files)
   - Make sure to edit your experiment branch to just build the packages that need to be built from that branch. [Example PR](https://github.com/dotnet/runtimelab/pull/467). To read more about why this can cause issues, read [this issue](https://github.com/dotnet/runtimelab/issues/465).
- If your experiment is branched from [dotnet/runtimelab:standalone-template](https://github.com/dotnet/runtimelab/tree/standalone-template) follow the [README.md](https://github.com/dotnet/runtimelab/tree/standalone-template#standalone-experiments).
- To make sure we follow our naming conventions, make sure all packages produced on your experiment are prefixed with `Microsoft.*` or `System.*`.
