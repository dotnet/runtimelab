using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace DllImportGenerator.Tools
{
    /// <summary>
    /// P/Invoke method information from assembly metadata
    /// </summary>
    public record PInvokeMethod
    {
        public string EnclosingTypeName { get; init; }
        public string MethodName { get; init; }

        public CharSet CharSet { get; init; }
        public bool PreserveSig { get; init; }
        public bool SetLastError { get; init; }
        public bool BestFitMapping { get; init; }
        public bool ThrowOnUnmappableChar { get; init; }

        public ParameterInfo ReturnType { get; init; }
        public List<ParameterInfo> ArgumentTypes { get; init; }
    }

    public class NotSupportedPInvokeException : Exception
    {
        public string AssemblyPath { get; init; }
        public string MethodName { get; init; }

        public NotSupportedPInvokeException(string assemblyPath, string methodName, string message)
            : base(message)
        {
            this.AssemblyPath = assemblyPath;
            this.MethodName = methodName;
        }
    }

    /// <summary>
    /// Class for processing assemblies to retrieve information about their P/Invokes
    /// </summary>
    public sealed partial class PInvokeDump
    {
        private readonly TypeProvider typeProvider = new TypeProvider();
        private readonly Dictionary<string, IReadOnlyCollection<PInvokeMethod>> methodsByAssemblyPath = new Dictionary<string, IReadOnlyCollection<PInvokeMethod>>();
        private readonly HashSet<string> allTypeNames = new HashSet<string>();

        public IReadOnlySet<string> AllTypeNames => allTypeNames;
        public IReadOnlyDictionary<string, IReadOnlyCollection<PInvokeMethod>> MethodsByAssemblyPath => methodsByAssemblyPath;
        public int Count { get; private set; }

        /// <summary>
        /// Process an assembly
        /// </summary>
        /// <param name="assemblyFile">Assembly to process</param>
        /// <returns>
        /// hasMetadata: True if the assembly has metadata.
        /// count: Number of P/Invoke methods found in the assembly.
        /// </returns>
        public (bool hasMetadata, int count) Process(FileInfo assemblyFile)
        {
            using var peReader = new PEReader(assemblyFile.OpenRead());
            if (!peReader.HasMetadata)
                return (false, 0);
            
            MetadataReader mdReader = peReader.GetMetadataReader(MetadataReaderOptions.None);
            List<PInvokeMethod> pinvokeMethods = new List<PInvokeMethod>();
            foreach (var methodDefHandle in mdReader.MethodDefinitions)
            {
                MethodDefinition methodDef = mdReader.GetMethodDefinition(methodDefHandle);

                // Not a P/Invoke.
                if (!methodDef.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
                    continue;

                MethodImport methodImp = methodDef.GetImport();
                string methodName = mdReader.GetString(methodDef.Name);

                // Process method signature
                MethodSignature<ParameterInfo> signature;
                try
                {
                    signature = methodDef.DecodeSignature(this.typeProvider, null);
                }
                catch (NotSupportedTypeException e)
                {
                    throw new NotSupportedPInvokeException(assemblyFile.FullName, methodName, $"Method '{methodName}' has unsupported type '{e.Type}'");
                }

                // Process method details
                MethodImportAttributes impAttr = methodImp.Attributes;
                CharSet charSet = (impAttr & MethodImportAttributes.CharSetMask) switch
                {
                    MethodImportAttributes.CharSetAnsi => CharSet.Ansi,
                    MethodImportAttributes.CharSetAuto => CharSet.Auto,
                    MethodImportAttributes.CharSetUnicode => CharSet.Unicode,
                    _ => CharSet.None
                };
                TypeDefinition typeDef = mdReader.GetTypeDefinition(methodDef.GetDeclaringType());
                var method = new PInvokeMethod()
                {
                    EnclosingTypeName = GetTypeDefinitionFullName(mdReader, typeDef),
                    MethodName = methodName,
                    CharSet = charSet,
                    PreserveSig = (methodDef.ImplAttributes & MethodImplAttributes.PreserveSig) != 0,
                    SetLastError = (impAttr & MethodImportAttributes.SetLastError) != 0,
                    BestFitMapping = (impAttr & MethodImportAttributes.BestFitMappingMask) == MethodImportAttributes.BestFitMappingEnable,
                    ThrowOnUnmappableChar = (impAttr & MethodImportAttributes.ThrowOnUnmappableCharMask) == MethodImportAttributes.ThrowOnUnmappableCharEnable,
                    ReturnType = signature.ReturnType,
                    ArgumentTypes = signature.ParameterTypes.ToList(),
                };

                // Track all types - just uses the full name, ignoring assembly
                allTypeNames.Add(signature.ReturnType.Name);
                allTypeNames.UnionWith(signature.ParameterTypes.Select(t => t.Name));

                // Process marshalling descriptors
                foreach (var paramHandle in methodDef.GetParameters())
                {
                    Parameter param = mdReader.GetParameter(paramHandle);
                    bool isReturn = param.SequenceNumber == 0;
                    BlobHandle marshallingInfo = param.GetMarshallingDescriptor();

                    MarshalAsAttribute marshalAs = null;
                    if (!marshallingInfo.IsNil)
                    {
                        BlobReader br = mdReader.GetBlobReader(marshallingInfo);

                        // Just reads the unmanaged type, ignoring any other data
                        var unmanagedType = (UnmanagedType)br.ReadByte();
                        marshalAs = new MarshalAsAttribute(unmanagedType);
                    }

                    if (isReturn)
                    {
                        method.ReturnType.MarshalAsInfo = marshalAs;
                    }
                    else
                    {
                        method.ArgumentTypes[param.SequenceNumber - 1].MarshalAsInfo = marshalAs; 
                    }
                }

                pinvokeMethods.Add(method);
            }

            methodsByAssemblyPath.Add(assemblyFile.FullName, pinvokeMethods);
            Count += pinvokeMethods.Count;
            return (true, pinvokeMethods.Count);
        }

        private static string GetTypeDefinitionFullName(MetadataReader reader, TypeDefinition typeDef)
        {
            var enclosingTypes = new List<string>() { reader.GetString(typeDef.Name) };
            TypeDefinition parentTypeDef = typeDef;
            while (parentTypeDef.IsNested)
            {
                parentTypeDef = reader.GetTypeDefinition(parentTypeDef.GetDeclaringType());
                enclosingTypes.Add(reader.GetString(parentTypeDef.Name));
            }

            enclosingTypes.Reverse();
            string name = string.Join(Type.Delimiter, enclosingTypes);
            if (!parentTypeDef.Namespace.IsNil)
                name = $"{reader.GetString(parentTypeDef.Namespace)}{Type.Delimiter}{name}";

            return name;
        }
    }
}
