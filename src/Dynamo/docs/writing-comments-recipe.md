# Prepending Code With A Comment

```csharp
var code = GenerateSomeCode();
new CSComment("This is a comment").AttachBefore(code);
```