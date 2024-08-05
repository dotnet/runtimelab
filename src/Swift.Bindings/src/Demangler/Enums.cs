// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace BindingsGeneration.Demangling;

/// <Summary>
/// Represents the kind of a node
/// </Summary>
public enum NodeKind {
	[Context]
	Allocator,
	[Context]
	AnonymousContext,
	AnyProtocolConformanceList,
	ArgumentTuple,
	AssociatedType,
	AssociatedTypeRef,
	AssociatedTypeMetadataAccessor,
	DefaultAssociatedTypeMetadataAccessor,
	AssociatedTypeWitnessTableAccessor,
	BaseWitnessTableAccessor,
	AutoClosureType,
	BoundGenericClass,
	BoundGenericEnum,
	BoundGenericStructure,
	BoundGenericProtocol,
	BoundGenericOtherNominalType,
	BoundGenericTypeAlias,
	BoundGenericFunction,
	BuiltinTypeName,
	CFunctionPointer,
	[Context]
	Class,
	ClassMetadataBaseOffset,
	ConcreteProtocolConformance,
	[Context]
	Constructor,
	CoroutineContinuationPrototype,
	[Context]
	Deallocator,
	DeclContext,
	[Context]
	DefaultArgumentInitializer,
	DependentAssociatedConformance,
	DependentAssociatedTypeRef,
	DependentGenericConformanceRequirement,
	DependentGenericParamCount,
	DependentGenericParamType,
	DependentGenericSameTypeRequirement,
	DependentGenericLayoutRequirement,
	DependentGenericSignature,
	DependentGenericType,
	DependentMemberType,
	DependentPseudogenericSignature,
	DependentProtocolConformanceRoot,
	DependentProtocolConformanceInherited,
	DependentProtocolConformanceAssociated,
	[Context]
	Destructor,
	[Context]
	DidSet,
	Directness,
	DynamicAttribute,
	DirectMethodReferenceAttribute,
	DynamicSelf,
	DynamicallyReplaceableFunctionImpl,
	DynamicallyReplaceableFunctionKey,
	DynamicallyReplaceableFunctionVar,
	[Context]
	Enum,
	EnumCase,
	ErrorType,
	EscapingAutoClosureType,
	NoEscapeFunctionType,
	ExistentialMetatype,
	[Context]
	ExplicitClosure,
	[Context]
	Extension,
	FieldOffset,
	FullTypeMetadata,
	[Context]
	Function,
	FunctionSignatureSpecialization,
	FunctionSignatureSpecializationParam,
	FunctionSignatureSpecializationParamKind,
	FunctionSignatureSpecializationParamPayload,
	FunctionType,
	GenericPartialSpecialization,
	GenericPartialSpecializationNotReAbstracted,
	GenericProtocolWitnessTable,
	GenericProtocolWitnessTableInstantiationFunction,
	ResilientProtocolWitnessTable,
	GenericSpecialization,
	GenericSpecializationNotReAbstracted,
	GenericSpecializationParam,
	InlinedGenericFunction,
	GenericTypeMetadataPattern,
	[Context]
	Getter,
	Global,
	[Context]
	GlobalGetter,
	Identifier,
	Index,
	[Context]
	IVarInitializer,
	[Context]
	IVarDestroyer,
	ImplEscaping,
	ImplConvention,
	ImplFunctionAttribute,
	ImplFunctionType,
	[Context]
	ImplicitClosure,
	ImplParameter,
	ImplResult,
	ImplErrorResult,
	InOut,
	InfixOperator,
	[Context]
	Initializer,
	KeyPathGetterThunkHelper,
	KeyPathSetterThunkHelper,
	KeyPathEqualsThunkHelper,
	KeyPathHashThunkHelper,
	LazyProtocolWitnessTableAccessor,
	LazyProtocolWitnessTableCacheVariable,
	LocalDeclName,
	[Context]
	MaterializeForSet,
	MergedFunction,
	Metatype,
	MetatypeRepresentation,
	Metaclass,
	MethodLookupFunction,
	ObjCMetadataUpdateFunction,
	[Context]
	ModifyAccessor,
	[Context]
	Module,
	[Context]
	NativeOwningAddressor,
	[Context]
	NativeOwningMutableAddressor,
	[Context]
	NativePinningAddressor,
	[Context]
	NativePinningMutableAddressor,
	NominalTypeDescriptor,
	NonObjCAttribute,
	Number,
	ObjCAttribute,
	ObjCBlock,
	[Context]
	OtherNominalType,
	[Context]
	OwningAddressor,
	[Context]
	OwningMutableAddressor,
	PartialApplyForwarder,
	PartialApplyObjCForwarder,
	PostfixOperator,
	PrefixOperator,
	PrivateDeclName,
	PropertyDescriptor,
	[Context]
	Protocol,
	[Context]
	ProtocolSymbolicReference,
	ProtocolConformance,
	ProtocolConformanceRefInTypeModule,
	ProtocolConformanceRefInProtocolModule,
	ProtocolConformanceRefInOtherModule,
	ProtocolDescriptor,
	ProtocolConformanceDescriptor,
	ProtocolList,
	ProtocolListWithClass,
	ProtocolListWithAnyObject,
	ProtocolSelfConformanceDescriptor,
	ProtocolSelfConformanceWitness,
	ProtocolSelfConformanceWitnessTable,
	ProtocolWitness,
	ProtocolWitnessTable,
	ProtocolWitnessTableAccessor,
	ProtocolWitnessTablePattern,
	ReabstractionThunk,
	ReabstractionThunkHelper,
	[Context]
	ReadAccessor,
	RelatedEntityDeclName,
	RetroactiveConformance,
	ReturnType,
	Shared,
	Owned,
	SILBoxType,
	SILBoxTypeWithLayout,
	SILBoxLayout,
	SILBoxMutableField,
	SILBoxImmutableField,
	[Context]
	Setter,
	SpecializationPassID,
	SpecializationIsFragile,
	[Context]
	Static,
	[Context]
	Structure,
	[Context]
	Subscript,
	Suffix,
	ThinFunctionType,
	Tuple,
	TupleElement,
	TupleElementName,
	Type,
	[Context]
	TypeSymbolicReference,
	[Context]
	TypeAlias,
	TypeList,
	TypeMangling,
	TypeMetadata,
	TypeMetadataAccessFunction,
	TypeMetadataCompletionFunction,
	TypeMetadataInstantiationCache,
	TypeMetadataInstantiationFunction,
	TypeMetadataSingletonInitializationCache,
	TypeMetadataLazyCache,
	Unowned,
	UncurriedFunctionType,
	Weak,

	Unmanaged,
	[Context]
	UnsafeAddressor,
	[Context]
	UnsafeMutableAddressor,
	ValueWitness,
	ValueWitnessTable,
	[Context]
	Variable,
	VTableThunk,
	VTableAttribute, // note: old mangling only
	[Context]
	WillSet,
	ReflectionMetadataBuiltinDescriptor,
	ReflectionMetadataFieldDescriptor,
	ReflectionMetadataAssocTypeDescriptor,
	ReflectionMetadataSuperclassDescriptor,
	GenericTypeParamDecl,
	CurryThunk,
	DispatchThunk,
	MethodDescriptor,
	ProtocolRequirementsBaseDescriptor,
	AssociatedConformanceDescriptor,
	DefaultAssociatedConformanceAccessor,
	BaseConformanceDescriptor,
	AssociatedTypeDescriptor,
	ThrowsAnnotation,
	EmptyList,
	FirstElementMarker,
	VariadicMarker,
	OutlinedBridgedMethod,
	OutlinedCopy,
	OutlinedConsume,
	OutlinedRetain,
	OutlinedRelease,
	OutlinedInitializeWithTake,
	OutlinedInitializeWithCopy,
	OutlinedAssignWithTake,
	OutlinedAssignWithCopy,
	OutlinedDestroy,
	OutlinedVariable,
	AssocTypePath,
	LabelList,
	ModuleDescriptor,
	ExtensionDescriptor,
	AnonymousDescriptor,
	AssociatedTypeGenericParamRef,
}

/// <Summary>
/// Represents the kind of a node's payload
/// </Summary>
public enum PayloadKind {
	None,
	Text,
	Index,
}

/// <Summary>
/// Represents the variety of a generic type 
/// </Summary>
internal enum GenericTypeKind {
	Generic,
	Assoc,
	CompoundAssoc,
	Substitution,
}

internal enum GenericConstraintKind {
	Protocol,
	BaseClass,
	SameType,
	Layout,
}

internal enum FunctionSigSpecializationParamKind : uint {
	ConstantPropFunction = 0,
	ConstantPropGlobal = 1,
	ConstantPropInteger = 2,
	ConstantPropFloat = 3,
	ConstantPropString = 4,
	ClosureProp = 5,
	BoxToValue = 6,
	BoxToStack = 7,

	// Option Set Flags use bits 6-31. This gives us 26 bits to use for option
	// flags.
	Dead = 1 << 6,
	OwnedToGuaranteed = 1 << 7,
	SROA = 1 << 8,
	GuaranteedToOwned = 1 << 9,
	ExistentialToGeneric = 1 << 10,
}

/// <Summary>
/// Represents the type of directness
/// </Summary>
public enum Directness {
	Direct,
	Indirect,
}

internal enum FunctionEntityArgs {
	None,
	Type,
	TypeAndName,
	TypeAndMaybePrivateName,
	TypeAndIndex,
	Index,
}

internal enum ValueWitnessKind {
	AllocateBuffer,
	AssignWithCopy,
	AssignWithTake,
	DeallocateBuffer,
	Destroy,
	DestroyBuffer,
	DestroyArray,
	InitializeBufferWithCopyOfBuffer,
	InitializeBufferWithCopy,
	InitializeWithCopy,
	InitializeBufferWithTake,
	InitializeWithTake,
	ProjectBuffer,
	InitializeBufferWithTakeOfBuffer,
	InitializeArrayWithCopy,
	InitializeArrayWithTakeFrontToBack,
	InitializeArrayWithTakeBackToFront,
	StoreExtraInhabitant,
	GetExtraInhabitantIndex,
	GetEnumTag,
	DestructiveProjectEnumData,
	DestructiveInjectEnumTag,
	GetEnumTagSinglePayload,
	StoreEnumTagSinglePayload
}

internal enum MatchNodeContentType {
	None,
	Index,
	Text,
	AlwaysMatch,
}

public enum SymbolicReferenceKind {
	Context,
}
