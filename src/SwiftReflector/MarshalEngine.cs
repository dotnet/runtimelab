// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using SyntaxDynamo.CSLang;
using System.Text;
using System.Linq;
using SwiftRuntimeLibrary;
using SwiftReflector.TypeMapping;
using SyntaxDynamo;
using SwiftReflector.SwiftXmlReflection;
using SwiftReflector.Demangling;

namespace SwiftReflector {
	public class MarshalEngine {
		CSUsingPackages use;
		List<string> identifiersUsed;
		TypeMapper typeMapper;
		public bool skipThisParameterPremarshal = false;
		List<CSFixedCodeBlock> fixedChain = new List<CSFixedCodeBlock> ();
		Version swiftLangVersion;
		public Func<int, int, string> genericReferenceNamer = null;

		public MarshalEngine (CSUsingPackages use, List<string> identifiersUsed, TypeMapper typeMapper, Version swiftLangVersion)
		{
			this.use = use;
			this.identifiersUsed = identifiersUsed;
			absolutelyMustBeFirst = new List<CSLine> ();
			preMarshalCode = new List<CSLine> ();
			postMarshalCode = new List<CSLine> ();

			this.typeMapper = typeMapper;
			this.swiftLangVersion = swiftLangVersion;
		}

		public IEnumerable<ICodeElement> MarshalFunctionCall (FunctionDeclaration wrapperFuncDecl, bool isExtension, string pinvokeCall,
								      CSParameterList pl,
								      BaseDeclaration typeContext,
								      TypeSpec swiftReturnType,
								      CSType returnType,
								      TypeSpec swiftInstanceType,
								      CSType instanceType,
								      bool includeCastToReturnType,
								      FunctionDeclaration originalFunction,
								      bool includeIntermediateCastToLong = false,
								      int passedInIndexOfReturn = -1,
								      bool originalThrows = false,
								      bool restoreDynamicSelf = false)
		{
			RequiredUnsafeCode = false;
			preMarshalCode.Clear ();
			postMarshalCode.Clear ();
			fixedChain.Clear ();
			returnLine = null;
			functionCall = null;

			var parms = new CSParameterList (pl); // work with local copy
			CSIdentifier returnIdent = null, returnIntPtr = null, returnProtocol = null;
			var indexOfReturn = passedInIndexOfReturn;

			var originalReturn = swiftReturnType;

			int indexOfInstance = (swiftReturnType != null && (typeMapper.MustForcePassByReference (typeContext, swiftReturnType)) && !swiftReturnType.IsDynamicSelf) || originalThrows ?
				1 : 0;
			var instanceIsSwiftProtocol = false;
			var instanceIsObjC = false;

			if (swiftInstanceType != null) {
				var entity = typeMapper.GetEntityForTypeSpec (swiftInstanceType);
				instanceIsSwiftProtocol = entity.EntityType == EntityType.Protocol;
				instanceIsObjC = entity.Type.IsObjC;
				var thisIdentifier = isExtension ? new CSIdentifier ("self") : CSIdentifier.This;
				parms.Insert (0, new CSParameter (instanceType, thisIdentifier, wrapperFuncDecl.ParameterLists.Last () [indexOfInstance].IsInOut ?
				    CSParameterKind.Ref : CSParameterKind.None));
			}

			var hasReturn = returnType != null && returnType != CSSimpleType.Void;

			if (hasReturn)
				returnType = ReworkTypeWithNamer (returnType);

			var isExtensionMethod = pl.Count () > 0 && pl [0].ParameterKind == CSParameterKind.This;
			var returnIsScalar = returnType != null && TypeMapper.IsScalar (swiftReturnType);
			var returnEntity = hasReturn && !typeContext.IsTypeSpecGenericReference (swiftReturnType) ? typeMapper.GetEntityForTypeSpec (swiftReturnType) : null;
			var returnIsTrivialEnum = hasReturn && returnEntity != null && returnEntity.EntityType == EntityType.TrivialEnum;
			var returnIsGenericClass = hasReturn && returnEntity != null && returnEntity.EntityType == EntityType.Class && swiftReturnType.ContainsGenericParameters;
			var returnIsClass = hasReturn && returnEntity != null && returnEntity.EntityType == EntityType.Class;
			var returnIsNonTrivialTuple = hasReturn && swiftReturnType is TupleTypeSpec && ((TupleTypeSpec)swiftReturnType).Elements.Count > 1;
			var returnIsClosure = hasReturn && swiftReturnType is ClosureTypeSpec;
			var returnIsGeneric = hasReturn && typeContext.IsTypeSpecGeneric (swiftReturnType) && !returnIsClosure;
			var returnIsAssocPath = hasReturn && typeContext.IsProtocolWithAssociatedTypesFullPath (swiftReturnType as NamedTypeSpec, typeMapper);
			var returnIsNonScalarStruct = hasReturn && !returnIsScalar && returnEntity != null &&
				(returnEntity.EntityType == EntityType.Struct || returnEntity.EntityType == EntityType.Enum);
			var returnIsSelf = hasReturn && swiftReturnType.IsDynamicSelf;

			var retSimple = returnType as CSSimpleType;
			var returnIsInterfaceFromProtocol =
				hasReturn && returnEntity != null && returnEntity.EntityType == EntityType.Protocol && retSimple != null;
			var returnIsProtocolList = hasReturn && swiftReturnType is ProtocolListTypeSpec;
			var returnIsObjCProtocol = hasReturn && returnEntity != null && returnEntity.IsObjCProtocol;
			var returnNeedsPostProcessing = (hasReturn && (returnIsClass || returnIsProtocolList || returnIsInterfaceFromProtocol || returnIsNonTrivialTuple ||
				returnIsGeneric || returnIsNonScalarStruct || returnIsAssocPath || (returnIsSelf && !restoreDynamicSelf))) || originalThrows
				|| returnIsTrivialEnum;

			includeCastToReturnType = includeCastToReturnType || returnIsTrivialEnum;
			includeIntermediateCastToLong = includeIntermediateCastToLong || returnIsTrivialEnum;

			var filteredTypeSpec = FilterParams (parms, wrapperFuncDecl, originalThrows);

			var callParameters = new List<CSBaseExpression> (parms.Count);

			var offsetToOriginalArgs = 0;
			if ((hasReturn && indexOfReturn >= 0) || originalThrows)
				offsetToOriginalArgs++;
			if (swiftInstanceType != null)
				offsetToOriginalArgs++;
			if (isExtensionMethod && swiftInstanceType == null)
				offsetToOriginalArgs++;

			for (int i = 0; i < parms.Count; i++) {
				var p = parms [i];
				// if it's the instance, pass that
				// if it's the return, pass that
				// otherwise take it from the original functions primary parameter list
				TypeSpec originalParm = null;

				if (i == indexOfInstance && swiftInstanceType != null) {
					originalParm = swiftInstanceType;
				} else if ((hasReturn && i == indexOfReturn) || (originalThrows && i == indexOfReturn)) {
					originalParm = swiftReturnType;
				} else if (isExtensionMethod && p.ParameterKind == CSParameterKind.This) {
					originalParm = originalFunction.IsExtension ? originalFunction.ParentExtension.ExtensionOnType :
						new NamedTypeSpec (originalFunction.Parent.ToFullyQualifiedNameWithGenerics ());				
				} else {
					var index = i - offsetToOriginalArgs;
					originalParm = originalFunction.ParameterLists.Last () [i - offsetToOriginalArgs].TypeSpec;
				}
				callParameters.Add (Marshal (typeContext, wrapperFuncDecl, p, filteredTypeSpec [i], instanceIsSwiftProtocol && i == indexOfInstance,
							 indexOfReturn >= 0 && i == indexOfReturn, originalParm));
			}


			var call = new CSFunctionCall (pinvokeCall, false, callParameters.ToArray ());

				// Post marshal code demands an intermediate return value
				if (postMarshalCode.Count > 0 && ((object)returnIdent) == null && (returnType != null && returnType != CSSimpleType.Void)) {
					returnIdent = new CSIdentifier (Uniqueify ("retval", identifiersUsed));
					identifiersUsed.Add (returnIdent.Name);
					preMarshalCode.Add (CSVariableDeclaration.VarLine (returnType, returnIdent, returnType.Default ()));
				}


				if (((object)returnIdent) != null) {
					// if returnIntPtr is non-null, then the function returns a pointer to a class
					// If this is the case, we have post marshal code which will assign it to
					// retval.

					if (typeMapper.MustForcePassByReference (typeContext, swiftReturnType) || returnIsNonTrivialTuple || returnIsProtocolList) {
						this.functionCall = new CSLine (call);
					} else {
						CSBaseExpression callExpr = call;
						if (includeCastToReturnType && returnType != null && returnType != CSSimpleType.Void) {
							if (includeIntermediateCastToLong) {
								callExpr = new CSCastExpression (CSSimpleType.Long, callExpr);
							}
							callExpr = new CSCastExpression (returnType, callExpr);
						}
						this.functionCall = CSAssignment.Assign ((returnIntPtr ?? returnProtocol) ?? returnIdent, callExpr);
					}
					this.returnLine = CSReturn.ReturnLine (returnIdent);
				} else {
					if (returnType != null && returnType != CSSimpleType.Void) {
						if (includeCastToReturnType) {
							CSBaseExpression expr = call;
							if (includeIntermediateCastToLong) {
								expr = new CSCastExpression (CSSimpleType.Long, expr);
							}
							expr = new CSCastExpression (returnType, expr);
							this.functionCall = CSReturn.ReturnLine (expr);
						} else {
							this.functionCall = CSReturn.ReturnLine ((ICSExpression)call);
						}
					} else
						this.functionCall = new CSLine (call);
				}

				foreach (var l in absolutelyMustBeFirst)
					yield return l;
				foreach (var l in preMarshalCode)
					yield return l;
				yield return functionCall;
				foreach (var l in postMarshalCode)
					yield return l;
				if (returnLine != null)
					yield return returnLine;
		}

		CSParameter ReworkParameterWithNamer (CSParameter p)
		{
			if (GenericReferenceNamer == null)
				return p;
			var pClone = ReworkTypeWithNamer (p.CSType);
			return new CSParameter (pClone, p.Name, p.ParameterKind, p.DefaultValue);
		}

		CSType ReworkTypeWithNamer (CSType ty)
		{
			if (ty is CSGenericReferenceType genRef) {
				var newGen = new CSGenericReferenceType (genRef.Depth, genRef.Index);
				newGen.ReferenceNamer = GenericReferenceNamer;
				return newGen;
			} else if (ty is CSSimpleType simple) {
				if (simple.GenericTypes == null)
					return simple;
				var genSubTypes = new CSType [simple.GenericTypes.Length];
				for (int i = 0; i < genSubTypes.Length; i++) {
					genSubTypes [i] = ReworkTypeWithNamer (simple.GenericTypes [i]);
				}
				var simpleClone = new CSSimpleType (simple.GenericTypeName, simple.IsArray, genSubTypes);
				return simpleClone;
			} else {
				throw new NotImplementedException ($"Unable to rework type {ty.GetType ().Name} {ty.ToString ()} as generic reference");
			}
		}

		CSBaseExpression Marshal (BaseDeclaration typeContext, FunctionDeclaration funcDecl, CSParameter p, TypeSpec swiftType,
			bool marshalProtocolAsValueType, bool isReturnVariable, TypeSpec originalType)
		{
			p = ReworkParameterWithNamer (p);

			var entityType = typeMapper.GetEntityTypeForTypeSpec (swiftType);
			switch (entityType) {
			case EntityType.Scalar:
			case EntityType.Tuple:
			case EntityType.None:
			// Add more types
				break;
			}
			throw new NotImplementedException ($"Uh-oh - not ready for {swiftType.ToString ()}, a {entityType}.");
		}

		public static string Uniqueify (string name, IEnumerable<string> names)
		{
			int thisTime = 0;
			var sb = new StringBuilder (name);
			while (names.Contains (sb.ToString ())) {
				sb.Clear ().Append (name).Append (thisTime++);
			}
			return sb.ToString ();
		}

		TypeSpec [] FilterParams (CSParameterList parms, FunctionDeclaration wrapperFunc, bool originalThrows)
		{
			var results = new TypeSpec [parms.Count];
			var parameterList = wrapperFunc.ParameterLists.Last ();
			for (int i=0; i < parms.Count; i++) {
				var currType = parameterList [i].TypeSpec;
				results [i] = currType;
			}
			return results;
		}

		List<CSLine> absolutelyMustBeFirst;
		List<CSLine> preMarshalCode;
		CSLine functionCall;
		List<CSLine> postMarshalCode;
		CSLine returnLine;

		public bool MarshalProtocolsDirectly { get; set; }
		public bool RequiredUnsafeCode { get; private set; }
		public bool MarshalingConstructor { get; set; }
		public Func<int, int, string> GenericReferenceNamer { get; set; }
		public CSType ProtocolInterfaceType { get; set; }

	}
}

