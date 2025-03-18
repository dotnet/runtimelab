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
    private static readonly Dictionary<string, string> PropertyNameMappings = new()
    {
        { "isEligibleForIntroOffer", "isEligibleForIntroOfferProperty" },
    };

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
        // This will emit the C# equivalent of the Swift property.
        // To achieve this, the process is divided into the following steps:
        // 1. Emit Accessor Methods: Generate the C# methods that correspond to the Swift property's accessors (getter, setter, etc.).
        // 2. Emit Property Definition: Define the C# property itself, including its type, name, and accessors.
        //    This step utilizes the previously generated accessor methods to implement the property's behavior.

        var propertyEnv = (PropertyEnvironment)env;
        var propertyDecl = propertyEnv.PropertyDecl;

        bool processed = propertyEnv.TypeDatabase.TryGetTypeRecord(propertyDecl.SwiftTypeSpec, out var typeRecord);

        if (!processed)
        {
            Console.WriteLine($"PropertyHandler: Couldn't process property {propertyDecl.Name} of type {propertyDecl.SwiftTypeSpec}. Skipping.");
            return;
        }

        if (propertyDecl.Accessors.Count == 0)
        {
            // No public accessors, so we don't need to emit anything
            return;
        }

        var csTypeName = propertyEnv.BoundGenericsHandler.IsBoundGeneric(propertyDecl) switch
        {
            true => propertyEnv.BoundGenericsHandler.TranslateBoundGenericTypeToCSharp(propertyDecl),
            false => typeRecord!.CSharpTypeName.FullyQualifiedName
        };

        // TODO Detect and skip / Handle async properties https://github.com/dotnet/runtimelab/issues/2996

        // Get the C# property name, handling reserved keywords and special cases
        var propertyName = NameProvider.GetPropertyName(propertyDecl.Name);

        // First emit the accessor methods using MethodHandler
        foreach (var accessor in propertyDecl.Accessors)
        {
            if (conductor.TryGetMethodHandler(accessor.Method, out var methodHandler))
            {
                var accessorEnv = methodHandler.Marshal(accessor.Method, propertyEnv.TypeDatabase);
                methodHandler.Emit(csWriter, swiftWriter, accessorEnv, conductor);
            }
            else
            {
                throw new InvalidOperationException($"No handler found for properties accessor {accessor.Method.Name}");
            }
        }
        var staticModifier = propertyDecl.IsStatic ? "static " : string.Empty;
        // Then emit the property
        csWriter.WriteLine($"public {staticModifier}{csTypeName} {propertyName}");
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

