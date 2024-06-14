// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Interop.JavaScript
{
    public sealed partial class JSExportGenerator
    {
        private const string AsyncEntryPointThunkClassNamespace = "System.Runtime.InteropServices.JavaScript";
        private const string AsyncEntryPointThunkClassName = "GeneratedAsyncEntryPointThunkClass";
        private const string AsyncEntryPointThunkMethodName = "AsyncEntryPointThunk";
        private const string AsyncEntryPointThunkExportName = "System_Runtime_InteropServices_JavaScript_JavaScriptExports_CallEntrypoint";

        private static readonly SymbolDisplayFormat s_qualifiedNameOnlyFormat = new(
            SymbolDisplayGlobalNamespaceStyle.Omitted,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private static readonly SymbolDisplayFormat s_localFullTypeNameFormat = new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private static AsyncEntryPointInfo GetAsyncEntryPointInfo(Compilation compilation, CancellationToken cancellationToken)
        {
            IMethodSymbol? symbol = compilation.GetEntryPoint(cancellationToken);
            if (symbol is null)
            {
                return null;
            }

            INamedTypeSymbol containingType = symbol.ContainingType;
            Debug.Assert(!containingType.IsGenericType); // Generic types cannot contain entry points.

            if (containingType.IsFileLocal)
            {
                DiagnosticInfo error = containingType.CreateDiagnosticInfo(
                    GeneratorDiagnostics.EntryPointDefinedInFileClass,
                    containingType.ToDisplayString(s_localFullTypeNameFormat));
                return new AsyncEntryPointInfo(error);
            }

            if (containingType.ContainingType != null)
            {
                // We could handle this, but it would complicate the code quite a bit, for limited benefit.
                DiagnosticInfo error = containingType.CreateDiagnosticInfo(
                    GeneratorDiagnostics.EntryPointDefinedInNestedClass,
                    containingType.ToDisplayString(s_localFullTypeNameFormat));
                return new AsyncEntryPointInfo(error);
            }

            // Determine if the entry point is accessible or not. "Accessible" in this context means that we
            // can invoke it via a simple "OwningClass.Main()". As a UX optimization, we won't require that
            // accessible entry points be contained in a partial class.
            bool isAccessible = symbol.DeclaredAccessibility is
                Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;
            bool isTopLevelEntryPoint = symbol.Name == WellKnownMemberNames.TopLevelStatementsEntryPointMethodName;

            // Note that the top-level entry point type is in fact partial, so there can be explicit syntax
            // references to it in the compilation. That is why we check the method name above.
            if (!isAccessible && !isTopLevelEntryPoint)
            {
                ImmutableArray<SyntaxReference> declSyntaxRefs = containingType.DeclaringSyntaxReferences;
                if (declSyntaxRefs.IsEmpty ||
                    declSyntaxRefs[0].GetSyntax(cancellationToken) is not TypeDeclarationSyntax declSyntax)
                {
                    // This should never happen, but just in case it does, let's not crash the compilation...
                    Debug.Fail("Entry point class declaration syntax missing");
                    return null;
                }

                if (!declSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    DiagnosticInfo error = containingType.CreateDiagnosticInfo(
                        GeneratorDiagnostics.PrivateEntryPointDefinedInNotPartialClass,
                        containingType.ToDisplayString(s_localFullTypeNameFormat));
                    return new AsyncEntryPointInfo(error);
                }
            }

            EntryPointSigType returnSigType = symbol.ReturnType.SpecialType switch
            {
                SpecialType.System_Void => EntryPointSigType.None,
                SpecialType.System_Int32 => EntryPointSigType.Int,
                _ when symbol.ReturnType.MetadataName is "Task" => EntryPointSigType.TaskOfVoid,
                _ => EntryPointSigType.TaskOfInt
            };
            EntryPointSigType argsSigType = symbol.Parameters.Length is 0 ? EntryPointSigType.None : EntryPointSigType.Args;

            EntryPointAccessType access = isAccessible switch
            {
                true => EntryPointAccessType.Direct,
                false when isTopLevelEntryPoint => EntryPointAccessType.Unspeakable,
                _ => EntryPointAccessType.Indirect
            };

            return new AsyncEntryPointInfo(
                containingType.ContainingNamespace.ToDisplayString(s_qualifiedNameOnlyFormat),
                containingType.Name,
                symbol.Name,
                returnSigType,
                argsSigType,
                access);
        }

        private static string GenerateAsyncEntryPointInfoSource(AsyncEntryPointInfo info)
        {
            if (info is null || info.Error is not null)
            {
                return null;
            }

            string langRetType = info.ReturnType switch
            {
                EntryPointSigType.None => "void",
                EntryPointSigType.Int => "int",
                EntryPointSigType.TaskOfVoid => "global::System.Threading.Tasks.Task",
                EntryPointSigType.TaskOfInt => "global::System.Threading.Tasks.Task<int>",
                _ => throw new UnreachableException()
            };
            string fptrSigArgs = info.ArgumentType is EntryPointSigType.None ? "" : "string[], ";
            string methodArgs = info.ArgumentType is EntryPointSigType.None ? "" : "args";
            string methodArgsTrailing = info.ArgumentType is EntryPointSigType.None ? "" : $", {methodArgs}";
            string methodSigArgs = info.ArgumentType is EntryPointSigType.None ? "" : $"string[] {methodArgs}";
            string methodSigArgsTrailing = info.ArgumentType is EntryPointSigType.None ? "" : $", {methodSigArgs}";
            string entryPointTypeFullName = info.TypeNamespace is not ""
                ? $"global::{info.TypeNamespace}.{info.TypeName}"
                : $"global::{info.TypeName}";

            string fptrValue;
            string thunkCode = "";
            string globalCode = "";
            switch (info.Access)
            {
                case EntryPointAccessType.Direct:
                    fptrValue = $"(delegate*<{fptrSigArgs}{langRetType}>)&{entryPointTypeFullName}.{info.MethodName}";
                    break;

                case EntryPointAccessType.Indirect:
                    fptrValue = $"{entryPointTypeFullName}.GetUserDefinedEntryPointMethodAddress()";
                    globalCode = $@"
{(info.TypeNamespace is not "" ? $"namespace {info.TypeNamespace}\n{{" : "")}
    partial class {info.TypeName}
    {{
        internal static unsafe delegate*<{fptrSigArgs}{langRetType}> GetUserDefinedEntryPointMethodAddress() => &{info.MethodName};
    }}
{(info.TypeNamespace is not "" ? "}" : "")}";
                    break;

                case EntryPointAccessType.Unspeakable:
                    fptrValue = $"(delegate*<{fptrSigArgs}{langRetType}>)&InvokeUserEntryPoint";
                    thunkCode = $@"
        private static {langRetType} InvokeUserEntryPoint({methodSigArgs}) => UserEntryPoint(default{methodArgsTrailing});

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = ""{info.MethodName}"")]
        private static extern {langRetType} UserEntryPoint({info.TypeName} _{methodSigArgsTrailing});";
                    break;

                default:
                    throw new UnreachableException();
            }

            // The other part of this class (implementing the actual JSExport entry point) is delivered via
            // a hidden "Compile" item in the NuGet package targets. This way, we don't have to add a special
            // path to the stub generation that would have to deal with the lack of actual syntax for the entry
            // point thunk.
            string source = $@"
// <auto-generated />

namespace {AsyncEntryPointThunkClassNamespace}
{{
    using System.Runtime.CompilerServices;

    internal static partial class {AsyncEntryPointThunkClassName}
    {{
        private static unsafe void GetEntryPointInfo(void** pEntryPoint, int* sigRetType, int* sigArgsType)
        {{
            *pEntryPoint = {fptrValue};
            *sigRetType = {(int)info.ReturnType};
            *sigArgsType = {(int)info.ArgumentType};
        }}
        {thunkCode}
    }}
}}
{globalCode}
";
            return source;
        }

        private static bool IsAsyncEntryPointThunkMethod(string methodWithContainingTypeName)
        {
            // This can technically yield false-positives since we don't compare namespaces but that's inevitable
            // at some level anyway in a design that relies on a known entry point name.
            return methodWithContainingTypeName == $"{AsyncEntryPointThunkClassNamespace}.{AsyncEntryPointThunkClassName}.{AsyncEntryPointThunkMethodName}";
        }

        private sealed record AsyncEntryPointInfo(
            string TypeNamespace,
            string TypeName,
            string MethodName,
            EntryPointSigType ReturnType,
            EntryPointSigType ArgumentType,
            EntryPointAccessType Access,
            DiagnosticInfo Error = null)
        {
            public AsyncEntryPointInfo(DiagnosticInfo error) : this(null, null, null, default, default, default, error) { }
        }

        private enum EntryPointSigType
        {
            None,
            Args,
            Int,
            TaskOfVoid,
            TaskOfInt
        }

        private enum EntryPointAccessType
        {
            Direct,
            Indirect,
            Unspeakable
        }
    }
}
