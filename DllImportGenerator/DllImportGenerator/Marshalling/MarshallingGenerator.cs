﻿using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Interop
{
    /// <summary>
    /// Interface for generation of marshalling code for P/Invoke stubs
    /// </summary>
    internal interface IMarshallingGenerator
    {
        /// <summary>
        /// Get the native type syntax for <paramref name="info"/>
        /// </summary>
        /// <param name="info">Object to marshal</param>
        /// <returns>Type syntax for the native type representing <paramref name="info"/></returns>
        TypeSyntax AsNativeType(TypePositionInfo info);

        /// <summary>
        /// Get the <paramref name="info"/> as a parameter of the P/Invoke declaration
        /// </summary>
        /// <param name="info">Object to marshal</param>
        /// <returns>Parameter syntax for <paramref name="info"/></returns>
        ParameterSyntax AsParameter(TypePositionInfo info);

        /// <summary>
        /// Get the <paramref name="info"/> as an argument to be passed to the P/Invoke
        /// </summary>
        /// <param name="info">Object to marshal</param>
        /// <param name="context">Code generation context</param>
        /// <returns>Argument syntax for <paramref name="info"/></returns>
        ArgumentSyntax AsArgument(TypePositionInfo info, StubCodeContext context);

        /// <summary>
        /// Generate code for marshalling
        /// </summary>
        /// <param name="info">Object to marshal</param>
        /// <param name="context">Code generation context</param>
        /// <returns>List of statements to be added to the P/Invoke stub</returns>
        /// <remarks>
        /// The generator should return the appropriate statements based on the
        /// <see cref="StubCodeContext.CurrentStage" /> of <paramref name="context"/>.
        /// For <see cref="StubCodeContext.Stage.Pin"/>, any statements not of type
        /// <see cref="FixedStatementSyntax"/> will be ignored.
        /// </remarks>
        IEnumerable<StatementSyntax> Generate(TypePositionInfo info, StubCodeContext context);
    }

    internal class MarshallingGenerators
    {
        public static readonly CBoolMarshaller CBool = new CBoolMarshaller();
        public static readonly WinBoolMarshaller WinBool = new WinBoolMarshaller();
        public static readonly VariantBoolMarshaller VariantBool = new VariantBoolMarshaller();
        public static readonly Forwarder Forwarder = new Forwarder();
        public static readonly BlittableMarshaller Blittable = new BlittableMarshaller();

        public static bool TryCreate(TypePositionInfo info, out IMarshallingGenerator generator)
        {
#if GENERATE_FORWARDER
            generator = MarshallingGenerators.Forwarder;
            return true;
#else
            if (info.IsNativeReturnPosition && !info.IsManagedReturnPosition)
            {
                // [TODO] Use marshaller for native HRESULT return / exception throwing
                // Debug.Assert(info.ManagedType.SpecialType == SpecialType.System_Int32)
            }

            switch (info)
            {
                case { MarshallingAttributeInfo : BlittableTypeAttributeInfo _ }:
                    generator = Blittable;
                    return true;

                case { MarshallingAttributeInfo : NativeMarshallingAttributeInfo { ValuePropertyType : null, NativeMarshallingType : {} nativeType} }:
                    // Simple marshalling with new attribute model
                    generator = null;
                    return true;
                
                case { MarshallingAttributeInfo: NativeMarshallingAttributeInfo { ValuePropertyType : {} nativeType } }:
                    // Marshalling with Value property (and possibly GetPinnableReference) in new model
                    generator = null;
                    return true;

                case { MarshallingAttributeInfo: GeneratedNativeMarshallingAttributeInfo { NativeMarshallingFullyQualifiedTypeName: string name } }:
                    // Simple marshalling with new attribute model, only have type name.
                    generator = null;
                    return true;
                case { ManagedType : { SpecialType: SpecialType.System_SByte }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.I1 }}
                    or { ManagedType : { SpecialType: SpecialType.System_Byte }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.U1 }}
                    or { ManagedType : { SpecialType: SpecialType.System_Int16 }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.I2 }}
                    or { ManagedType : { SpecialType: SpecialType.System_UInt16 }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.U2 }}
                    or { ManagedType : { SpecialType: SpecialType.System_Int32 }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.I4 }}
                    or { ManagedType : { SpecialType: SpecialType.System_UInt32 }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.U4 }}
                    or { ManagedType : { SpecialType: SpecialType.System_Int64 }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.I8 }}
                    or { ManagedType : { SpecialType: SpecialType.System_UInt64 }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.U8 }}
                    or { ManagedType : { SpecialType: SpecialType.System_IntPtr }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 }}
                    or { ManagedType : { SpecialType: SpecialType.System_UIntPtr }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 }}
                    or { ManagedType : { SpecialType: SpecialType.System_Single }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.R4 }}
                    or { ManagedType : { SpecialType: SpecialType.System_Double }, MarshallingAttributeInfo: null or MarshalAsInfo { UnmanagedType: 0 or UnmanagedType.R8 }}:
                    // Blittable primitives with no marshalling info or with a compatible [MarshalAs] attribute.
                    generator = Blittable;
                    return true;
                
                case { ManagedType: { SpecialType : SpecialType.System_Boolean }}:
                    switch (info.MarshallingAttributeInfo)
                    {
                        case null or MarshalAsInfo { UnmanagedType: 0 }:
                            generator = CBool;
                            return true;
                        case MarshalAsInfo { UnmanagedType: UnmanagedType.I1 or UnmanagedType.U1 }:
                            generator = CBool;
                            return true;
                        case MarshalAsInfo { UnmanagedType: UnmanagedType.I4 or UnmanagedType.U4 }:
                            generator = WinBool;
                            return true;
                        case MarshalAsInfo { UnmanagedType: UnmanagedType.VariantBool }:
                            generator = VariantBool;
                            return true;
                    }
                    generator = null;
                    return false;

                default:
                    generator = null;
                    return false;
            }
#endif
        }
    }
}
