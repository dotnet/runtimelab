// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;

namespace BindingsGeneration;

/// <summary>
/// Factory class for creating instances of PropertyHandler.
/// </summary>
public class PropertyHandlerFactory : IFactory<BaseDecl, IPropertyHandler>
{
    public bool Handles(BaseDecl decl)
    {
        return decl is PropertyDecl;
    }

    public IPropertyHandler Construct()
    {
        return new PropertyHandler();
    }
}

/// <summary>
/// Handler class for property declarations that generates the binding code for Swift properties.
/// </summary> 
public class PropertyHandler : BaseHandler, IPropertyHandler
{
    private readonly MethodHandler _methodHandler = new();

    /// <inheritdoc/>
    public IEnvironment Marshal(BaseDecl baseDecl, ITypeDatabase typeDatabase)
    {
        if (baseDecl is not PropertyDecl propertyDecl)
        {
            throw new ArgumentException("The provided decl must be a PropertyDecl.", nameof(baseDecl));
        }
        return new PropertyEnvironment(propertyDecl, typeDatabase);
    }

    /// <inheritdoc/>
    public void Emit(CSharpWriter csWriter, SwiftWriter swiftWriter, IEnvironment env, Conductor conductor)
    {
        var propertyEnv = (PropertyEnvironment)env;
        var propertyDecl = propertyEnv.PropertyDecl;

        var typeRecord = propertyEnv.TypeDatabase.GetTypeRecordOrThrow(propertyDecl.SwiftTypeSpec);

        if (propertyDecl.Accessors.Count == 0)
        {
            // No public accessors, so we don't need to emit anything
            return;
        }

        if (propertyDecl.IsStatic)
        {
            throw new NotImplementedException("Static properties are not supported yet.");
        }
        var csTypeName = typeRecord.CSTypeIdentifier;


        // First emit the accessor methods using MethodHandler
        foreach (var accessor in propertyDecl.Accessors)
        {
            var accessorEnv = new MethodEnvironment(accessor.Method, propertyEnv.TypeDatabase);
            _methodHandler.Emit(csWriter, swiftWriter, accessorEnv, conductor);
        }

        // Then emit the property
        csWriter.WriteLine($"public {csTypeName} {propertyDecl.Name}");
        csWriter.WriteLine("{");
        csWriter.Indent++;

        var getter = propertyDecl.Accessors.OfType<GetAccessorDecl>().FirstOrDefault();
        if (getter != null)
        {
            EmitGetter(csWriter, getter);
        }

        csWriter.Indent--;
        csWriter.WriteLine("}");
        csWriter.WriteLine();
    }

    /// <summary>
    /// Emits the getter implementation for a property.
    /// </summary>
    /// <param name="csWriter">The C# code writer to emit to</param>
    /// <param name="getter">The getter accessor declaration</param>
    private void EmitGetter(CSharpWriter csWriter, GetAccessorDecl getter)
    {
        csWriter.WriteLine($"get => {getter.Method.Name}();");
    }
}

