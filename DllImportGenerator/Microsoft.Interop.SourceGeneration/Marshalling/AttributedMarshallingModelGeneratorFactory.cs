using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Microsoft.Interop
{
    public class AttributedMarshallingModelGeneratorFactory : IMarshallingGeneratorFactory
    {
        private static readonly BlittableMarshaller Blittable = new BlittableMarshaller();
        private static readonly Forwarder Forwarder = new Forwarder();

        private readonly IMarshallingGeneratorFactory innerMarshallingGenerator;

        public AttributedMarshallingModelGeneratorFactory(IMarshallingGeneratorFactory innerMarshallingGenerator, InteropGenerationOptions options)
        {
            Options = options;
            this.innerMarshallingGenerator = innerMarshallingGenerator;
            ElementMarshallingGeneratorFactory = this;
        }

        public InteropGenerationOptions Options { get; }

        /// <summary>
        /// The <see cref="IMarshallingGeneratorFactory"/> to use for collection elements.
        /// This property is settable to enable decorating factories to ensure that element marshalling also goes through the decorator support.
        /// </summary>
        public IMarshallingGeneratorFactory ElementMarshallingGeneratorFactory { get; set; }

        public IMarshallingGenerator Create(TypePositionInfo info, StubCodeContext context)
        {
            return info.MarshallingAttributeInfo switch
            {
                NativeMarshallingAttributeInfo marshalInfo => CreateCustomNativeTypeMarshaller(info, context, marshalInfo),
                BlittableTypeAttributeInfo => Blittable,
                GeneratedNativeMarshallingAttributeInfo => Forwarder,
                _ => innerMarshallingGenerator.Create(info, context)
            };
        }


        private ExpressionSyntax GetNumElementsExpressionFromMarshallingInfo(TypePositionInfo info, CountInfo count, StubCodeContext context)
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

        private IMarshallingGenerator CreateCustomNativeTypeMarshaller(TypePositionInfo info, StubCodeContext context, NativeMarshallingAttributeInfo marshalInfo)
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
                return CreateNativeCollectionMarshaller(info, context, collectionMarshallingInfo, marshallingStrategy);
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

        private IMarshallingGenerator CreateNativeCollectionMarshaller(
            TypePositionInfo info,
            StubCodeContext context,
            NativeContiguousCollectionMarshallingInfo collectionInfo,
            ICustomNativeTypeMarshallingStrategy marshallingStrategy)
        {
            var elementInfo = TypePositionInfo.CreateForType(collectionInfo.ElementType, collectionInfo.ElementMarshallingInfo) with { ManagedIndex = info.ManagedIndex };
            var elementMarshaller = ElementMarshallingGeneratorFactory.Create(
                elementInfo,
                new ContiguousCollectionElementMarshallingCodeContext(StubCodeContext.Stage.Setup, string.Empty, context));
            var elementType = elementMarshaller.AsNativeType(elementInfo);

            bool isBlittable = elementMarshaller is BlittableMarshaller;

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
                numElementsExpression = GetNumElementsExpressionFromMarshallingInfo(info, collectionInfo.ElementCountInfo, context);
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
                    Options);
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
