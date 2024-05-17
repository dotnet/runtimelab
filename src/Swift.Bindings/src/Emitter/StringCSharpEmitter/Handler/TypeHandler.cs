// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CodeDom.Compiler;
using Swift.Runtime;

namespace BindingsGeneration
{
    /// <summary>
    /// Factory class for creating instances of StructHandler.
    /// </summary>
    public class StructHandlerFactory : IFactory<BaseDecl, ITypeHandler>
    {
         /// <summary>
        /// Determines if the factory handles the specified declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        public bool Handles(BaseDecl decl)
        {
            return decl is TypeDecl && decl is StructDecl;
        }

        /// <summary>
        /// Constructs a new instance of StructHandler.
        /// </summary>
        public ITypeHandler Construct ()
        {
            return new StructHandler();
        }
    }

    /// <summary>
    /// Handler class for struct declarations.
    /// </summary>
    public class StructHandler : BaseHandler, ITypeHandler
    {
        public StructHandler ()
        {
        }

        /// <summary>
        /// Marshals the specified struct declaration.
        /// </summary>
        /// <param name="structDecl">The struct declaration.</param>
        public IEnvironment Marshal(BaseDecl structDecl)
        {
            return new TypeEnvironment(structDecl);
        }

        /// <summary>
        /// Emits the code for the specified environment.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor, TypeDatabase typeDatabase)
        {
            var structEnv = (TypeEnvironment)env;
            var structDecl = (StructDecl)structEnv.TypeDecl;
            var parentDecl = structDecl.ParentDecl ?? throw new ArgumentNullException(nameof(structDecl.ParentDecl));
            var moduleDecl = structDecl.ModuleDecl ?? throw new ArgumentNullException(nameof(structDecl.ParentDecl));
            // Retrieve type info from the type database
            var typeRecord = typeDatabase.Registrar.GetType(moduleDecl.Name, structDecl.Name);
            SwiftTypeInfo? swiftTypeInfo = typeRecord?.SwiftTypeInfo;

            if (swiftTypeInfo.HasValue)
            {
                unsafe
                {
                    // Apply struct layout attributes
                    writer.WriteLine($"[StructLayout(LayoutKind.Sequential, Size = {swiftTypeInfo.Value.ValueWitnessTable->Size})]");
                }
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
                base.HandleBaseDecl(writer, baseDecl, conductor, typeDatabase);
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
            if ((System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)fieldRecord->Name.Target) ?? string.Empty) != fieldDecl.Name)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Factory class for creating instances of ClassHandler.
    /// </summary>
    public class ClassHandlerFactory : IFactory<BaseDecl, ITypeHandler>
    {
        /// <summary>
        /// Determines if the factory handles the specified declaration.
        /// </summary>
        /// <param name="decl">The base declaration.</param>
        public bool Handles(BaseDecl decl)
        {
            return decl is TypeDecl && decl is ClassDecl;
        }

        /// <summary>
        /// Constructs a new instance of ClassHandler.
        /// </summary>
        public ITypeHandler Construct ()
        {
            return new ClassHandler();
        }
    }

    /// <summary>
    /// Handler class for class declarations.
    /// </summary>
    public class ClassHandler : BaseHandler, ITypeHandler
    {
        public ClassHandler ()
        {
        }

         /// <summary>
        /// Marshals the specified class declaration.
        /// </summary>
        /// <param name="classDecl">The class declaration.</param>
        public IEnvironment Marshal(BaseDecl classDecl)
        {
            return new TypeEnvironment(classDecl);
        }

        /// <summary>
        /// Emits the necessary code for the specified environment.
        /// </summary>
        /// <param name="writer">The IndentedTextWriter instance.</param>
        /// <param name="env">The environment.</param>
        /// <param name="conductor">The conductor instance.</param>
        /// <param name="typeDatabase">The type database instance.</param>
        public void Emit(IndentedTextWriter writer, IEnvironment env, Conductor conductor, TypeDatabase typeDatabase)
        {
            var classEnv = (TypeEnvironment)env;
            var classDecl = (ClassDecl)classEnv.TypeDecl;
            
            writer.WriteLine($"public unsafe class {classDecl.Name} {{");
            writer.Indent++;
            foreach (BaseDecl baseDecl in classDecl.Declarations)
                base.HandleBaseDecl(writer, baseDecl, conductor, typeDatabase);
            writer.Indent--;
            writer.WriteLine("}");
        }
    }
}
