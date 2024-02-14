// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo.SwiftLang;
using System.Collections.Generic;
using SwiftReflector.SwiftXmlReflection;
using System.Linq;
using SwiftReflector.ExceptionTools;
using SyntaxDynamo;

namespace SwiftReflector.TypeMapping {
	public class TypeSpecToSLType {
		TypeMapper parent;
		bool isForOverride;
		public TypeSpecToSLType (TypeMapper parent, bool forOverride)
		{
			this.parent = Exceptions.ThrowOnNull (parent, nameof (parent));
			isForOverride = forOverride;
		}

		public SLType MapTypeSimplified (SLImportModules modules, TypeSpec spec)
		{
			switch (spec.Kind) {
			case TypeSpecKind.Named:
				var named = (NamedTypeSpec)spec;
				if (TypeSpec.IsBuiltInValueType (spec)) {
					return new SLSimpleType (named.Name);
				} else {
					return new SLSimpleType ("UnsafeRawPointer");
				}
			case TypeSpecKind.Closure:
			case TypeSpecKind.Tuple:
			case TypeSpecKind.ProtocolList:
				return new SLSimpleType ("UnsafeRawPointer");
			default:
				throw new ArgumentOutOfRangeException ("spec");
			}
		}

		public SLType MapType (BaseDeclaration declContext, SLImportModules modules, TypeSpec spec, bool isForReturn)
		{
			switch (spec.Kind) {
			case TypeSpecKind.Named:
				return MapType (declContext, modules, (NamedTypeSpec)spec);
			case TypeSpecKind.Closure:
				return MapType (declContext, modules, (ClosureTypeSpec)spec, isForReturn);
			case TypeSpecKind.Tuple:
				return MapType (declContext, modules, (TupleTypeSpec)spec);
			case TypeSpecKind.ProtocolList:
				return MapType (declContext, modules, (ProtocolListTypeSpec)spec);
			default:
				throw new ArgumentOutOfRangeException ("spec");
			}
		}

		SLType MapType (BaseDeclaration declContext, SLImportModules modules, NamedTypeSpec spec)
		{
			SLType retval = null;
			if (spec.HasModule (declContext, this.parent))
				modules.AddIfNotPresent (spec.Module);
			if (declContext.IsTypeSpecGeneric (spec) && !spec.ContainsGenericParameters) {
				Tuple<int, int> depthIndex = declContext.GetGenericDepthAndIndex (spec);
				retval = new SLGenericReferenceType (depthIndex.Item1, depthIndex.Item2);
			} else if (spec.ContainsGenericParameters) {
				retval = new SLBoundGenericType (spec.NameWithoutModule, spec.GenericParameters.Select (p => MapType (declContext, modules, p, false)));
			} else {
				if (declContext.IsProtocolWithAssociatedTypesFullPath (spec, parent)) {
					// for T.AssocType
					var genPart = spec.Module;
					var depthIndex = declContext.GetGenericDepthAndIndex (genPart);
					var newGenPart = new SLGenericReferenceType (depthIndex.Item1, depthIndex.Item2);
					retval = new SLSimpleType ($"{newGenPart}.{spec.NameWithoutModule}");
				} else {
					retval = new SLSimpleType (spec.NameWithoutModule);
				}
			}

			if (spec.InnerType == null)
				return retval;
			else
				return new SLCompoundType (retval, MapType (declContext, modules, spec.InnerType));
		}

		SLType MapType (BaseDeclaration declContext, SLImportModules modules, ClosureTypeSpec spec, bool isForReturn)
		{
			var argumentTypes = new List<SLType> ();
			if (spec.Arguments is TupleTypeSpec) {
				argumentTypes.AddRange (((TupleTypeSpec)spec.Arguments).Elements.Select (arg => MapType (declContext, modules, arg, false)));
			} else {
				argumentTypes.Add (MapType (declContext, modules, spec.Arguments, false));
			}


			SLFuncType funcType = null;
			if (isForOverride) {
				var arguments = new SLTupleType (argumentTypes.Select (at => new SLNameTypePair ((string)null, at)).ToList ());
				if (spec.ReturnType.IsEmptyTuple) {
					// Action ->
					funcType = new SLFuncType (arguments ?? new SLTupleType (), new SLTupleType (), hasThrows: spec.Throws);
				} else {
					// Func
					SLType slRetType = MapType (declContext, modules, spec.ReturnType, true);
					funcType = new SLFuncType (arguments ?? new SLTupleType (), slRetType, hasThrows: spec.Throws);
				}
				if (spec.IsEscaping) {
					SLAttribute.Escaping ().AttachBefore (funcType);
				}
			} else if (isForReturn) {
				var arguments = argumentTypes.Select (at => new SLNameTypePair ("_", at)).ToList ();

				if (spec.ReturnType.IsEmptyTuple) {
					// Action ->
					// (UnsafeMutablePointer<(argumentTypes)>) -> ()
					var pointerToArgTuple = arguments.Count != 0 ?
									 new SLBoundGenericType ("UnsafeMutablePointer", new SLTupleType (arguments))
									 : null;
					if (pointerToArgTuple != null) {
						funcType = new SLFuncType (new SLTupleType (new SLNameTypePair ("_", pointerToArgTuple)),
												  new SLTupleType ());
					} else { // should never happen?
						funcType = new SLFuncType (new SLTupleType (), new SLTupleType ());
					}
				} else {
					// Func
					// (UnsafeMutablePointer<returnType>,UnsafeMutablePointer<(argumentTypes)>) -> ()
					SLType slRetType = MapType (declContext, modules, spec.ReturnType, true);
					var pointerToArgTuple = arguments.Count != 0 ?
									 new SLBoundGenericType ("UnsafeMutablePointer", new SLTupleType (arguments))
									 : null;
					SLType returnSLType = null;
					if (spec.Throws && !spec.IsAsync) {
						var returnTuple = new SLTupleType (new SLNameTypePair ((string)null, slRetType),
							new SLNameTypePair ((string)null, new SLSimpleType ("Swift.Error")),
							new SLNameTypePair ((string)null, SLSimpleType.Bool));
						returnSLType = new SLBoundGenericType ("UnsafeMutablePointer", returnTuple);
					} else if (spec.IsAsync) {
						throw new NotImplementedException ("Not handling async return closure mapping (yet)");
					} else {
						returnSLType = new SLBoundGenericType ("UnsafeMutablePointer", slRetType);
					}

					if (pointerToArgTuple != null) {
						funcType = new SLFuncType (new SLTupleType (new SLNameTypePair ("_", returnSLType),
							new SLNameTypePair ("_", pointerToArgTuple)), new SLTupleType ());
					} else {
						funcType = new SLFuncType (new SLTupleType (new SLNameTypePair ("_", returnSLType)),
							new SLTupleType ());
					}
				}
			} else {
				var arguments = argumentTypes.Select (at => new SLNameTypePair ("_", at)).ToList ();
				var opaquePointerArg = new SLNameTypePair ("_", SLSimpleType.OpaquePointer);

				if (spec.ReturnType.IsEmptyTuple) {
					// Action ->
					// (UnsafeMutablePointer<(argumentTypes)>, OpaquePointer) -> ()
					var pointerToArgTuple = arguments.Count != 0 ?
									 new SLBoundGenericType ("UnsafeMutablePointer", new SLTupleType (arguments))
									 : null;
					if (pointerToArgTuple != null) {
						funcType = new SLFuncType (new SLTupleType (new SLNameTypePair ("_", pointerToArgTuple), opaquePointerArg),
												  new SLTupleType ());
					} else { // should never happen?
						funcType = new SLFuncType (new SLTupleType (opaquePointerArg), new SLTupleType ());
					}

				} else {
					// Func
					// (UnsafeMutablePointer<returnType>,UnsafeMutablePointer<(argumentTypes)>) -> ()
					// SLType slRetType = MapType (declContext, modules, spec.ReturnType, true);
					// var pointerToArgTuple = arguments.Count != 0 ?
					// 				 new SLBoundGenericType ("UnsafeMutablePointer", new SLTupleType (arguments))
					// 				 : null;
					// var slReturnTypePtr = MethodWrapping.ClosureReturnType (slRetType, spec);
					// if (pointerToArgTuple != null) {
					// 	funcType = new SLFuncType (new SLTupleType (new SLNameTypePair ("_", slReturnTypePtr),
					// 	                                            new SLNameTypePair ("_", pointerToArgTuple),
					// 						    opaquePointerArg),
					// 				   new SLTupleType ());
					// } else {
					// 	funcType = new SLFuncType (new SLTupleType (new SLNameTypePair ("_", slReturnTypePtr),
					// 						    opaquePointerArg),
					// 				   new SLTupleType ());
					// }
				}

				if (!isForReturn) {
					funcType.Attributes.Add (new SLAttribute ("escaping", null));
				}
			}


			return funcType;
		}

		SLType MapType (BaseDeclaration declContext, SLImportModules modules, TupleTypeSpec spec)
		{
			return new SLTupleType (spec.Elements.Select (p => new SLNameTypePair ((string)null, MapType (declContext, modules, p, false))).ToList ());
		}

		SLType MapType (BaseDeclaration declContext, SLImportModules modules, ProtocolListTypeSpec spec)
		{
			return new SLProtocolListType (spec.Protocols.Keys.Select (proto => MapType (declContext, modules, proto)));
		}

		public void MapParams (TypeMapper typeMapper, FunctionDeclaration func, SLImportModules modules, List<SLParameter> output, List<ParameterItem> parameters,
				      bool dontChangeInOut, SLGenericTypeDeclarationCollection genericDeclaration = null, bool remapSelf = false, string selfReplacement = "")
		{
			output.AddRange (parameters.Select ((p, i) => ToParameter (typeMapper, func, modules, p, i, dontChangeInOut, genericDeclaration, remapSelf, selfReplacement)));
		}

		public void MapParamsToCSharpTypes (SLImportModules modules, List<SLParameter> output, List<ParameterItem> parameters)
		{
			output.AddRange (parameters.Select ((p, i) => ToParameterToCSharpTypes (modules, p, i)));
		}

		SLParameter ToParameterToCSharpTypes (SLImportModules modules, ParameterItem p, int index)
		{
			return new SLParameter (p.PublicName, p.PrivateName, MapTypeSimplified (modules, p.TypeSpec),
						p.IsInOut && TypeSpec.IsBuiltInValueType (p.TypeSpec) ? SLParameterKind.InOut : SLParameterKind.None);
		}

		public SLParameter ToParameter (TypeMapper typeMapper, FunctionDeclaration func, SLImportModules modules, ParameterItem p, int index,
						   bool dontChangeInOut, SLGenericTypeDeclarationCollection genericDecl = null, bool remapSelf = false, string remappedSelfName = "")
		{
			var pIsGeneric = func.IsTypeSpecGeneric (p) && p.TypeSpec is NamedTypeSpec;
			var parmTypeEntity = !pIsGeneric ? typeMapper.GetEntityForTypeSpec (p.TypeSpec) : null;
			if (parmTypeEntity == null && !pIsGeneric && p.IsInOut)
				throw ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 45, $"In function {func.ToFullyQualifiedName ()}, unknown type parameter type {p.PublicName}:{p.TypeName}.");
			var parmKind = p.IsInOut && (pIsGeneric || !parmTypeEntity.IsStructOrEnum) ? SLParameterKind.InOut : SLParameterKind.None;
			SLType parmType = null;

			var pTypeSpec = remapSelf ? p.TypeSpec.ReplaceName ("Self", remappedSelfName) : p.TypeSpec;

			if (genericDecl != null) {
				GatherGenerics (typeMapper, func, modules, pTypeSpec, genericDecl);
			}
			if (pIsGeneric) {
				if (pTypeSpec.ContainsGenericParameters) {
					var ns = pTypeSpec as NamedTypeSpec;
					var boundGen = new SLBoundGenericType (ns.Name,
									       pTypeSpec.GenericParameters.Select (genParm => {
										       return MapType (func, modules, genParm, true);
									       }));
					if (parent.MustForcePassByReference (func, ns)) {
						boundGen = new SLBoundGenericType ("UnsafeMutablePointer", boundGen);
					}
					parmType = boundGen;
				} else {
					var namedType = pTypeSpec as NamedTypeSpec;
					if (namedType == null)
						throw new NotImplementedException ("Can only have a named type spec here.");
					var depthIndex = func.GetGenericDepthAndIndex (namedType.Name);
					var gd = func.GetGeneric (depthIndex.Item1, depthIndex.Item2);
					var genRef = new SLGenericReferenceType (depthIndex.Item1, depthIndex.Item2, func.IsTypeSpecGenericMetatypeReference (namedType));
					parmType = genRef;
				}
			} else {
				parmType = MapType (func, modules, pTypeSpec, false);
				if (pIsGeneric) {
					Tuple<int, int> depthIndex = func.GetGenericDepthAndIndex (pTypeSpec);
					parmType = new SLGenericReferenceType (depthIndex.Item1, depthIndex.Item2);
				} else if (parent.MustForcePassByReference (func, pTypeSpec) && !dontChangeInOut) {
					parmType = new SLBoundGenericType (p.IsInOut ? "UnsafeMutablePointer" : "UnsafePointer", parmType);
				}
			}
			if (isForOverride && p.IsVariadic) {
				// if we get here, then parmType is an SLSimpleType of SwiftArray<someType>
				// we're going to turn it into "someType ..."
				var oldParmType = parmType as SLBoundGenericType;
				parmType = new SLVariadicType (oldParmType.BoundTypes [0]);
			}
			var publicName = !p.NameIsRequired ? null : ConjureIdentifier (p.PublicName, index);
			var privateName = !String.IsNullOrEmpty (p.PrivateName) ? p.PrivateName : null;
			return new SLParameter (publicName, ConjureIdentifier (privateName, index), parmType, parmKind);
		}


		void GatherGenerics (TypeMapper typeMapper, FunctionDeclaration func, SLImportModules modules, TypeSpec type,
		                     SLGenericTypeDeclarationCollection genericDeclations)
		{
			var redundantConstraints = new Dictionary<string, List<BaseConstraint>> ();
			GatherGenerics (typeMapper, func, modules, type, genericDeclations, redundantConstraints);

			// In the process of gathering generics, there may be generic types with
			// constraints that are redundant. In the process of gathering the generics, I
			// collect all the generic reference types that have constraints that would be
			// considered redundant.
			// After gathering, all constraints are looked up and if they're redundant, they get removed.

			foreach (var generic in genericDeclations) {
				var genericName = generic.Name.Name;
				List<BaseConstraint> forbiddenList = null;
				if (!redundantConstraints.TryGetValue (genericName, out forbiddenList))
					continue;
				for (int i = generic.Constraints.Count - 1; i >= 0; i--) {
					var candidateConstraint = generic.Constraints [i];
					foreach (var forbidden in forbiddenList) {
						var forbiddenInherit = forbidden as InheritanceConstraint;
						if (candidateConstraint.IsInheritance && forbidden != null) {
							var candidateString = candidateConstraint.SecondType.ToString ();
							var forbiddenNamedTypeSpec = forbiddenInherit.InheritsTypeSpec as NamedTypeSpec;
							if (forbiddenNamedTypeSpec == null)
								continue;
							var verbottenString = forbiddenNamedTypeSpec.NameWithoutModule;
							if (candidateString == verbottenString) {
								generic.Constraints.RemoveAt (i);
								break;
							}								
						}
					}
				}
			}
		}

		void GatherGenerics (TypeMapper typeMapper, FunctionDeclaration func, SLImportModules modules, TypeSpec type,
		                     SLGenericTypeDeclarationCollection genericDecl, Dictionary<string, List<BaseConstraint>> redundantConstraints)
		{
			if (!func.IsTypeSpecGeneric (type))
				return;

			if (type.ContainsGenericParameters) {
				var entity = typeMapper.GetEntityForTypeSpec (type);
				if (entity != null) {
					for (int i = 0; i < entity.Type.Generics.Count; i++) {
						var genDecl = entity.Type.Generics [i];
						var originalGenTypeSpec = type.GenericParameters [i];
						if (originalGenTypeSpec is NamedTypeSpec named) {
							var depthIndex = func.GetGenericDepthAndIndex (originalGenTypeSpec);
							if (depthIndex.Item1 < 0 || depthIndex.Item2 < 0)
								continue;
							var genRef = new SLGenericReferenceType (depthIndex.Item1, depthIndex.Item2);
							var genRefName = genRef.Name;
							List<BaseConstraint> constList = null;
							if (!redundantConstraints.TryGetValue (genRefName, out constList)) {
								constList = new List<BaseConstraint> ();
								redundantConstraints.Add (genRefName, constList);
							}
							constList.AddRange (genDecl.Constraints);
						} else if (originalGenTypeSpec is ClosureTypeSpec closure) {
							GatherGenerics (typeMapper, func, modules, closure.Arguments, genericDecl, redundantConstraints);
							GatherGenerics (typeMapper, func, modules, closure.ReturnType, genericDecl, redundantConstraints);
						} else if (originalGenTypeSpec is TupleTypeSpec tuple) {
							foreach (var tupleSpec in tuple.Elements) {
								GatherGenerics (typeMapper, func, modules, tupleSpec, genericDecl, redundantConstraints);
							}
						}
					}
				}
				foreach (var subType in type.GenericParameters)
					GatherGenerics (typeMapper, func, modules, subType, genericDecl, redundantConstraints);
			} else {
// 				if (type is NamedTypeSpec named) {
// 					var depthIndex = func.GetGenericDepthAndIndex (type);
// 					var gd = func.GetGeneric (depthIndex.Item1, depthIndex.Item2);
// 					var genRef = new SLGenericReferenceType (depthIndex.Item1, depthIndex.Item2);
// 					var sldecl = new SLGenericTypeDeclaration (new SLIdentifier (genRef.Name));
// #if SWIFT4
// 					if (depthIndex.Item1 >= func.GetMaxDepth ()) {
// 						sldecl.Constraints.AddRange (gd.Constraints.Select (baseConstraint =>
// 							MethodWrapping.ToSLGenericConstraint (func, baseConstraint, genRef.ToString ())
// 						));
// 					}
// #else
// 					sldecl.Constraints.AddRange (gd.Constraints.Select (bc => {
// 						InheritanceConstraint inh = bc as InheritanceConstraint;
// 						if (inh == null)
// 							throw new CompilerException ("Equality constraints not supported (yet)");
// 						return new SLGenericConstraint (true, new SLSimpleType (genRef.Name), parent.TypeSpecMapper.MapType (func, modules, inh.InheritsTypeSpec));
// 					}));
// #endif
// 					genericDecl.Add (sldecl);
// 				} else if (type is ClosureTypeSpec closure) {
// 					GatherGenerics (typeMapper, func, modules, closure.Arguments, genericDecl, redundantConstraints);
// 					GatherGenerics (typeMapper, func, modules, closure.ReturnType, genericDecl, redundantConstraints);
// 				} else if (type is TupleTypeSpec tuple) {
// 					foreach (var tupleSpec in tuple.Elements) {
// 						GatherGenerics (typeMapper, func, modules, tupleSpec, genericDecl, redundantConstraints);
// 					}
// 				}
			}
		}

		public static SLIdentifier ConjureIdentifier (string name, int index)
		{
			name = name ?? $"xamarin_anonymous_parameter{index}";

			return new SLIdentifier (name);
		}
	}
}

