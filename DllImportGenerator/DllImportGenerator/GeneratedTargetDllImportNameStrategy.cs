using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Microsoft.Interop
{
    interface ITargetDllImportNameStrategy
    {
        public string GenerateDllImportEntryPointName(
            SemanticModel model,
            IMethodSymbol method,
            DllImportStub.GeneratedDllImportData targetDllImportData,
            TypeSyntax returnType,
            TypeSyntax[] parameterTypes,
            out bool duplicateEntryPoint
            );
    }

    class GeneratedTargetDllImportNameStrategy : ITargetDllImportNameStrategy
    {
        struct TypeSymbolOrSyntax : IEquatable<TypeSymbolOrSyntax>
        {
            private readonly bool hasSymbol;
            private readonly ITypeSymbol? symbol;
            private readonly TypeSyntax? syntax;

            private TypeSymbolOrSyntax(ITypeSymbol symbol)
            {
                this.hasSymbol = true;
                this.symbol = symbol;
                this.syntax = null;
            }
            private TypeSymbolOrSyntax(TypeSyntax syntax)
            {
                this.hasSymbol = true;
                this.symbol = null;
                this.syntax = syntax;
            }

            public override int GetHashCode()
            {
                if (hasSymbol)
                {
                    return SymbolEqualityComparer.Default.GetHashCode(symbol);
                }
                else
                {
                    return syntax!.ToString().GetHashCode() ^ 0x1;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is TypeSymbolOrSyntax other && Equals(other);
            }

            public bool Equals(TypeSymbolOrSyntax other)
            {
                if (hasSymbol)
                {
                    return other.hasSymbol && SymbolEqualityComparer.Default.Equals(symbol, other.symbol);
                }
                else
                {
                    return !other.hasSymbol && this.syntax!.ToString() == other.syntax!.ToString();
                }
            }

            public static TypeSymbolOrSyntax CreateFromSemanticModel(SemanticModel model, TypeSyntax syntax)
            {
                TypeInfo info = model.GetSpeculativeTypeInfo(0, syntax, SpeculativeBindingOption.BindAsTypeOrNamespace);
                if (info.Type is null or IErrorTypeSymbol)
                {
                    return new TypeSymbolOrSyntax(syntax);
                }
                else
                {
                    return new TypeSymbolOrSyntax(info.Type);
                }
            }
        }

        sealed record DllImportEntryPoint(ITypeSymbol ContainingType, ImmutableArray<TypeSymbolOrSyntax> Parameters, TypeSymbolOrSyntax ReturnValue)
        {
            public override int GetHashCode()
            {
                return SymbolEqualityComparer.Default.GetHashCode(ContainingType) ^ ReturnValue.GetHashCode();
            }

            public bool Equals(DllImportEntryPoint other)
            {
                bool equal = ReturnValue.Equals(other.ReturnValue);
                if (!equal || Parameters.Length != other.Parameters.Length)
                {
                    return false;
                }

                for (int i = 0; i < Parameters.Length; i++)
                {
                    if (!Parameters[i].Equals(other.Parameters[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private Dictionary<DllImportEntryPoint, List<(DllImportStub.GeneratedDllImportData data, string name)>> emittedEntryPoints = new();

        public string GenerateDllImportEntryPointName(
            SemanticModel model,
            IMethodSymbol method,
            DllImportStub.GeneratedDllImportData targetDllImportData,
            TypeSyntax returnType,
            TypeSyntax[] parameterTypes,
            out bool duplicateEntryPoint
            )
        {
            ITypeSymbol containingType = method.ContainingType;
            TypeSymbolOrSyntax resolvedReturnType = TypeSymbolOrSyntax.CreateFromSemanticModel(model, returnType);
            TypeSymbolOrSyntax[] resolvedParameterTypes = new TypeSymbolOrSyntax[parameterTypes.Length];
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                resolvedParameterTypes[i] = TypeSymbolOrSyntax.CreateFromSemanticModel(model, parameterTypes[i]);
            }
            DllImportEntryPoint entryPoint = new DllImportEntryPoint(containingType, ImmutableArray.Create(resolvedParameterTypes), resolvedReturnType);

            if (!emittedEntryPoints.TryGetValue(entryPoint, out var emittedEntryPointAttributeInfo))
            {
                duplicateEntryPoint = false;
                string name = GenerateEntryPointName(method, targetDllImportData);
                emittedEntryPoints.Add(entryPoint, new List<(DllImportStub.GeneratedDllImportData, string)> { (targetDllImportData, name) });
                return name;
            }

            duplicateEntryPoint = false;
            foreach (var info in emittedEntryPointAttributeInfo)
            {
                if (info.data.IsUserDefined != targetDllImportData.IsUserDefined)
                {
                    continue;
                }
                bool attributeDataMatches = true;
                if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.BestFitMapping))
                {
                    attributeDataMatches &= targetDllImportData.BestFitMapping == info.data.BestFitMapping;
                }
                if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.CallingConvention))
                {
                    attributeDataMatches &= targetDllImportData.CallingConvention == info.data.CallingConvention;
                }
                if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.CharSet))
                {
                    attributeDataMatches &= targetDllImportData.CharSet == info.data.CharSet;
                }
                if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.EntryPoint))
                {
                    attributeDataMatches &= targetDllImportData.EntryPoint == info.data.EntryPoint;
                }
                if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.ExactSpelling))
                {
                    attributeDataMatches &= targetDllImportData.ExactSpelling == info.data.ExactSpelling;
                }
                if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.PreserveSig))
                {
                    attributeDataMatches &= targetDllImportData.PreserveSig == info.data.PreserveSig;
                }
                if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.SetLastError))
                {
                    attributeDataMatches &= targetDllImportData.SetLastError == info.data.SetLastError;
                }
                if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.ThrowOnUnmappableChar))
                {
                    attributeDataMatches &= targetDllImportData.ThrowOnUnmappableChar == info.data.ThrowOnUnmappableChar;
                }

                if (attributeDataMatches)
                {
                    duplicateEntryPoint = true;
                    return info.name;
                }
            }

            string newEntryPointName = GenerateEntryPointName(method, targetDllImportData);
            emittedEntryPointAttributeInfo.Add((targetDllImportData, newEntryPointName));
            return newEntryPointName;
        }

        private static string GenerateEntryPointName(IMethodSymbol method, DllImportStub.GeneratedDllImportData targetDllImportData)
        {
            // Generate a new entry-point name based on the method name and target DllImportAttibute info.
            StringBuilder builder = new StringBuilder(method.Name);
            builder.Append("__PInvoke__");
            builder.Append(targetDllImportData.ModuleName.Replace('.', '_'));
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.BestFitMapping)
                && targetDllImportData.BestFitMapping)
            {
                builder.Append("BestFitMapping__");
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.CallingConvention))
            {
                builder.Append("CallingConvention__");
                builder.Append(targetDllImportData.CallingConvention);
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.CharSet))
            {
                builder.Append("CharSet__");
                builder.Append(targetDllImportData.CharSet);
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.EntryPoint))
            {
                builder.Append("EntryPoint__");
                builder.Append(targetDllImportData.EntryPoint.Replace('.', '_'));
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.ExactSpelling)
                && targetDllImportData.ExactSpelling)
            {
                builder.Append("ExactSpelling__");
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.PreserveSig)
                && targetDllImportData.PreserveSig)
            {
                builder.Append("PreserveSig__");
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.SetLastError)
                && targetDllImportData.SetLastError)
            {
                builder.Append("SetLastError__");
            }
            if (targetDllImportData.IsUserDefined.HasFlag(DllImportStub.DllImportMember.ThrowOnUnmappableChar)
                && targetDllImportData.ThrowOnUnmappableChar)
            {
                builder.Append("ThrowOnUnmappableChar__");
            }
            string newEntryPointName = builder.ToString();
            return newEntryPointName;
        }
    }

    class ForwarderDllImportNameStrategy : ITargetDllImportNameStrategy
    {
        public string GenerateDllImportEntryPointName(SemanticModel model, IMethodSymbol method, DllImportStub.GeneratedDllImportData targetDllImportData, TypeSyntax returnType, TypeSyntax[] parameterTypes, out bool duplicateEntryPoint)
        {
            duplicateEntryPoint = false;
            return method.Name + "__PInvoke__";
        }
    }
}
