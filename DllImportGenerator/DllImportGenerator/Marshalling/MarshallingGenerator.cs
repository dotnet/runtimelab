﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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

        /// <summary>
        /// Returns whether or not this marshaller uses an identifier for the native value in addition
        /// to an identifer for the managed value.
        /// </summary>
        /// <param name="info">Object to marshal</param>
        /// <param name="context">Code generation context</param>
        /// <returns>If the marshaller uses an identifier for the native value, true; otherwise, false.</returns>
        /// <remarks>
        /// <see cref="StubCodeContext.CurrentStage" /> of <paramref name="context"/> may not be valid.
        /// </remarks>
        bool UsesNativeIdentifier(TypePositionInfo info, StubCodeContext context);

        /// <summary>
        /// Returns if the given ByValueContentsMarshalKind is supported in the current marshalling context.
        /// A supported marshal kind has a different behavior than the default behavior.
        /// </summary>
        /// <param name="marshalKind">The marshal kind.</param>
        /// <param name="context">The marshalling context.</param>
        /// <returns></returns>
        bool SupportsByValueMarshalKind(ByValueContentsMarshalKind marshalKind, StubCodeContext context);
    }

    /// <summary>
    /// Interface for generating attributes for native return types.
    /// </summary>
    internal interface IAttributedReturnTypeMarshallingGenerator : IMarshallingGenerator
    {
        /// <summary>
        /// Gets any attributes that should be applied to the return type for this <paramref name="info"/>.
        /// </summary>
        /// <param name="info">Object to marshal</param>
        /// <returns>Attributes for the return type for this <paramref name="info"/>, or <c>null</c> if no attributes should be added.</returns>
        AttributeListSyntax? GenerateAttributesForReturnType(TypePositionInfo info);
    }

    /// <summary>
    /// Exception used to indicate marshalling isn't supported.
    /// </summary>
    internal class MarshallingNotSupportedException : Exception
    {
        /// <summary>
        /// Construct a new <see cref="MarshallingNotSupportedException"/> instance.
        /// </summary>
        /// <param name="info"><see cref="Microsoft.Interop.TypePositionInfo"/> instance</param>
        /// <param name="context"><see cref="Microsoft.Interop.StubCodeContext"/> instance</param>
        public MarshallingNotSupportedException(TypePositionInfo info, StubCodeContext context)
        {
            this.TypePositionInfo = info;
            this.StubCodeContext = context;
        }

        /// <summary>
        /// Type that is being marshalled.
        /// </summary>
        public TypePositionInfo TypePositionInfo { get; private init; }

        /// <summary>
        /// Context in which the marshalling is taking place.
        /// </summary>
        public StubCodeContext StubCodeContext { get; private init; }

        /// <summary>
        /// [Optional] Specific reason marshalling of the supplied type isn't supported.
        /// </summary>
        public string? NotSupportedDetails { get; init; }
    }

    internal class MarshallingGenerators
    {
        public static readonly ByteBoolMarshaller ByteBool = new ByteBoolMarshaller();
        public static readonly WinBoolMarshaller WinBool = new WinBoolMarshaller();
        public static readonly VariantBoolMarshaller VariantBool = new VariantBoolMarshaller();

        public static readonly Utf16CharMarshaller Utf16Char = new Utf16CharMarshaller();
        public static readonly Utf16StringMarshaller Utf16String = new Utf16StringMarshaller();
        public static readonly Utf8StringMarshaller Utf8String = new Utf8StringMarshaller();
        public static readonly AnsiStringMarshaller AnsiString = new AnsiStringMarshaller(Utf8String);
        public static readonly PlatformDefinedStringMarshaller PlatformDefinedString = new PlatformDefinedStringMarshaller(Utf16String, Utf8String);

        public static readonly Forwarder Forwarder = new Forwarder();
        public static readonly BlittableMarshaller Blittable = new BlittableMarshaller();
        public static readonly DelegateMarshaller Delegate = new DelegateMarshaller();
        public static readonly HResultExceptionMarshaller HResultException = new HResultExceptionMarshaller();

        /// <summary>
        /// Create an <see cref="IMarshallingGenerator"/> instance for marshalling the supplied type in the given position.
        /// </summary>
        /// <param name="info">Type details</param>
        /// <param name="context">Metadata about the stub the type is associated with</param>
        /// <returns>A <see cref="IMarshallingGenerator"/> instance.</returns>
        public static IMarshallingGenerator Create(
            TypePositionInfo info,
            StubCodeContext context,
            AnalyzerConfigOptions options)
        {
            return ValidateByValueMarshalKind(context, info, CreateCore(info, context, options));
        }

        private static IMarshallingGenerator ValidateByValueMarshalKind(StubCodeContext context, TypePositionInfo info, IMarshallingGenerator generator)
        {
            if (info.IsByRef && info.ByValueContentsMarshalKind != ByValueContentsMarshalKind.Default)
            {
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = Resources.InOutAttributeByRefNotSupported
                };
            }
            else if (info.ByValueContentsMarshalKind == ByValueContentsMarshalKind.In)
            {
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = Resources.InAttributeNotSupportedWithoutOut
                };
            }
            else if (info.ByValueContentsMarshalKind != ByValueContentsMarshalKind.Default
                && !generator.SupportsByValueMarshalKind(info.ByValueContentsMarshalKind, context))
            {
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = Resources.InOutAttributeMarshalerNotSupported
                };
            }
            return generator;
        }

        /// <summary>
        /// Create an <see cref="IMarshallingGenerator"/> instance to marshalling the supplied type.
        /// </summary>
        /// <param name="info">Type details</param>
        /// <param name="context">Metadata about the stub the type is associated with</param>
        /// <returns>A <see cref="IMarshallingGenerator"/> instance.</returns>
        private static IMarshallingGenerator CreateCore(
            TypePositionInfo info,
            StubCodeContext context,
            AnalyzerConfigOptions options)
        {
            if (options.GenerateForwarders())
            {
                return MarshallingGenerators.Forwarder;
            }

            if (info.IsNativeReturnPosition && !info.IsManagedReturnPosition)
            {
                // Use marshaller for native HRESULT return / exception throwing
                System.Diagnostics.Debug.Assert(info.ManagedType.SpecialType == SpecialType.System_Int32);
                return HResultException;
            }

            switch (info)
            {
                // Blittable primitives with no marshalling info or with a compatible [MarshalAs] attribute.
                case { ManagedType: { SpecialType: SpecialType.System_SByte }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.I1, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_Byte }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.U1, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_Int16 }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.I2, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_UInt16 }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.U2, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_Int32 }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.I4, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_UInt32 }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.U4, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_Int64 }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.I8, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_UInt64 }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.U8, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_IntPtr }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.SysInt, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_UIntPtr }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.SysUInt, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_Single }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.R4, _) }
                    or { ManagedType: { SpecialType: SpecialType.System_Double }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.R8, _) }:
                    return Blittable;

                // Enum with no marshalling info
                case { ManagedType: { TypeKind: TypeKind.Enum }, MarshallingAttributeInfo: NoMarshallingInfo }:
                    // Check that the underlying type is not bool or char. C# does not allow this, but ECMA-335 does.
                    var underlyingSpecialType = ((INamedTypeSymbol)info.ManagedType).EnumUnderlyingType!.SpecialType;
                    if (underlyingSpecialType == SpecialType.System_Boolean || underlyingSpecialType == SpecialType.System_Char)
                    {
                        throw new MarshallingNotSupportedException(info, context);
                    }
                    return Blittable;

                // Pointer with no marshalling info
                case { ManagedType: { TypeKind: TypeKind.Pointer }, MarshallingAttributeInfo: NoMarshallingInfo }:
                    return Blittable;

                // Function pointer with no marshalling info
                case { ManagedType: { TypeKind: TypeKind.FunctionPointer }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.FunctionPtr, _) }:
                    return Blittable;

                case { ManagedType: { SpecialType: SpecialType.System_Boolean }, MarshallingAttributeInfo: NoMarshallingInfo }:
                    return WinBool; // [Compat] Matching the default for the built-in runtime marshallers.
                case { ManagedType: { SpecialType: SpecialType.System_Boolean }, MarshallingAttributeInfo: MarshalAsInfo(UnmanagedType.I1 or UnmanagedType.U1, _) }:
                    return ByteBool;
                case { ManagedType: { SpecialType: SpecialType.System_Boolean }, MarshallingAttributeInfo: MarshalAsInfo(UnmanagedType.I4 or UnmanagedType.U4 or UnmanagedType.Bool, _) }:
                    return WinBool;
                case { ManagedType: { SpecialType: SpecialType.System_Boolean }, MarshallingAttributeInfo: MarshalAsInfo(UnmanagedType.VariantBool, _) }:
                    return VariantBool;

                case { ManagedType: { TypeKind: TypeKind.Delegate }, MarshallingAttributeInfo: NoMarshallingInfo or MarshalAsInfo(UnmanagedType.FunctionPtr, _) }:
                    return Delegate;

                case { MarshallingAttributeInfo: SafeHandleMarshallingInfo }:
                    if (!context.AdditionalTemporaryStateLivesAcrossStages)
                    {
                        throw new MarshallingNotSupportedException(info, context);
                    }
                    if (info.IsByRef && info.ManagedType.IsAbstract)
                    {
                        throw new MarshallingNotSupportedException(info, context)
                        {
                            NotSupportedDetails = Resources.SafeHandleByRefMustBeConcrete
                        };
                    }
                    return new SafeHandleMarshaller(options);

                // Marshalling in new model.
                // Must go before the cases that do not explicitly check for marshalling info to support
                // the user overridding the default marshalling rules with a MarshalUsing attribute.
                case { MarshallingAttributeInfo: NativeMarshallingAttributeInfo marshalInfo }:
                    return CreateCustomNativeTypeMarshaller(info, context, marshalInfo, options);

                case { MarshallingAttributeInfo: BlittableTypeAttributeInfo }:
                    return Blittable;

                // Simple generated marshalling with new attribute model, only have type name.
                case { MarshallingAttributeInfo: GeneratedNativeMarshallingAttributeInfo(string nativeTypeName) }:
                    return Forwarder;

                // Cases that just match on type must come after the checks that match only on marshalling attribute info.
                // The checks below do not account for generic marshalling overrides like [MarshalUsing], so those checks must come first.
                case { ManagedType: { SpecialType: SpecialType.System_Char } }:
                    return CreateCharMarshaller(info, context);

                case { ManagedType: { SpecialType: SpecialType.System_String } }:
                    return CreateStringMarshaller(info, context);

                case { ManagedType: { SpecialType: SpecialType.System_Void } }:
                    return Forwarder;

                default:
                    throw new MarshallingNotSupportedException(info, context);
            }
        }

        private static IMarshallingGenerator CreateCharMarshaller(TypePositionInfo info, StubCodeContext context)
        {
            MarshallingInfo marshalInfo = info.MarshallingAttributeInfo;
            if (marshalInfo is NoMarshallingInfo)
            {
                // [Compat] Require explicit marshalling information.
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = Resources.MarshallingStringOrCharAsUndefinedNotSupported
                };
            }

            // Explicit MarshalAs takes precedence over string encoding info
            if (marshalInfo is MarshalAsInfo marshalAsInfo)
            {
                switch (marshalAsInfo.UnmanagedType)
                {
                    case UnmanagedType.I2:
                    case UnmanagedType.U2:
                        return Utf16Char;
                }
            }
            else if (marshalInfo is MarshallingInfoStringSupport marshalStringInfo)
            {
                switch (marshalStringInfo.CharEncoding)
                {
                    case CharEncoding.Utf16:
                        return Utf16Char;
                    case CharEncoding.Ansi:
                        throw new MarshallingNotSupportedException(info, context) // [Compat] ANSI is not supported for char
                        {
                            NotSupportedDetails = string.Format(Resources.MarshallingCharAsSpecifiedCharSetNotSupported, CharSet.Ansi)
                        };
                    case CharEncoding.PlatformDefined:
                        throw new MarshallingNotSupportedException(info, context) // [Compat] See conversion of CharSet.Auto.
                        {
                            NotSupportedDetails = string.Format(Resources.MarshallingCharAsSpecifiedCharSetNotSupported, CharSet.Auto)
                        };
                }
            }

            throw new MarshallingNotSupportedException(info, context);
        }

        private static IMarshallingGenerator CreateStringMarshaller(TypePositionInfo info, StubCodeContext context)
        {
            MarshallingInfo marshalInfo = info.MarshallingAttributeInfo;
            if (marshalInfo is NoMarshallingInfo)
            {
                // [Compat] Require explicit marshalling information.
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = Resources.MarshallingStringOrCharAsUndefinedNotSupported
                };
            }

            // Explicit MarshalAs takes precedence over string encoding info
            if (marshalInfo is MarshalAsInfo marshalAsInfo)
            {
                switch (marshalAsInfo.UnmanagedType)
                {
                    case UnmanagedType.LPStr:
                        return AnsiString;
                    case UnmanagedType.LPTStr:
                    case UnmanagedType.LPWStr:
                        return Utf16String;
                    case (UnmanagedType)0x30:// UnmanagedType.LPUTF8Str
                        return Utf8String;
                }
            }
            else if (marshalInfo is MarshallingInfoStringSupport marshalStringInfo)
            {
                switch (marshalStringInfo.CharEncoding)
                {
                    case CharEncoding.Ansi:
                        return AnsiString;
                    case CharEncoding.Utf16:
                        return Utf16String;
                    case CharEncoding.Utf8:
                        return Utf8String;
                    case CharEncoding.PlatformDefined:
                        return PlatformDefinedString;
                }
            }

            throw new MarshallingNotSupportedException(info, context);
        }
        
        private static ExpressionSyntax GetNumElementsExpressionFromMarshallingInfo(TypePositionInfo info, CountInfo count, StubCodeContext context, AnalyzerConfigOptions options)
        {
            return count switch
            {
                SizeAndParamIndexInfo(int size, SizeAndParamIndexInfo.UnspecifiedData) => GetConstSizeExpression(size),
                ConstSizeCountInfo(int size) => GetConstSizeExpression(size),
                SizeAndParamIndexInfo(SizeAndParamIndexInfo.UnspecifiedData, int paramIndex) => CheckedExpression(SyntaxKind.CheckedExpression, GetExpressionForParam(context.GetTypePositionInfoForManagedIndex(paramIndex))),
                SizeAndParamIndexInfo(int size, int paramIndex) => CheckedExpression(SyntaxKind.CheckedExpression, BinaryExpression(SyntaxKind.AddExpression, GetConstSizeExpression(size), GetExpressionForParam(context.GetTypePositionInfoForManagedIndex(paramIndex)))),
                CountElementCountInfo(TypePositionInfo elementInfo) => CheckedExpression(SyntaxKind.CheckedExpression, GetExpressionForParam(elementInfo)),
                _ => throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = Resources.ArraySizeMustBeSpecified
                },
            };

            static LiteralExpressionSyntax GetConstSizeExpression(int size)
            {
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(size));
            }

            ExpressionSyntax GetExpressionForParam(TypePositionInfo? paramInfo)
            {
                if (paramInfo is null)
                {
                    throw new MarshallingNotSupportedException(info, context)
                    {
                        NotSupportedDetails = Resources.ArraySizeParamIndexOutOfRange
                    };
                }
                else
                {
                    ExpressionSyntax numElementsExpression = GetIndexedNumElementsExpression(
                        context,
                        paramInfo,
                        out int numIndirectionLevels);

                    ITypeSymbol type = paramInfo.ManagedType;
                    MarshallingInfo marshallingInfo = paramInfo.MarshallingAttributeInfo;

                    for (int i = 0; i < numIndirectionLevels; i++)
                    {
                        if (marshallingInfo is NativeContiguousCollectionMarshallingInfo collectionInfo)
                        {
                            type = collectionInfo.ElementType;
                            marshallingInfo = collectionInfo.ElementMarshallingInfo;
                        }
                        else
                        {
                            throw new MarshallingNotSupportedException(info, context)
                            {
                                NotSupportedDetails = Resources.CollectionSizeParamTypeMustBeIntegral
                            };
                        }
                    }

                    if (!type.IsIntegralType())
                    {
                        throw new MarshallingNotSupportedException(info, context)
                        {
                            NotSupportedDetails = Resources.CollectionSizeParamTypeMustBeIntegral
                        };
                    }

                    return CastExpression(
                            PredefinedType(Token(SyntaxKind.IntKeyword)),
                            ParenthesizedExpression(numElementsExpression));
                }
            }

            static ExpressionSyntax GetIndexedNumElementsExpression(StubCodeContext context, TypePositionInfo numElementsInfo, out int numIndirectionLevels)
            {
                Stack<string> indexerStack = new();

                StubCodeContext? currentContext = context;
                StubCodeContext lastContext = null!;

                while (currentContext is not null)
                {
                    if (currentContext is ContiguousCollectionElementMarshallingCodeContext collectionContext)
                    {
                        indexerStack.Push(collectionContext.IndexerIdentifier);
                    }
                    lastContext = currentContext;
                    currentContext = currentContext.ParentContext;
                }

                numIndirectionLevels = indexerStack.Count;

                ExpressionSyntax indexedNumElements = IdentifierName(lastContext.GetIdentifiers(numElementsInfo).managed);
                while (indexerStack.Count > 0)
                {
                    NameSyntax indexer = IdentifierName(indexerStack.Pop());
                    indexedNumElements = ElementAccessExpression(indexedNumElements)
                        .AddArgumentListArguments(Argument(indexer));
                }

                return indexedNumElements;
            }
        }

        private static IMarshallingGenerator CreateCustomNativeTypeMarshaller(TypePositionInfo info, StubCodeContext context, NativeMarshallingAttributeInfo marshalInfo, AnalyzerConfigOptions options)
        {
            ValidateCustomNativeTypeMarshallingSupported(info, context, marshalInfo);

            ICustomNativeTypeMarshallingStrategy marshallingStrategy = new SimpleCustomNativeTypeMarshalling(marshalInfo.NativeMarshallingType.AsTypeSyntax());

            if ((marshalInfo.MarshallingMethods & SupportedMarshallingMethods.ManagedToNativeStackalloc) != 0)
            {
                marshallingStrategy = new StackallocOptimizationMarshalling(marshallingStrategy);
            }

            if (ManualTypeMarshallingHelper.HasFreeNativeMethod(marshalInfo.NativeMarshallingType))
            {
                marshallingStrategy = new FreeNativeCleanupStrategy(marshallingStrategy);
            }

            // Collections have extra configuration, so handle them here.
            if (marshalInfo is NativeContiguousCollectionMarshallingInfo collectionMarshallingInfo)
            {
                return CreateNativeCollectionMarshaller(info, context, collectionMarshallingInfo, options, marshallingStrategy);
            }

            if (marshalInfo.ValuePropertyType is not null)
            {
                marshallingStrategy = DecorateWithValuePropertyStrategy(marshalInfo, marshallingStrategy);
            }

            IMarshallingGenerator marshallingGenerator = new CustomNativeTypeMarshallingGenerator(marshallingStrategy, enableByValueContentsMarshalling: false);

            if ((marshalInfo.MarshallingMethods & SupportedMarshallingMethods.Pinning) != 0)
            {
                return new PinnableManagedValueMarshaller(marshallingGenerator);
            }

            return marshallingGenerator;
        }

        private static void ValidateCustomNativeTypeMarshallingSupported(TypePositionInfo info, StubCodeContext context, NativeMarshallingAttributeInfo marshalInfo)
        {
            // The marshalling method for this type doesn't support marshalling from native to managed,
            // but our scenario requires marshalling from native to managed.
            if ((info.RefKind == RefKind.Ref || info.RefKind == RefKind.Out || info.IsManagedReturnPosition)
                && (marshalInfo.MarshallingMethods & SupportedMarshallingMethods.NativeToManaged) == 0)
            {
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = string.Format(Resources.CustomTypeMarshallingNativeToManagedUnsupported, marshalInfo.NativeMarshallingType.ToDisplayString())
                };
            }
            // The marshalling method for this type doesn't support marshalling from managed to native by value,
            // but our scenario requires marshalling from managed to native by value.
            else if (!info.IsByRef 
                && (marshalInfo.MarshallingMethods & SupportedMarshallingMethods.ManagedToNative) == 0 
                && (context.SingleFrameSpansNativeContext && (marshalInfo.MarshallingMethods & (SupportedMarshallingMethods.Pinning | SupportedMarshallingMethods.ManagedToNativeStackalloc)) == 0))
            {
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = string.Format(Resources.CustomTypeMarshallingManagedToNativeUnsupported, marshalInfo.NativeMarshallingType.ToDisplayString())
                };
            }
            // The marshalling method for this type doesn't support marshalling from managed to native by reference,
            // but our scenario requires marshalling from managed to native by reference.
            // "in" byref supports stack marshalling.
            else if (info.RefKind == RefKind.In 
                && (marshalInfo.MarshallingMethods & SupportedMarshallingMethods.ManagedToNative) == 0 
                && !(context.SingleFrameSpansNativeContext && (marshalInfo.MarshallingMethods & SupportedMarshallingMethods.ManagedToNativeStackalloc) != 0))
            {
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = string.Format(Resources.CustomTypeMarshallingManagedToNativeUnsupported, marshalInfo.NativeMarshallingType.ToDisplayString())
                };
            }
            // The marshalling method for this type doesn't support marshalling from managed to native by reference,
            // but our scenario requires marshalling from managed to native by reference.
            // "ref" byref marshalling doesn't support stack marshalling
            else if (info.RefKind == RefKind.Ref
                && (marshalInfo.MarshallingMethods & SupportedMarshallingMethods.ManagedToNative) == 0)
            {
                throw new MarshallingNotSupportedException(info, context)
                {
                    NotSupportedDetails = string.Format(Resources.CustomTypeMarshallingManagedToNativeUnsupported, marshalInfo.NativeMarshallingType.ToDisplayString())
                };
            }
        }

        private static ICustomNativeTypeMarshallingStrategy DecorateWithValuePropertyStrategy(NativeMarshallingAttributeInfo marshalInfo, ICustomNativeTypeMarshallingStrategy nativeTypeMarshaller)
        {
            TypeSyntax valuePropertyTypeSyntax = marshalInfo.ValuePropertyType!.AsTypeSyntax();
            if (ManualTypeMarshallingHelper.FindGetPinnableReference(marshalInfo.NativeMarshallingType) is not null)
            {
                return new PinnableMarshallerTypeMarshalling(nativeTypeMarshaller, valuePropertyTypeSyntax);
            }

            return new CustomNativeTypeWithValuePropertyMarshalling(nativeTypeMarshaller, valuePropertyTypeSyntax);
        }

        private static IMarshallingGenerator CreateNativeCollectionMarshaller(
            TypePositionInfo info,
            StubCodeContext context,
            NativeContiguousCollectionMarshallingInfo collectionInfo,
            AnalyzerConfigOptions options,
            ICustomNativeTypeMarshallingStrategy marshallingStrategy)
        {
            var elementInfo = TypePositionInfo.CreateForType(collectionInfo.ElementType, collectionInfo.ElementMarshallingInfo) with { ManagedIndex = info.ManagedIndex };
            var elementMarshaller = Create(
                elementInfo,
                new ContiguousCollectionElementMarshallingCodeContext(StubCodeContext.Stage.Setup, string.Empty, context),
                options);
            var elementType = elementMarshaller.AsNativeType(elementInfo);

            bool isBlittable = elementMarshaller == Blittable;

            if (isBlittable)
            {
                marshallingStrategy = new ContiguousBlittableElementCollectionMarshalling(marshallingStrategy, collectionInfo.ElementType.AsTypeSyntax());
            }
            else
            {
                marshallingStrategy = new ContiguousNonBlittableElementCollectionMarshalling(marshallingStrategy, elementMarshaller, elementInfo);
            }

            // Explicitly insert the Value property handling here (before numElements handling) so that the numElements handling will be emitted before the Value property handling in unmarshalling.
            if (collectionInfo.ValuePropertyType is not null)
            {
                marshallingStrategy = DecorateWithValuePropertyStrategy(collectionInfo, marshallingStrategy);
            }

            ExpressionSyntax numElementsExpression = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
            if (info.IsManagedReturnPosition || (info.IsByRef && info.RefKind != RefKind.In))
            {
                // In this case, we need a numElementsExpression supplied from metadata, so we'll calculate it here.
                numElementsExpression = GetNumElementsExpressionFromMarshallingInfo(info, collectionInfo.ElementCountInfo, context, options);
            }

            marshallingStrategy = new NumElementsExpressionMarshalling(
                marshallingStrategy,
                numElementsExpression,
                SizeOfExpression(elementType));

            if (collectionInfo.UseDefaultMarshalling && info.ManagedType is IArrayTypeSymbol { IsSZArray: true })
            {
                return new ArrayMarshaller(
                    new CustomNativeTypeMarshallingGenerator(marshallingStrategy, enableByValueContentsMarshalling: true),
                    elementType,
                    isBlittable,
                    options);
            }

            IMarshallingGenerator marshallingGenerator = new CustomNativeTypeMarshallingGenerator(marshallingStrategy, enableByValueContentsMarshalling: false);

            if ((collectionInfo.MarshallingMethods & SupportedMarshallingMethods.Pinning) != 0)
            {
                return new PinnableManagedValueMarshaller(marshallingGenerator);
            }

            return marshallingGenerator;
        }
    }
}
