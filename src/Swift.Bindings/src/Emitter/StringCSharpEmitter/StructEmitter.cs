// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace BindingsGeneration
{
    public partial class StringCSharpEmitter : ICSharpEmitter
    {
        // TODO: Update this method to support non-frozen structs
        /// <summary>
        /// Emits a struct declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="decl">The struct declaration.</param>
        private unsafe void EmitStruct(IndentedTextWriter writer, ModuleDecl moduleDecl, BaseDecl parentDecl, StructDecl structDecl)
        {
            SwiftTypeInfo swiftTypeInfo = _typeDatabase.GetSwiftTypeInfo(structDecl.Name);

            writer.WriteLine($"[StructLayout(LayoutKind.Sequential, Size = {swiftTypeInfo.ValueWitnessTable->Size})]");
            writer.WriteLine($"public unsafe struct {structDecl.Name} {{");
            writer.Indent++;

            // Project fields for frozen structs
            FieldDescriptor* desc = (FieldDescriptor*)IntPtr.Add((IntPtr)(((StructDescriptor*)swiftTypeInfo.Metadata->TypeDescriptor))->NominalType.FieldsPtr.Target, IntPtr.Size);
            for (int i = 0; i < desc->NumFields; i++)
            {
                FieldRecord* fieldRecord = desc->GetFieldRecord(i);
                string fieldName = Marshal.PtrToStringAnsi((IntPtr)fieldRecord->Name.Target) ?? string.Empty;
                string fieldType = string.Empty;

                IntPtr typeContextDescriptor = fieldRecord->GetContextDescriptorAddress();
                if (typeContextDescriptor != IntPtr.Zero)
                {
                    string swiftTypeName = ((StructDescriptor*)typeContextDescriptor)->NominalType.Name ?? string.Empty;
                    string [] csharpType = _typeDatabase.GetCSharpTypeName(swiftTypeName);
                    fieldType = csharpType[1];
                } else {
                    string mangledName = fieldRecord->GetMangledNameSymbol();
                    Console.WriteLine("Mangled name: " + mangledName);
                    // TODO: Resolve metadata and get the type name
                }

                Debug.Assert(!string.IsNullOrEmpty(fieldType), $"Field type is empty in {structDecl.Name} declaration");
                Debug.Assert(!string.IsNullOrEmpty(fieldName), $"Field name is empty in {structDecl.Name} declaration");

                writer.WriteLine($"public {fieldType} {fieldName};");
            }

            writer.WriteLine();

            foreach (BaseDecl baseDecl in structDecl.Declarations)
                EmitBaseDecl(writer, moduleDecl, structDecl, baseDecl);
            writer.Indent--;
            writer.WriteLine("}");
        }
    }
}
