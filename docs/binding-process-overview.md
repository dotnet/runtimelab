# Overview of the Processes Used in Binding

Functional requirements of the projection tooling:
 - The projection tooling shall consume a single ABI.json file and a .dylib
 - The projection tooling shall consume a type database for projected frameworks
 - The projection tooling shall generate C# source code
 - The projection tooling shall generate Swift wrappers for async functions to bridge gaps between different C# and Swift ABIs
 - The projection tooling shall allow customization (-include, -exclude, -rename) 
 - The projection tooling shall gracefully handle unsupported types

The distribution pipeline is responsible for ensuring the correct order of frameworks. It is also responsible for generating ABI.json files from the frameworks.
