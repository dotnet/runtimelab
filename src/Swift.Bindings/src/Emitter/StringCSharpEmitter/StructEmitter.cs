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
        /// <summary>
        /// Emits a struct declaration.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="moduleDecl">The module declaration.</param>
        /// <param name="parentDecl">The parent declaration.</param>
        /// <param name="decl">The struct declaration.</param>
        private unsafe void EmitStruct(IndentedTextWriter writer, ModuleDecl moduleDecl, BaseDecl parentDecl, StructDecl structDecl)
        {
            // Retrieve type info from the type database
            var typeRecord = _typeDatabase.Registrar.GetType(moduleDecl.Name, structDecl.Name);
            SwiftTypeInfo? swiftTypeInfo = typeRecord?.SwiftTypeInfo;

            if (swiftTypeInfo.HasValue)
            {
                // Apply struct layout attributes
                writer.WriteLine($"[StructLayout(LayoutKind.Sequential, Size = {swiftTypeInfo.Value.ValueWitnessTable->Size})]");
            }
            writer.WriteLine($"public unsafe struct {structDecl.Name} {{");
            writer.Indent++;

            // Emit each field in the struct
            foreach (var fieldDecl in structDecl.Fields)
            {
                string accessModifier = fieldDecl.Visibility == Visibility.Public ? "public" : "private";
                writer.WriteLine($"{accessModifier} {fieldDecl.TypeIdentifier.Name} {fieldDecl.Name};");

                // TODO: Fix memory access violation
                // // Verify field against Swift type information
                // if (swiftTypeInfo.HasValue && !VerifyFieldRecord(swiftTypeInfo.Value, structDecl.Fields.IndexOf(fieldDecl), fieldDecl))
                // {
                //     Console.WriteLine("Field record does not match the field declaration");
                // }
            }
            writer.WriteLine();

            foreach (BaseDecl baseDecl in structDecl.Declarations)
                EmitBaseDecl(writer, moduleDecl, structDecl, baseDecl);
            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// Verify field record with the Swift type information.
        /// </summary>
        private unsafe bool VerifyFieldRecord(SwiftTypeInfo swiftTypeInfo, int fieldIndex, FieldDecl fieldDecl)
        {
            // Access the field descriptor using pointer arithmetic
            FieldDescriptor* desc = (FieldDescriptor*)IntPtr.Add(
                (IntPtr)(((StructDescriptor*)swiftTypeInfo.Metadata->TypeDescriptor))->NominalType.FieldsPtr.Target,
                IntPtr.Size * fieldIndex
            );

            // Ensure the field number is within bounds
            if (desc->NumFields <= fieldIndex)
            {
                return false;
            }

            FieldRecord* fieldRecord = desc->GetFieldRecord(fieldIndex);

            // Check field name
            if ((Marshal.PtrToStringAnsi((IntPtr)fieldRecord->Name.Target) ?? string.Empty) != fieldDecl.Name)
            {
                return false;
            }

            return true;
        }
    }
}
