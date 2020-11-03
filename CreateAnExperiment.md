# Create an experiment

Experiments should be contained within a branch in the dotnet/runtimelab repository. Keeping all experiments branches in one repository helps with community visibility.

## Steps to setup a new experiment

- Pick a good name for your experiment and create branch for it in dotnet/runtimelab. Branch names should be prefixed with `feature/` in order to have official build support.
   - If the experiment is expected to require changes of .NET runtime itself, it should be branched off of [dotnet/runtimelab:runtime-master](https://github.com/dotnet/runtimelab/tree/runtime-master) that is a manually maitained mirror of [dotnet/runtime:master](https://github.com/dotnet/runtime/tree/master).
   - Otherwise, the experiment should be branched off of [dotnet/runtimelab:standalone-experiment](https://github.com/dotnet/runtimelab/tree/standalone-experiment) to get CI and all publishing infrastructure for your experiment.
- Submit a PR to update the [README.MD](https://github.com/dotnet/runtimelab/blob/master/README.md#active-experimental-projects) with the name of your branch and a brief description of the experiment. Example: [#19](https://github.com/dotnet/runtimelab/pull/19/files)
- Create label `area-<your experiment name>` for tagging issues. The label should use color `#d4c5f9`. 
- Edit `README.MD` in your experiment branch to include details about the experiment. Example: [README.md](https://github.com/dotnet/runtimelab/blob/feature/NativeAOT/README.md).
- If your experiment is branched from dotnet/runtime:
   - Update the pre-release label to include a unique identifier representing the name of the experiment to avoid package clashes given that all experiments publish to the same [feed](https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-experimental). To do this you need to update the versioning properties in [`Versions.props`](https://github.com/dotnet/runtimelab/blob/0cf87055346fd12fb22478f17521ebeb28a6d323/eng/Versions.props#L9)
   - Enable CI builds by editing `eng/pipelines/runtimelab.yml` in your branch. Example: [#137](https://github.com/dotnet/runtimelab/pull/137/files)
   - To avoid spurious github notifications for merges from upstream, delete `.github/CODEOWNERS` from your branch or replace it with setting specific to your experiment. Example: [#26](https://github.com/dotnet/runtimelab/pull/26/files)
- If your experiment is branched from [dotnet/runtimelab:standalone-experiment](https://github.com/dotnet/runtimelab/tree/standalone-experiment) follow the [README.md](https://github.com/dotnet/runtimelab/tree/standalone-template#standalone-experiments).
