namespace BindingsGeneration;

public record ArgumentDecl {
    public string? PublicName {get; set;}
    public required string PrivateName{get; set;}
    public required TypeDecl Type {get; set;}
    public required bool IsInOut {get; set;}
}