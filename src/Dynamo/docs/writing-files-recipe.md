# Recipe for Writing a C# File

```csharp
var csFile = new CSFile("This.Is.A.Namespace");
// ...
CodeWriter.WriteToFile("/path/to/output", csFile);
```

# Recipe for Writing a C# File Asynchronously

```csharp
var csFile = new CSFile("This.Is.A.Namespace");
// ...
await CodeWriter.WriteToFileAsync("/path/to/output", csFile);
```