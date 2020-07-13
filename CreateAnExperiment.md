# Create an experiment

Experiments should be contained within a sub-branch of the repository. Instead of using forks of the `dotnet/runtimelab` repository to house experiments, keep sub-branches in the official repository which helps with community visibility. Once an experiment sub-branch is pushed up, remember to submit a PR to update the [README.MD](README.MD#Active%20Experimental%20Projects) in the [main branch][main_branch_link] with the name of the sub-branch and a brief description of the experiment.

Things to consider:

- Experiments often involve updates to the [runtime](https://github.com/dotnet/runtime). Instead of branching off of the [main branch][main_branch_link] consider branching from the [official runtime branch](https://github.com/dotnet/runtimelab/tree/runtime-master). Including the entire runtime permits a self contained experiment that is easy for the community to try out.

<!-- common links -->

[main_branch_link]: https://github.com/dotnet/runtimelab/tree/master