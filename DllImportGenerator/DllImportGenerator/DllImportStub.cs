﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;

namespace Microsoft.Interop
{
    internal class DllImportStub
    {
        private DllImportStub()
        {
        }

        public string StubTypeNamespace { get; private set; }

        public IEnumerable<string> StubContainingTypesDecl { get; private set; }

        public string StubReturnType { get; private set; }

        public IEnumerable<(string Type, string Name, RefKind refKind)> StubParameters { get; private set; }

        public IEnumerable<string> StubCode { get; private set; }

        public string DllImportReturnType { get; private set; }

        public string DllImportMethodName { get; private set; }

        public IEnumerable<(string Type, string Name, RefKind refKind)> DllImportParameters { get; private set; }

        public IEnumerable<Diagnostic> Diagnostics { get; private set; }

        /// <summary>
        /// Flags used to indicate members on GeneratedDllImport attribute.
        /// </summary>
        [Flags]
        public enum DllImportMember
        {
            None = 0,
            BestFitMapping = 1 << 0,
            CallingConvention = 1 << 1,
            CharSet = 1 << 2,
            EntryPoint = 1 << 3,
            ExactSpelling = 1 << 4,
            PreserveSig = 1 << 5,
            SetLastError = 1 << 6,
            ThrowOnUnmappableChar = 1 << 7,
        }

        /// <summary>
        /// DllImport attribute data
        /// </summary>
        /// <remarks>
        /// The names of these members map directly to those on the
        /// DllImportAttribute and should not be changed.
        /// </remarks>
        public class DllImportAttributeData
        {
            public string ModuleName { get; set; }

            /// <summary>
            /// Value set by the user on the original declaration.
            /// </summary>
            public DllImportMember IsUserDefined = DllImportMember.None;

            // Default values for the below fields are based on the
            // documented semanatics of DllImportAttribute:
            //   - https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.dllimportattribute
            public bool BestFitMapping { get; set; } = true;
            public CallingConvention CallingConvention { get; set; } = CallingConvention.Winapi;
            public CharSet CharSet { get; set; } = CharSet.Ansi;
            public string EntryPoint { get; set; } = null;
            public bool ExactSpelling { get; set; } = false; // VB has different and unusual default behavior here.
            public bool PreserveSig { get; set; } = true;
            public bool SetLastError { get; set; } = false;
            public bool ThrowOnUnmappableChar { get; set; } = false;
        }

        public static DllImportStub Create(
            IMethodSymbol method,
            DllImportAttributeData dllImportData,
            CancellationToken token = default)
        {
            // Cancel early if requested
            token.ThrowIfCancellationRequested();

            // Determine the namespace
            string stubTypeNamespace = null;
            if (!(method.ContainingNamespace is null)
                && !method.ContainingNamespace.IsGlobalNamespace)
            {
                stubTypeNamespace = method.ContainingNamespace.ToString();
            }

            // Determine type
            var stubContainingTypes = new List<string>();
            INamedTypeSymbol currType = method.ContainingType;
            while (!(currType is null))
            {
                var visibility = currType.DeclaredAccessibility switch
                {
                    Accessibility.Public => "public",
                    Accessibility.Private => "private",
                    Accessibility.Protected => "protected",
                    Accessibility.Internal => "internal",
                    _ => throw new NotSupportedException(), // [TODO] Proper error message
                };

                var typeKeyword = currType.TypeKind switch
                {
                    TypeKind.Class => "class",
                    TypeKind.Struct => "struct",
                    _ => throw new NotSupportedException(), // [TODO] Proper error message
                };

                stubContainingTypes.Add($"{visibility} partial {typeKeyword} {currType.Name}");
                currType = currType.ContainingType;
            }

            // Flip the order to that of how to declare the types
            stubContainingTypes.Reverse();

            // Determine parameter types
            var stubParams = new List<(string Type, string Name, RefKind RefKind)>();
            var dllImportParams = new List<(string Type, string Name, RefKind RefKind)>();
            foreach (var namePair in method.Parameters)
            {
                stubParams.Add((ComputeTypeForStub(namePair.Type, namePair.RefKind), namePair.Name, namePair.RefKind));
                dllImportParams.Add((ComputeTypeForDllImport(namePair.Type, namePair.RefKind), namePair.Name, namePair.RefKind));
            }

            string dllImportName = method.Name + "__PInvoke__";

#if !GENERATE_FORWARDER
            var dispatchCall = new StringBuilder($"throw new System.{nameof(NotSupportedException)}();");
#else
            // Forward call to generated P/Invoke
            var returnMaybe = method.ReturnType.SpecialType == SpecialType.System_Void
                ? string.Empty
                : "return ";

            var dispatchCall = new StringBuilder($"{returnMaybe}{dllImportName}");
            if (!dllImportParams.Any())
            {
                dispatchCall.Append("();");
            }
            else
            {
                char delim = '(';
                foreach (var param in dllImportParams)
                {
                    dispatchCall.Append($"{delim}{RefKindToString(param.RefKind)}{param.Name}");
                    delim = ',';
                }
                dispatchCall.Append(");");
            }
#endif

            return new DllImportStub()
            {
                StubTypeNamespace = stubTypeNamespace,
                StubContainingTypesDecl = stubContainingTypes,
                StubReturnType = ComputeTypeForStub(method.ReturnType),
                StubParameters = stubParams,
                StubCode = new[] { dispatchCall.ToString() },
                DllImportReturnType = ComputeTypeForDllImport(method.ReturnType),
                DllImportMethodName = dllImportName,
                DllImportParameters = dllImportParams,
                Diagnostics = Enumerable.Empty<Diagnostic>(),
            };
        }

        private static string RefKindToString(RefKind refKind)
        {
            return refKind switch
            {
                RefKind.In => "in ",
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.None => string.Empty,
                _ => throw new NotImplementedException("Support for some RefKind"),
            };
        }

        private static string ComputeTypeForStub(ITypeSymbol type, RefKind refKind = RefKind.None)
        {
            var typeAsString = type.SpecialType switch
            {
                SpecialType.System_Void => "void",
                SpecialType.System_SByte => "sbyte",
                SpecialType.System_Byte => "byte",
                SpecialType.System_Int16 => "short",
                SpecialType.System_UInt16 => "ushort",
                SpecialType.System_Int32 => "int",
                SpecialType.System_UInt32 => "uint",
                SpecialType.System_Int64 => "long",
                SpecialType.System_UInt64 => "ulong",
                SpecialType.System_Single => "float",
                SpecialType.System_Double => "double",
                SpecialType.System_String => "string",
                SpecialType.System_IntPtr => "System.IntPtr",
                SpecialType.System_UIntPtr => "System.UIntPtr",
                _ => null,
            };

            var typePrefix = string.Empty;
            if (typeAsString is null)
            {
                // Determine the namespace
                if (!(type.ContainingNamespace is null)
                    && !type.ContainingNamespace.IsGlobalNamespace)
                {
                    typePrefix = $"{type.ContainingNamespace}{Type.Delimiter}";
                }

                typeAsString = type.ToString();
            }

            string refKindAsString = RefKindToString(refKind);
            return $"{refKindAsString}{typePrefix}{typeAsString}";
        }

        private static string ComputeTypeForDllImport(ITypeSymbol type, RefKind refKind = RefKind.None)
        {
#if GENERATE_FORWARDER
            return ComputeTypeForStub(type, refKind);
#else
            if (!type.IsUnmanagedType)
            {
                return "void*";
            }

            return type.SpecialType switch
            {
                SpecialType.System_Void => "void",
                SpecialType.System_SByte => "sbyte",
                SpecialType.System_Byte => "byte",
                SpecialType.System_Int16 => "short",
                SpecialType.System_UInt16 => "ushort",
                SpecialType.System_Int32 => "int",
                SpecialType.System_UInt32 => "uint",
                SpecialType.System_Int64 => "long",
                SpecialType.System_UInt64 => "ulong",
                SpecialType.System_Single => "float",
                SpecialType.System_Double => "double",
                SpecialType.System_String => "char*", // [TODO] Consider encoding here
                SpecialType.System_IntPtr => "void*",
                SpecialType.System_UIntPtr => "void*",
                _ => "void*",
            };
#endif
        }
    }
}
