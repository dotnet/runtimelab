namespace System.IO.StreamSourceGeneration;

/// <summary>
/// Instructs the System.IO.StreamSourceGeneration source generator to generate boilerplate implementations for multiple legacy members, leaving the implementer only with the task of providing implementations for the core operations.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateStreamBoilerplateAttribute : Attribute
{
    public bool CanRead { get; set; } = true;
    public bool CanSeek { get; set; } = true;
    public bool CanWrite { get; set; } = true;
}