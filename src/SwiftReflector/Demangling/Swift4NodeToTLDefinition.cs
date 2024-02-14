// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftRuntimeLibrary;

namespace SwiftReflector.Demangling {
	public class Swift4NodeToTLDefinition {
		static List<NodeKind> nominalNodeKinds = new List<NodeKind> {
			NodeKind.Class, NodeKind.Enum, NodeKind.Structure
		};
		static List<NodeKind> nominalAndProtocolNodeKinds = new List<NodeKind> {
			NodeKind.Class, NodeKind.Enum, NodeKind.Structure, NodeKind.Protocol
		};

		static List<NodeKind> boundGenericNominalNodeKinds = new List<NodeKind> {
			NodeKind.BoundGenericEnum, NodeKind.BoundGenericClass, NodeKind.BoundGenericStructure
		};

		static List<NodeKind> identifierOrOperator = new List<NodeKind> {
			NodeKind.Identifier, NodeKind.PrefixOperator, NodeKind.InfixOperator, NodeKind.PostfixOperator
		};

		static List<NodeKind> identifierOrOperatorOrPrivateDecl = new List<NodeKind> {
			NodeKind.Identifier, NodeKind.PrefixOperator, NodeKind.InfixOperator, NodeKind.PostfixOperator,NodeKind.PrivateDeclName
		};

		static List<NodeKind> identifierOrPrivateDeclName = new List<NodeKind> {
			NodeKind.Identifier, NodeKind.PrivateDeclName
		};
		string mangledName;
		ulong offset;

		RuleRunner ruleRunner;
		List<MatchRule> rules;

		public Swift4NodeToTLDefinition (string mangledName, ulong offset = 0)
		{
			this.mangledName = mangledName;
			this.offset = offset;
			rules = BuildMatchRules ();
			ruleRunner = new RuleRunner (rules);
		}

		List<MatchRule> BuildMatchRules ()
		{
			return new List<MatchRule> {
				new MatchRule {
					Name = "ReturnType",
					NodeKind = NodeKind.ReturnType,
					Reducer = ConvertFirstChildToSwiftType
				},
				new MatchRule {
					Name = "ArgumentTuple",
					NodeKind = NodeKind.ArgumentTuple,
					Reducer = ConvertFirstChildToSwiftType
				},
				new MatchRule {
					Name = "Tuple",
					NodeKind = NodeKind.Tuple,
					Reducer = ConvertToTuple
				},
				new MatchRule {
					Name = "Structure",
					NodeKind = NodeKind.Structure,
					Reducer = ConvertToStruct
				},
				new MatchRule {
					Name = "ClassEnum",
					NodeKindList = new List<NodeKind> { NodeKind.Class, NodeKind.Enum },
					Reducer = ConvertToClass
				},
				new MatchRule {
					Name = "Weak",
					NodeKind = NodeKind.Weak,
					Reducer = ConvertFirstChildToSwiftType
				},
				new MatchRule  {
					Name = "AutoClosure",
					NodeKind = NodeKind.AutoClosureType,
					Reducer = ConvertToFunctionType
				},
				new MatchRule {
					Name = "ProtocolList",
					NodeKind = NodeKind.ProtocolList,
					Reducer = ConvertToProtocolList,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "TypeList",
							NodeKind = NodeKind.TypeList
						}
					}
				},
				new MatchRule {
					Name = "Protocol",
					NodeKind = NodeKind.Protocol,
					Reducer = ConvertToClass
				},
				new MatchRule {
					Name = "NamedTupleElement",
					NodeKind = NodeKind.TupleElement,
					Reducer = ConvertToNamedTupleElement,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "NamedTupleElementName",
							NodeKind = NodeKind.TupleElementName
						},
						new MatchRule {
							Name = "NamedTupleElementType",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "VariadicNamedTupleElement",
					NodeKind = NodeKind.TupleElement,
					Reducer = ConvertToNamedTupleElement,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "VariadicNamedTupleElementMarker",
							NodeKind = NodeKind.VariadicMarker
						},
						new MatchRule {
							Name = "VariadicNamedTupleElementName",
							NodeKind = NodeKind.TupleElementName
						},
						new MatchRule {
							Name = "VariadicNamedTupleElementType",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "VariadicMarkerTupleElement",
					NodeKind = NodeKind.TupleElement,
					Reducer = ConvertToUnnamedTupleElement,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "VariadicNamedTupleElementMarker",
							NodeKind = NodeKind.VariadicMarker
						},
						new MatchRule {
							Name = "VariadicMarkerTupleElementType",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "TupleElement",
					NodeKind = NodeKind.TupleElement,
					Reducer = ConvertToUnnamedTupleElement,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "TupleElementType",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "VariadicTupleElement",
					NodeKind = NodeKind.TupleElement,
					Reducer = ConvertToVariadicTupleElement,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "VariadicElementType",
							NodeKind = NodeKind.VariadicMarker,
						},
						new MatchRule {
							Name = "NamedTupleElementType",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "ProtocolListWithAnyObject",
					NodeKind = NodeKind.ProtocolListWithAnyObject,
					Reducer = ConvertToAnyObject
				},
				new MatchRule {
					Name = "InOutType",
					NodeKind = NodeKind.Type,
					Reducer = ConvertToReferenceType,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "InOutChild",
							NodeKind = NodeKind.InOut
						}
					}
				},
				new MatchRule {
					Name = "ProtocolWitnessTable",
					NodeKindList = new List<NodeKind> {
						NodeKind.ProtocolWitnessTable,
						NodeKind.ProtocolWitnessTableAccessor
					},
					Reducer = ConvertToProtocolWitnessTable,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "ProtocolWitnessChild",
							NodeKind = NodeKind.ProtocolConformance,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "ProtocolWitnessChildTypeChild",
									NodeKind = NodeKind.Type,
								},
								new MatchRule {
									Name = "ProtocolWitnessChildProtocolTypeChild",
									NodeKind = NodeKind.Type,
								},
								new MatchRule {
									Name = "ProtocolWitnessChildModuleChild",
									NodeKind = NodeKind.Identifier
								}
							}
						}
					}
				},
				new MatchRule {
					Name = "ValueWitnessTable",
					NodeKind = NodeKind.ValueWitnessTable,
					Reducer = ConvertToValueWitnessTable,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "ValueWitnessTableChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "Static",
					NodeKind = NodeKind.Static,
					Reducer = ConvertToStatic,
				},
				new MatchRule {
					Name = "UncurriedFunction",
					NodeKind = NodeKind.Function,
					Reducer = ConvertToMethod,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "FunctionNominalChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "FunctionIdentifierChild",
							NodeKindList = identifierOrOperatorOrPrivateDecl,
						},
						new MatchRule {
							Name = "FunctionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "FunctionTypeChildChild",
									NodeKind = NodeKind.FunctionType
								}
							}
						},
					}
				},
				new MatchRule {
					Name = "UncurriedGenericFunction",
					NodeKind = NodeKind.Function,
					Reducer = ConvertToMethod,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "GenericFunctionNominalChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "GenericFunctionIdentifierChild",
							NodeKindList = identifierOrOperatorOrPrivateDecl,
						},
						new MatchRule {
							Name = "GenericFunctionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "GenericFunctionTypeChildChild",
									NodeKind = NodeKind.DependentGenericType
								}
							}
						}
					}
				},
				new MatchRule {
					Name = "Constructor",
					NodeKind = NodeKind.Constructor,
					Reducer = ConvertToNonAllocatingConstructor,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "ConstructorNominalChild",
							NodeKindList = nominalNodeKinds,
						},
						new MatchRule {
							Name = "FunctionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "FunctionTypeChildChild",
									NodeKind = NodeKind.FunctionType
								}
							}
						},
					}
				},
				new MatchRule {
					Name = "Allocator",
					NodeKind = NodeKind.Allocator,
					Reducer = ConvertToAllocatingConstructor,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "AllocatorNominalChild",
							NodeKindList = nominalNodeKinds,
						},
						new MatchRule {
							Name = "FunctionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "FunctionTypeChildChild",
									NodeKind = NodeKind.FunctionType
								}
							}
						},
					}
				},
				new MatchRule {
					Name = "Destructor",
					NodeKind = NodeKind.Destructor,
					Reducer = ConvertToDestructor,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "DestructorNominalChild",
							NodeKindList = nominalNodeKinds,
						}
					}
				},
				new MatchRule {
					Name = "Deallocator",
					NodeKind = NodeKind.Deallocator,
					Reducer = ConvertToDeallocator,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "DeallocatorNominalChild",
							NodeKindList = nominalNodeKinds,
						}
					}
				},
				new MatchRule {
					Name = "Function",
					NodeKind = NodeKind.Function,
					Reducer = ConvertToFunction,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "FunctionIdentifierChild",
							NodeKind = NodeKind.Identifier,
						},
						new MatchRule {
							Name = "FunctionIdentifierChild",
							NodeKindList = identifierOrOperatorOrPrivateDecl,
						},
						new MatchRule {
							Name = "FunctionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "FunctionTypeChildChild",
									NodeKind = NodeKind.FunctionType
								}
							}
						},
					}
				},
				new MatchRule {
					Name = "GlobalMutableAddressor",
					NodeKind = NodeKind.UnsafeMutableAddressor,
					Reducer = ConvertToGlobalUnsafeMutableAddressor,
	    				ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "FunctionIdentifierChild",
							NodeKind = NodeKind.Identifier,
						},
						new MatchRule {
							Name = "FunctionIdentifierChild",
							NodeKindList = identifierOrOperatorOrPrivateDecl,
						},
						new MatchRule {
							Name = "FunctionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "FunctionTypeChildChild",
									NodeKind = NodeKind.FunctionType
								}
							}
						},
					}
				},
				new MatchRule {
					Name = "FunctionExtension",
					NodeKind = NodeKind.Function,
					Reducer = ConvertToFunction,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "FunctionExtensionChild",
							NodeKind = NodeKind.Extension,
						},
						new MatchRule {
							Name = "FunctionExtIdentifierChild",
							NodeKindList = identifierOrOperatorOrPrivateDecl,
						},
						new MatchRule {
							Name = "FunctionExtTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "FunctionTypeChildChild",
									NodeKind = NodeKind.FunctionType
								}
							}
						},
					}
				},
				new MatchRule {
					Name = "GenericFunctionExtension",
					NodeKind = NodeKind.Function,
					Reducer = ConvertToFunction,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "GenericFunctionExtensionChild",
							NodeKind = NodeKind.Extension,
						},
						new MatchRule {
							Name = "GenericFunctionExtensionIdentifierChild",
							NodeKindList = identifierOrOperatorOrPrivateDecl,
						},
						new MatchRule {
							Name = "GenericFunctionExtensionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "GenericFunctionExtensionTypeChildChild",
									NodeKind = NodeKind.DependentGenericType
								}
							}
						},
					}
				},
				new MatchRule {
					Name = "GenericFunction",
					NodeKind = NodeKind.Function,
					Reducer = ConvertToFunction,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "FunctionIdentifierChild",
							NodeKind = NodeKind.Identifier,
						},
						new MatchRule {
							Name = "FunctionIdentifierChild",
							NodeKindList = identifierOrOperatorOrPrivateDecl,
						},
						new MatchRule {
							Name = "FunctionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "FunctionTypeChildChild",
									NodeKind = NodeKind.DependentGenericType,
									ChildRules = new List<MatchRule> {
										new MatchRule {
											Name = "DependentGenericSignature",
											NodeKind = NodeKind.DependentGenericSignature
										},
										new MatchRule {
											Name = "DependentGenericFunctionTypeChild",
											NodeKind = NodeKind.Type,
											ChildRules = new List<MatchRule> {
												new MatchRule {
													Name = "DependentGenericFunctionChild",
													NodeKind = NodeKind.FunctionType
												}
											}
										}
									}
								}
							}
						},
					}
				},
				new MatchRule {
					Name = "FunctionThrowsType",
					NodeKind = NodeKind.FunctionType,
					Reducer = ConvertToFunctionThrowsType,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "FunctionChildThrows",
							NodeKind = NodeKind.ThrowsAnnotation
						},
						new MatchRule {
							Name = "FunctionChildArgumentTuple",
							NodeKind = NodeKind.ArgumentTuple
						},
						new MatchRule {
							Name = "FunctionChildReturnType",
							NodeKind = NodeKind.ReturnType
						}
					}
				},
				new MatchRule {
					Name = "FunctionType",
					NodeKind = NodeKind.FunctionType,
					Reducer = ConvertToFunctionType,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "FunctionChildArgumentTuple",
							NodeKind = NodeKind.ArgumentTuple
						},
						new MatchRule {
							Name = "FunctionChildReturnType",
							NodeKind = NodeKind.ReturnType
						}
					}
				},
				new MatchRule {
					Name = "CFunctionPointerType",
					NodeKind = NodeKind.CFunctionPointer,
					Reducer = ConvertToCFunctionPointerType,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "CFunctionPointerChildArgumentTuple",
							NodeKind = NodeKind.ArgumentTuple
						},
						new MatchRule {
							Name = "CFunctionPointerChildReturnType",
							NodeKind = NodeKind.ReturnType
						}
					}
				},
				new MatchRule {
					Name = "SubscriptGetter",
					NodeKind = NodeKind.Getter,
					Reducer = ConvertToSubscriptGetter,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "SubscriptGetterContextChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "SubscriptGetterIdentifierChild",
							NodeKind = NodeKind.Identifier
						},
						new MatchRule {
							Name = "SubscriptGetterTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "SubscriptFunction",
									NodeKind = NodeKind.FunctionType
								}
							}
						}
					}
				},
				new MatchRule {
					Name = "SubscriptSetter",
					NodeKind = NodeKind.Setter,
					Reducer = ConvertToSubscriptSetter,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "SubscriptGetterContextChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "SubscriptGetterIdentifierChild",
							NodeKind = NodeKind.Identifier
						},
						new MatchRule {
							Name = "SubscriptGetterTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "SubscriptFunction",
									NodeKind = NodeKind.FunctionType
								}
							}
						}
					}
				},
				new MatchRule {
					Name = "Getter",
					NodeKind = NodeKind.Getter,
					Reducer = ConvertToGetter,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "GetterContextChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "GetterIdentifierChild",
							NodeKindList = identifierOrPrivateDeclName
						},
						new MatchRule {
							Name = "GetterTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "Setter",
					NodeKind = NodeKind.Setter,
					Reducer = ConvertToSetter,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "SetterContextChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "SetterIdentifierChild",
							NodeKindList = identifierOrPrivateDeclName
						},
						new MatchRule {
							Name = "SetterTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "DidSet",
					NodeKind = NodeKind.DidSet,
					Reducer = ConvertToDidSet,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "DidSetContextChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "DidSetIdentifierChild",
							NodeKindList = identifierOrPrivateDeclName
						},
						new MatchRule {
							Name = "DidSetTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "WillSet",
					NodeKind = NodeKind.WillSet,
					Reducer = ConvertToWillSet,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "WillSetContextChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "WillSetIdentifierChild",
							NodeKindList = identifierOrPrivateDeclName
						},
						new MatchRule {
							Name = "WillSetTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "MaterializeForSet",
					NodeKind = NodeKind.MaterializeForSet,
					Reducer = ConvertToMaterializer,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "MaterializerContextChild",
							NodeKindList = nominalAndProtocolNodeKinds,
						},
						new MatchRule {
							Name = "MaterializerIdentifierChild",
							NodeKindList = identifierOrPrivateDeclName
						},
						new MatchRule {
							Name = "MaterializerTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "MaterializeForSetExtension",
		    			NodeKind = NodeKind.MaterializeForSet,
					Reducer = ConvertToMaterializer,
		    			ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "MaterializeForSetExtensionChild",
			    				NodeKind = NodeKind.Extension,
						},
						new MatchRule {
							Name = "MaterializeForSetExtensionIdentifierChild",
			    				NodeKind = NodeKind.Identifier,
						},
						new MatchRule {
							Name = "MaterializeForSetExtensionTypeChild",
			    				NodeKind = NodeKind.Type,
						},
					}
				},
				new MatchRule {
					Name = "GlobalGetter",
					NodeKind = NodeKind.Getter,
					Reducer = ConvertToGlobalGetter,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "GetterModuleChild",
							NodeKind = NodeKind.Identifier,
						},
						new MatchRule {
							Name = "GetterIdentifierChild",
							NodeKind = NodeKind.Identifier
						},
						new MatchRule {
							Name = "GetterTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "GlobalExtensionGetter",
					NodeKind = NodeKind.Getter,
					Reducer = ConvertToGlobalGetter,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "GetterExtensionChild",
							NodeKind = NodeKind.Extension,
						},
						new MatchRule {
							Name = "GetterExtIdentifierChild",
							NodeKind = NodeKind.Identifier
						},
						new MatchRule {
							Name = "GetterExtTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "GlobalSetter",
					NodeKind = NodeKind.Setter,
					Reducer = ConvertToGlobalSetter,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "SetterModuleChild",
							NodeKind = NodeKind.Identifier,
						},
						new MatchRule {
							Name = "SetterIdentifierChild",
							NodeKind = NodeKind.Identifier
						},
						new MatchRule {
							Name = "SetterTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "GlobalExtensionSetter",
					NodeKind = NodeKind.Setter,
					Reducer = ConvertToGlobalSetter,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "SetterExtensionModuleChild",
							NodeKind = NodeKind.Extension,
						},
						new MatchRule {
							Name = "SetterExtensionIdentifierChild",
							NodeKind = NodeKind.Identifier
						},
						new MatchRule {
							Name = "SetterExtensionTypeChild",
							NodeKind = NodeKind.Type
						}
					}
				},

				new MatchRule {
					Name = "DependentGenericFunctionType",
					NodeKind = NodeKind.DependentGenericType,
					Reducer = ConvertToGenericFunction,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "DependentGenericSignature",
							NodeKind = NodeKind.DependentGenericSignature
						},
						new MatchRule {
							Name = "DependentGenericFunctionTypeChild",
							NodeKind = NodeKind.Type,
							ChildRules = new List<MatchRule> {
								new MatchRule {
									Name = "DependentGenericFunctionChild",
									NodeKind = NodeKind.FunctionType
								}
							}
						}
					}
				},
				new MatchRule {
					Name = "BoundGenericNominal",
					NodeKindList = boundGenericNominalNodeKinds,
					Reducer = ConvertToBoundGeneric,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "TypeChild",
							NodeKind = NodeKind.Type
						},
						new MatchRule {
							Name = "TypeListChild",
							NodeKind = NodeKind.TypeList
						}
					}
				},
				new MatchRule {
					Name = "Variable",
					NodeKind = NodeKind.Variable,
					Reducer = ConvertToVariable,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "VariableChildContext",
							NodeKindList = nominalNodeKinds,
						},
						new MatchRule {
							Name = "VariableChildIdentifier",
							NodeKind = NodeKind.Identifier,
						},
						new MatchRule {
							Name = "VariableChildType",
							NodeKind = NodeKind.Type
						}
					}
				},
				new MatchRule {
					Name = "DependentGenericParameter",
					NodeKind = NodeKind.DependentGenericParamType,
					Reducer = ConvertToGenericReference,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "Depth",
							NodeKind = NodeKind.Index
						},
						new MatchRule {
							Name = "Index",
							NodeKind = NodeKind.Index
						}
					}
				},
				new MatchRule {
					Name = "DependentMemberType",
					NodeKind = NodeKind.DependentMemberType,
					Reducer = ConvertFirstChildToSwiftType,
					ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "Type",
							NodeKind = NodeKind.Type
						},
					}
				},

				new MatchRule {
					Name = "DynamicSelf",
					NodeKind = NodeKind.DynamicSelf,
					Reducer = ConvertFirstChildToSwiftType,
					MatchChildCount = false
				},

				new MatchRule {
					Name = "ExitentialMetatype",
		    			NodeKind = NodeKind.ExistentialMetatype,
					Reducer = ConvertToMetatype,
		    			ChildRules = new List<MatchRule> {
						new MatchRule {
							Name = "Type",
			    				NodeKind = NodeKind.Type
						},
					}
				},

				// Probably should be last
				new MatchRule {
					Name = "Type",
					NodeKind = NodeKind.Type,
					Reducer = ConvertFirstChildToSwiftType,
					MatchChildCount = false
				}
			};
		}

		public TLDefinition Convert (Node node)
		{
			Exceptions.ThrowOnNull (node, nameof (node));


			switch (node.Kind) {
			case NodeKind.Type:
				return Convert (node.Children [0]);
			case NodeKind.Static:
				return ConvertStatic (node);
			case NodeKind.Function:
				return ConvertFunction (node);
			case NodeKind.Constructor:
			case NodeKind.Allocator:
				return ConvertFunctionConstructor (node);
			case NodeKind.Destructor:
			case NodeKind.Deallocator:
				return ConvertFunctionDestructor (node);
			case NodeKind.Getter:
			case NodeKind.Setter:
			case NodeKind.DidSet:
			case NodeKind.MaterializeForSet:
			case NodeKind.WillSet:
				return ConvertFunctionProp (node);
			case NodeKind.Variable:
				return ConvertVariable (node, false);
			case NodeKind.TypeMetadataAccessFunction:
				return ConvertCCtor (node);
			case NodeKind.TypeMetadata:
				return ConvertMetadata (node);
			case NodeKind.NominalTypeDescriptor:
				return ConvertNominalTypeDescriptor (node);
			case NodeKind.ProtocolDescriptor:
				return ConvertProtocolDescriptor (node);
			case NodeKind.Initializer:
				return ConvertInitializer (node);
			case NodeKind.TypeMetadataLazyCache:
				return ConvertLazyCacheVariable (node);
			case NodeKind.ProtocolWitnessTable:
			case NodeKind.ProtocolWitnessTableAccessor:
			case NodeKind.ValueWitnessTable:
				return ConvertProtocolWitnessTable (node);
			case NodeKind.FieldOffset:
				return ConvertFieldOffset (node);
			case NodeKind.DefaultArgumentInitializer:
				return ConvertDefaultArgumentInitializer (node);
			case NodeKind.Metaclass:
				return ConvertMetaclass (node);
			case NodeKind.UnsafeMutableAddressor:
				return ConvertUnsafeMutableAddressor (node);
			case NodeKind.CurryThunk:
				return ConvertCurryThunk (node);
			case NodeKind.Global:
				return Convert (node);
			default:
				return null;
			}
		}

		TLFunction ConvertFunction (Node node)
		{
			var swiftType = ConvertToSwiftType (node, false, null);
			var uncurriedFunction = swiftType as SwiftUncurriedFunctionType;
			if (uncurriedFunction != null) {
				// method
				var context = uncurriedFunction.DiscretionaryString.Split ('.');
				var module = new SwiftName (context [0], false);
				var functionName = new SwiftName (context.Last (), false);
				return new TLFunction (mangledName, module, functionName, uncurriedFunction.UncurriedParameter as SwiftClassType,
						      uncurriedFunction, offset);
			}

			var plainFunction = swiftType as SwiftFunctionType;
			if (plainFunction != null) {
				var context = plainFunction.DiscretionaryString.Split ('.');
				var module = new SwiftName (context [0], false);
				var operatorType = OperatorType.None;
				if (context.Length > 2 && context [context.Length - 2] [0] == '|') {
					Enum.TryParse (context [context.Length - 2].Substring (1), out operatorType);
				}
				var functionName = new SwiftName (context.Last (), false);
				return new TLFunction (mangledName, module, functionName, null, plainFunction, offset, operatorType);
			}
			return null;
		}

		TLDefinition ConvertStatic (Node node)
		{
			if (node.Children [0].Kind == NodeKind.Variable) {
				return ConvertVariable (node.Children [0], true);
			}
			var swiftType = ConvertToSwiftType (node, false, null);
			var propType = swiftType as SwiftPropertyType;
			if (propType != null) {
				var context = propType.DiscretionaryString.Split ('.');
				var uncurriedParam = propType.UncurriedParameter as SwiftClassType;
				return new TLFunction (mangledName, new SwiftName (context [0], false), propType.Name, uncurriedParam, propType, offset);
			}

			var staticFunction = swiftType as SwiftStaticFunctionType;
			if (staticFunction != null) {
				var context = staticFunction.DiscretionaryString.Split ('.');
				var module = new SwiftName (context [0], false);
				var operatorType = OperatorType.None;
				if (context.Length > 2 && context [context.Length - 2] [0] == '|') {
					Enum.TryParse (context [context.Length - 2].Substring (1), out operatorType);
				}
				var functionName = new SwiftName (context.Last (), false);
				return new TLFunction (mangledName, module, functionName, staticFunction.OfClass, staticFunction, offset, operatorType);
			}
			return null;
		}

		TLFunction ConvertFunctionConstructor (Node node)
		{
			var swiftType = ConvertToSwiftType (node, false, null);
			var constructor = swiftType as SwiftConstructorType;
			if (constructor != null) {
				var context = constructor.DiscretionaryString.Split ('.');
				var module = new SwiftName (context [0], false);
				var functionName = constructor.Name;
				var metaType = constructor.UncurriedParameter as SwiftMetaClassType;
				return new TLFunction (mangledName, module, functionName, metaType.Class, constructor, offset);
			}
			return null;
		}

		TLFunction ConvertFunctionDestructor (Node node)
		{
			var swiftType = ConvertToSwiftType (node, false, null);
			var destructor = swiftType as SwiftDestructorType;
			if (destructor != null) {
				var context = destructor.DiscretionaryString.Split ('.');
				var module = new SwiftName (context [0], false);
				var functionName = destructor.Name;
				var className = destructor.Parameters as SwiftClassType;
				if (className == null)
					throw new NotSupportedException ($"Expected a SwiftClassType as the parameter to the destructor bute got {destructor.Parameters.GetType ().Name}");
				return new TLFunction (mangledName, module, functionName, className, destructor, offset);
			}
			return null;
		}

		TLFunction ConvertFunctionProp (Node node)
		{
			var propType = ConvertToSwiftType (node, false, null) as SwiftPropertyType;
			if (propType == null)
				return null;
			var context = propType.DiscretionaryString.Split ('.');
			var uncurriedParam = propType.UncurriedParameter as SwiftClassType;
			return new TLFunction (mangledName, new SwiftName (context [0], false), propType.Name, uncurriedParam, propType, offset);
		}

		TLVariable ConvertVariable (Node node, bool isStatic)
		{
			if (node.Children.Count != 3)
				throw new ArgumentOutOfRangeException (nameof (node), $"Expected 3 children in a variable node, but got {node.Children.Count}");
			var classType = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			if (classType == null && !node.Children [0].HasText)
				return null;
			var module = classType != null ? classType.ClassName.Module : new SwiftName (node.Children [0].Text, false);
			var name = new SwiftName (PrivateNamePublicName (node.Children [1]).Item2, false);
			var variableType = ConvertToSwiftType (node.Children [2], false, null);
			return new TLVariable (mangledName, module, classType, name, variableType, isStatic, offset);
		}

		TLDefaultArgumentInitializer ConvertDefaultArgumentInitializer (Node node)
		{
			if (node.Children.Count != 2)
				return null;
			var baseFunction = ConvertToSwiftType (node.Children [0], false, null) as SwiftBaseFunctionType;
			if (baseFunction == null)
				return null;
			var context = baseFunction.DiscretionaryString.Split ('.');
			var module = new SwiftName (context [0], false);
			var argumentIndex = (int)node.Children [1].Index;
			return new TLDefaultArgumentInitializer (mangledName, module, baseFunction, argumentIndex, offset);
		}

		TLFieldOffset ConvertFieldOffset (Node node)
		{
			if (node.Children.Count != 2 || node.Children [0].Kind != NodeKind.Directness)
				return null;
			var variable = ConvertToVariable (node.Children [1], false, null) as SwiftInitializerType;
			if (variable == null)
				return null;

			var context = variable.DiscretionaryString.Split ('.');
			var module = new SwiftName (context [0], false);
			var className = variable.Owner;

			return new TLFieldOffset (mangledName, module, className, node.Children [0].Index == 0, variable.Name,
						  variable.ReturnType, offset);
		}

		TLFunction ConvertCCtor (Node node)
		{
			var classType = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			var metaType = new SwiftMetaClassType (classType, false);
			var cctor = new SwiftClassConstructorType (metaType, false);
			return new TLFunction (mangledName, classType.ClassName.Module, Decomposer.kSwiftClassConstructorName, classType,
					      cctor, offset);
		}

		TLDirectMetadata ConvertMetadata (Node node)
		{
			var classType = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			return new TLDirectMetadata (mangledName, classType.ClassName.Module, classType, offset);
		}


		TLNominalTypeDescriptor ConvertNominalTypeDescriptor (Node node)
		{
			var classType = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			return new TLNominalTypeDescriptor (mangledName, classType.ClassName.Module, classType, offset);
		}

		TLProtocolTypeDescriptor ConvertProtocolDescriptor (Node node)
		{
			var classType = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			return new TLProtocolTypeDescriptor (mangledName, classType.ClassName.Module, classType, offset);
		}

		TLFunction ConvertInitializer (Node node)
		{
			var swiftInitializer = ConvertToSwiftType (node.Children [0], false, null) as SwiftInitializerType;
			var context = swiftInitializer.DiscretionaryString.Split ('.');
			return new TLFunction (mangledName, new SwiftName (context [0], false), swiftInitializer.Name, swiftInitializer.Owner, swiftInitializer, offset);
		}

		TLLazyCacheVariable ConvertLazyCacheVariable (Node node)
		{
			var classType = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			return new TLLazyCacheVariable (mangledName, classType.ClassName.Module, classType, offset);
		}

		TLFunction ConvertProtocolWitnessTable (Node node)
		{
			var witnessTable = ConvertToSwiftType (node, false, null) as SwiftWitnessTableType;

			var rebuiltWitnessTable = new SwiftWitnessTableType (witnessTable.WitnessType, witnessTable.ProtocolType);

			return new TLFunction (mangledName, new SwiftName (witnessTable.DiscretionaryString, false), null,
					       witnessTable.UncurriedParameter as SwiftClassType, rebuiltWitnessTable, offset);
		}

		TLMetaclass ConvertMetaclass (Node node)
		{
			if (node.Children [0].Kind != NodeKind.Type)
				return null;
			var classType = ConvertToSwiftType (node.Children [0].Children [0], false, null) as SwiftClassType;
			if (classType == null)
				return null;
			var module = classType.ClassName.Module;
			return new TLMetaclass (mangledName, module, classType, offset);
		}

		SwiftType ConvertToGlobalUnsafeMutableAddressor (Node node, bool isReference, SwiftName name)
		{
			string module = node.Children [0].Text;
			var operatorType = ToOperatorType (node.Children [1].Kind);
			var funcName = operatorType == OperatorType.None ? PrivateNamePublicName (node.Children [1]).Item2 : node.Children [1].Text;
			var functionName = module +
					       (operatorType != OperatorType.None ? $".|{operatorType.ToString ()}" : "") +
					       "." + funcName;
			var function = ConvertToSwiftType (node.Children [2], isReference, new SwiftName (funcName, false)) as SwiftFunctionType;
			function.DiscretionaryString = functionName;
			return function;
		}

		TLUnsafeMutableAddressor ConvertUnsafeMutableAddressor (Node node)
		{
			var classType = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			if (classType != null) {
				var privatePublicName = PrivateNamePublicName (node.Children [1]).Item2;
				var ofType = ConvertToSwiftType (node.Children [2], false, null);
				if (classType == null || ofType == null)
					return null;

				return new TLUnsafeMutableAddressor (mangledName, classType.ClassName.Module, classType, new SwiftName (privatePublicName, false), ofType, offset);
			} else {
				var funcType = ConvertToSwiftType (node, false, null) as SwiftFunctionType;
				if (funcType != null) {
					var parts = funcType.DiscretionaryString.Split ('.');
					var moduleName = parts [0];
					var funcName = funcType.Name;
					return new TLUnsafeMutableAddressor (mangledName, new SwiftName (moduleName, false), null, funcName, funcType, offset);
				}
			}
			return null;
		}

		TLThunk ConvertCurryThunk (Node node)
		{
			TLFunction func = ConvertFunction (node.Children [0]);
			return new TLThunk (ThunkType.Curry, func.MangledName, func.Module, func.Class, func.Offset);
		}








		// All the ConvertTo... functions are called from the tree reduction rules

		SwiftType ConvertToSwiftType (Node node, bool isReference, SwiftName name)
		{
			return ruleRunner.RunRules (node, isReference, name);
		}

		SwiftType ConvertFirstChildToSwiftType (Node node, bool isReference, SwiftName name)
		{
			if (node.Children.Count == 0)
				throw new ArgumentOutOfRangeException (nameof (node));
			return ConvertToSwiftType (node.Children [0], isReference, name);
		}

		SwiftType ConvertToReferenceType (Node node, bool isReference, SwiftName name)
		{
			return ConvertToSwiftType (node.Children [0].Children [0], true, name);
		}

		SwiftType ConvertToMetatype (Node node, bool isReference, SwiftName name)
		{
			var subType = ConvertFirstChildToSwiftType (node, isReference, name);
			var classType = subType as SwiftClassType;
			if (classType == null)
				throw new ArgumentOutOfRangeException (nameof (node));
			return new SwiftMetaClassType (classType, isReference);
		}

		SwiftType ConvertToTuple (Node node, bool isReference, SwiftName name)
		{
			if (node.Children.Count == 0)
				return SwiftTupleType.Empty;
			var args = new List<SwiftType> ();
			foreach (var n in node.Children) {
				args.Add (ConvertToSwiftType (n, false, null));
			}
			var st = new SwiftTupleType (args, isReference, name);
			return st;
		}

		SwiftType ConvertToVariable (Node node, bool isReference, SwiftName name)
		{
			var context = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			var variableName = node.Children [1].Text;
			var variableType = ConvertToSwiftType (node.Children [2], false, null);
			var initializerExpr = new SwiftInitializerType (InitializerType.Variable, variableType, context, new SwiftName (variableName, false));
			initializerExpr.DiscretionaryString = context.ClassName.ToFullyQualifiedName (true);
			return initializerExpr;
		}

		SwiftType ConvertToGenericReference (Node node, bool isReference, SwiftName name)
		{
			long depth = node.Children [0].Index;
			long index = node.Children [1].Index;
			return new SwiftGenericArgReferenceType ((int)depth, (int)index, isReference, name);
		}

		SwiftType ConvertToBoundGeneric (Node node, bool isReference, SwiftName name)
		{
			var baseType = ConvertToSwiftType (node.Children [0], false, null);
			var boundTypes = ConvertTypeList (node.Children [1]);
			return new SwiftBoundGenericType (baseType, boundTypes, isReference, name);
		}

		List<SwiftType> ConvertTypeList (Node node)
		{
			var typeList = new List<SwiftType> (node.Children.Count);
			foreach (var childNode in node.Children) {
				typeList.Add (ConvertToSwiftType (childNode, false, null));
			}
			return typeList;
		}

		SwiftType ConvertToNamedTupleElement (Node node, bool isReference, SwiftName name)
		{
			var offset = node.Children [0].Kind == NodeKind.VariadicMarker ? 1 : 0;
			name = new SwiftName (node.Children [0 + offset].Text, false);
			SwiftType type = ConvertToSwiftType (node.Children [1 + offset], isReference, name);
			if (node.Children [0].Kind == NodeKind.VariadicMarker)
				type.IsVariadic = true;
			return type;
		}

		SwiftType ConvertToVariadicTupleElement (Node node, bool isReference, SwiftName name)
		{
			SwiftType type = ConvertToSwiftType (node.Children [1], isReference, name);
			type.IsVariadic = true;
			return type;
		}

		SwiftType ConvertToSubscript (Node node, bool isReference, SwiftName name, PropertyType propertyType)
		{
			SwiftClassType context = null;
			SwiftType extensionOn = null;
			string module = null;
			if (node.Children [0].Kind == NodeKind.Extension) {
				var extNode = node.Children [0];
				extensionOn = ConvertToSwiftType (extNode.Children [1], false, null);
				module = extNode.Children [0].Text;
			} else {
				context = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
				module = context.ClassName.Module.Name;
			}


			var propName = new SwiftName (node.Children [1].Text, false);
			var getterType = ConvertToSwiftType (node.Children [2], false, null) as SwiftFunctionType;
			if (propertyType == PropertyType.Setter && getterType.ReturnType != null && !getterType.ReturnType.IsEmptyTuple) {
				// oh hooray!
				// If I define an indexer in swift like this:
				// public subscript(T index) -> U {
				//    get { return getSomeUValue(index); }
				//    set (someUValue) { setSomeUValue(index, someUValue); }
				// }
				// This signature of the function attached to both properties is:
				// T -> U
				// which makes bizarre sense - the subscript() declaration is T -> U and the getter is T -> U, but
				// the setter is (T, U) -> void
				//
				// Since we have actual code that depends upon the signature, we need to "fix" this signature to reflect
				// what's really happening.

				// need to change this so that the tail parameters get names? Maybe just the head?
				SwiftTupleType newParameters = getterType.ParameterCount == 1 ?
									 new SwiftTupleType (false, null, getterType.ReturnType,
											     getterType.Parameters.RenamedCloneOf (new SwiftName (getterType.ReturnType.Name == null ||
																		  getterType.ReturnType.Name.Name != "a" ? "a" : "b", false)))
									 : new SwiftTupleType (Enumerable.Concat (getterType.ReturnType.Yield (), getterType.EachParameter),
								       false, null);
				getterType = new SwiftFunctionType (newParameters, SwiftTupleType.Empty, false, getterType.CanThrow, getterType.Name);

			}
			var prop = new SwiftPropertyType (context, propertyType, propName, null, getterType, false, isReference);
			prop.ExtensionOn = extensionOn;
			prop.DiscretionaryString = module;
			return prop;
		}

		SwiftType ConvertToSubscriptGetter (Node node, bool isReference, SwiftName name)
		{
			return ConvertToSubscript (node, isReference, name, PropertyType.Getter);
		}

		SwiftType ConvertToSubscriptSetter (Node node, bool isReference, SwiftName name)
		{
			return ConvertToSubscript (node, isReference, name, PropertyType.Setter);
		}

		SwiftType ConvertToProperty (Node node, bool isReference, SwiftName name, PropertyType propertyType)
		{
			var context = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			var privatePublicName = PrivateNamePublicName (node.Children [1]);
			var propName = new SwiftName (privatePublicName.Item2, false);
			var privateName = privatePublicName.Item1 != null ? new SwiftName (privatePublicName.Item1, false) : null;

			if (Decomposer.IsSubscript (propName))
				return ConvertToSubscript (node, isReference, name, propertyType);
			
			var getterType = ConvertToSwiftType (node.Children [2], false, propertyType == PropertyType.Setter ? new SwiftName ("newValue", false) : null);
			var prop = new SwiftPropertyType (context, propertyType, propName, privateName, getterType, false, isReference);
			prop.DiscretionaryString = context.ClassName.ToFullyQualifiedName (true);
			return prop;
		}

		SwiftType ConvertToGetter (Node node, bool isReference, SwiftName name)
		{
			return ConvertToProperty (node, isReference, name, PropertyType.Getter);
		}

		SwiftType ConvertToSetter (Node node, bool isReference, SwiftName name)
		{
			return ConvertToProperty (node, isReference, name, PropertyType.Setter);
		}

		SwiftType ConvertToMaterializer (Node node, bool isReference, SwiftName name)
		{
			if (node.Children [0].Kind == NodeKind.Extension) {
				return ConvertToGlobalProperty (node, isReference, name, PropertyType.Materializer);
			}
			return ConvertToProperty (node, isReference, name, PropertyType.Materializer);
		}

		SwiftType ConvertToDidSet (Node node, bool isReference, SwiftName name)
		{
			return ConvertToProperty (node, isReference, name, PropertyType.DidSet);
		}

		SwiftType ConvertToWillSet (Node node, bool isReference, SwiftName name)
		{
			return ConvertToProperty (node, isReference, name, PropertyType.WillSet);
		}


		SwiftType ConvertToGlobalProperty (Node node, bool isReference, SwiftName name, PropertyType propertyType)
		{
			string module = null;
			SwiftType extensionOn = null;
			if (node.Children [0].Kind == NodeKind.Extension) {
				var extNode = node.Children [0];
				module = extNode.Children [0].Text;
				extensionOn = ConvertToSwiftType (extNode.Children [1], false, null);
			} else {
				module = node.Children [0].Text;
			}
			SwiftName propName = null;
			SwiftName privateName = null;
			if (node.Children [1].Kind == NodeKind.PrivateDeclName) {
				propName = new SwiftName (node.Children [1].Children [0].Text, false);
				privateName = new SwiftName (node.Children [1].Children [1].Text, false);

			} else if (node.Children [1].Kind == NodeKind.Identifier) {
				propName = new SwiftName (node.Children [1].Text, false);
				// materializers are formatted oddly and come in
				// as globals like willSet and didSet
				// We don't care very much about materializers, but enough
				// that they should go to the correct place (subscript vs property).
				if (Decomposer.IsSubscript (propName)) {
					return ConvertToSubscript (node, isReference, name, propertyType);
				}
			}
			var getterType = ConvertToSwiftType (node.Children [2], false, null);
			var prop = new SwiftPropertyType (null, propertyType, propName, privateName, getterType, false, isReference, extensionOn);
			prop.DiscretionaryString = module;
			return prop;
		}

		SwiftType ConvertToGlobalGetter (Node node, bool isReference, SwiftName name)
		{
			return ConvertToGlobalProperty (node, isReference, name, PropertyType.Getter);
		}

		SwiftType ConvertToGlobalSetter (Node node, bool isReference, SwiftName name)
		{
			return ConvertToGlobalProperty (node, isReference, name, PropertyType.Setter);
		}

		SwiftType ConvertToUnnamedTupleElement (Node node, bool isReference, SwiftName name)
		{
			var offset = node.Children [0].Kind == NodeKind.VariadicMarker ? 1 : 0;
			return ConvertToSwiftType (node.Children [offset], false, null);
		}

		SwiftType ConvertToProtocolList (Node node, bool isReference, SwiftName name)
		{
			if (node.Children.Count != 1)
				throw new NotSupportedException ("ProtocolList node with more than 1 child not supported");
			if (node.Children [0].Kind != NodeKind.TypeList || node.Children [0].Children.Count > 1)
				throw new NotSupportedException ($"Given a ProtocolList node with child type {node.Children [0].Kind.ToString ()} and {node.Children [0].Children.Count} children, but expected a TypeList with exactly 1 child.");
			// If the number of elements is 0, it means that this is an "Any" type in swift.
			// I'm assuming it's lodged here as a protocol list is that an empty protocol list is
			// represented by an existential container which is also used to represent a protocol list.
			if (node.Children [0].Children.Count == 0) {
				var className = SwiftClassName.FromFullyQualifiedName ("Swift.Any", OperatorType.None, 'P');
				var classType = new SwiftClassType (className, isReference, name);
				return classType;
			}
			return ConvertToSwiftType (node.Children [0].Children [0], isReference, name);
		}

		SwiftType ConvertToProtocolWitnessTable (Node node, bool isReference, SwiftName name)
		{
			var witnessType = node.Kind == NodeKind.ProtocolWitnessTable ?
					      WitnessType.Protocol : WitnessType.ProtocolAccessor;
			var classType = ConvertToSwiftType (node.Children [0].Children [0], false, null) as SwiftClassType;
			var protoType = ConvertToSwiftType (node.Children [0].Children [1], false, null) as SwiftClassType;
			var protoWitness = new SwiftWitnessTableType (witnessType, protoType, classType);
			protoWitness.DiscretionaryString = node.Children [0].Children [2].Text;
			return protoWitness;
		}

		SwiftType ConvertToValueWitnessTable (Node node, bool isReference, SwiftName name)
		{
			var valueType = ConvertToSwiftType (node.Children [0], false, null) as SwiftClassType;
			var valueWitnessTable = new SwiftWitnessTableType (WitnessType.Value, null, valueType);
			valueWitnessTable.DiscretionaryString = valueType.ClassName.Module.Name;
			return valueWitnessTable;
		}

		SwiftType ConvertToFunction (Node node, bool isReference, SwiftName name)
		{
			string module = null;
			SwiftType extensionOn = null;
			if (node.Children [0].Kind == NodeKind.Extension) {
				var extNode = node.Children [0];
				module = extNode.Children [0].Text;
				extensionOn = ConvertToSwiftType (extNode.Children [1], false, null);
			} else {
				module = node.Children [0].Text;
			}
			var operatorType = ToOperatorType (node.Children [1].Kind);
			var funcName = operatorType == OperatorType.None ? PrivateNamePublicName (node.Children [1]).Item2 : node.Children [1].Text;
			var functionName = module +
					       (operatorType != OperatorType.None ? $".|{operatorType.ToString ()}" : "") +
					       "." + funcName;
			var function = ConvertToSwiftType (node.Children [2], isReference, new SwiftName (funcName, false)) as SwiftFunctionType;
			if (extensionOn != null)
				function.ExtensionOn = extensionOn;
			function.DiscretionaryString = functionName;
			return function;
		}

		SwiftType ConvertToFunctionType (Node node, bool isReference, SwiftName name)
		{
			var args = ConvertToSwiftType (node.Children [0], false, null);
			var ret = ConvertToSwiftType (node.Children [1], false, null);
			var function = new SwiftFunctionType (args, ret, isReference, false, name);
			return function;
		}

		SwiftType ConvertToCFunctionPointerType (Node node, bool isReference, SwiftName name)
		{
			var args = ConvertToSwiftType (node.Children [0], false, null);
			var ret = ConvertToSwiftType (node.Children [1], false, null);
			var function = new SwiftCFunctionPointerType (args, ret, isReference, false, name);
			return function;
		}

		SwiftType ConvertToFunctionThrowsType (Node node, bool isReference, SwiftName name)
		{
			var args = ConvertToSwiftType (node.Children [1], false, null);
			var ret = ConvertToSwiftType (node.Children [2], false, null);
			var function = new SwiftFunctionType (args, ret, isReference, true, name);
			return function;
		}

		SwiftType ConvertToStatic (Node node, bool isReference, SwiftName name)
		{
			var functionType = ConvertToSwiftType (node.Children [0], isReference, name);
			if (functionType == null)
				return null;
			var propType = functionType as SwiftPropertyType;
			if (propType != null) {
				return propType.RecastAsStatic ();
			}
			var uncurriedFunction = functionType as SwiftUncurriedFunctionType;
			if (uncurriedFunction != null) {
				var staticFunction = new SwiftStaticFunctionType (uncurriedFunction.Parameters, uncurriedFunction.ReturnType,
									      uncurriedFunction.IsReference, uncurriedFunction.CanThrow,
									      uncurriedFunction.UncurriedParameter as SwiftClassType, uncurriedFunction.Name);
				staticFunction.DiscretionaryString = uncurriedFunction.DiscretionaryString;
				return staticFunction;
			}
			var baseFunctionType = functionType as SwiftBaseFunctionType;
			if (baseFunctionType != null) {
				var staticFunction = new SwiftStaticFunctionType (baseFunctionType.Parameters, baseFunctionType.ReturnType,
										  baseFunctionType.IsReference, baseFunctionType.CanThrow,
										  null, baseFunctionType.Name);
				staticFunction.DiscretionaryString = baseFunctionType.DiscretionaryString;
				staticFunction.ExtensionOn = baseFunctionType.ExtensionOn;
				return staticFunction;
			}
			var initializerType = functionType as SwiftInitializerType;
			if (initializerType != null) {
				return initializerType; // this doesn't need a static recast?
			}
			throw new ArgumentOutOfRangeException ($"Expected a SwiftUncurriedFunctionType, a SwiftPropertyType, a SwiftBaseFunctionType or a SwiftInitializerType in a static node, but got {functionType.GetType ().Name}");
		}

		SwiftType ConvertToMethod (Node node, bool isReference, SwiftName name)
		{
			var instanceType = ConvertToSwiftType (node.Children [0], false, null);
			var sct = instanceType as SwiftClassType;
			if (sct == null)
				throw new NotSupportedException ($"Expected an SwiftClassType for the instance type but got {instanceType.GetType ().Name}.");
			var operatorType = ToOperatorType (node.Children [1].Kind);
			var funcName = operatorType == OperatorType.None ? PrivateNamePublicName (node.Children [1]).Item2 : node.Children [1].Text;
			var functionName = sct.ClassName.ToFullyQualifiedName (true) +
					      (operatorType != OperatorType.None ? $".|{operatorType.ToString ()}" : "") +
					      "." + funcName;

			sct.ClassName.Operator = operatorType;
			var functionType = node.Children [2].Children [0];
			var genericArguments = new List<GenericArgument> ();
			if (functionType.Children[0].Kind == NodeKind.DependentGenericSignature) {
				genericArguments = GetGenericArguments (functionType);
				functionType = functionType.Children [1].Children[0];
			}
			var throws = functionType.Children.Count == 3;
			var startIndex = throws ? 1 : 0;
			var args = ConvertToSwiftType (functionType.Children [startIndex + 0], false, null);
			var ret = ConvertToSwiftType (functionType.Children [startIndex + 1], false, null);
			var uncurriedFunction = new SwiftUncurriedFunctionType (instanceType, args, ret, isReference, throws, name);
			uncurriedFunction.DiscretionaryString = functionName;
			uncurriedFunction.GenericArguments.AddRange (genericArguments);
			return uncurriedFunction;
		}

		SwiftType ConvertToAnyObject(Node node, bool isReference, SwiftName name)
		{
			var className = SwiftClassName.FromFullyQualifiedName ("Swift.AnyObject", OperatorType.None, 'C');
			var classType = new SwiftClassType (className, isReference, name);
			return classType;
		}

		OperatorType ToOperatorType (NodeKind kind)
		{
			switch (kind) {
			default:
			case NodeKind.Identifier:
				return OperatorType.None;
			case NodeKind.PrefixOperator:
				return OperatorType.Prefix;
			case NodeKind.InfixOperator:
				return OperatorType.Infix;
			case NodeKind.PostfixOperator:
				return OperatorType.Postfix;
			}
		}

		SwiftType ConvertToAllocatingConstructor (Node node, bool isReference, SwiftName name)
		{
			return ConvertToConstructor (node, isReference, name, true);
		}

		SwiftType ConvertToNonAllocatingConstructor (Node node, bool isReference, SwiftName name)
		{
			return ConvertToConstructor (node, isReference, name, false);
		}

		SwiftType ConvertToConstructor (Node node, bool isReference, SwiftName name, bool isAllocating)
		{
			var instanceType = ConvertToSwiftType (node.Children [0], false, null);
			var sct = instanceType as SwiftClassType;
			if (sct == null)
				throw new NotSupportedException ($"Expected an SwiftClassType for the instance type in constructor but got {instanceType.GetType ().Name}.");
			var metadata = new SwiftMetaClassType (sct, false, null);
			var functionType = node.Children [1].Children [0];
			var functionThrows = functionType.Children.Count == 3 && functionType.Children [0].Kind == NodeKind.ThrowsAnnotation ? 1 : 0;
			var args = ConvertToSwiftType (functionType.Children [0 + functionThrows], false, null);
			var ret = ConvertToSwiftType (functionType.Children [1 + functionThrows], false, null);
			var constructor = new SwiftConstructorType (isAllocating, metadata, args, ret, isReference, functionThrows != 0);
			constructor.DiscretionaryString = sct.ClassName.ToFullyQualifiedName (true);
			return constructor;
		}

		SwiftType ConvertToDestructor (Node node, bool isReference, SwiftName name)
		{
			return ConvertToDestructor (node, isReference, name, false);
		}

		SwiftType ConvertToDeallocator (Node node, bool isReference, SwiftName name)
		{
			return ConvertToDestructor (node, isReference, name, true);
		}

		SwiftType ConvertToDestructor (Node node, bool isReference, SwiftName name, bool isDeallocating)
		{
			var instanceType = ConvertToSwiftType (node.Children [0], false, null);
			var sct = instanceType as SwiftClassType;
			if (sct == null)
				throw new NotSupportedException ($"Expected an SwiftClassType for the instance type in destructor but got {instanceType.GetType ().Name}.");
			var destructor = new SwiftDestructorType (isDeallocating, sct, isReference, false);
			destructor.DiscretionaryString = sct.ClassName.ToFullyQualifiedName (true);
			return destructor;
		}

		SwiftType ConvertToStruct (Node node, bool isReference, SwiftName name)
		{
			var className = ToSwiftClassName (node);
			var bit = TryAsBuiltInType (node, className, isReference, name);
			return (SwiftType)bit ?? new SwiftClassType (className, isReference, name);
		}

		SwiftType ConvertToClass (Node node, bool isReference, SwiftName name)
		{
			var className = ToSwiftClassName (node);
			return new SwiftClassType (className, isReference, name);
		}

		SwiftClassName ToSwiftClassName (Node node)
		{
			var memberNesting = new List<MemberNesting> ();
			var nestingNames = new List<SwiftName> ();
			var moduleName = BuildMemberNesting (node, memberNesting, nestingNames);

			moduleName = PatchClassName (moduleName, nestingNames);
			return new SwiftClassName (moduleName, memberNesting, nestingNames);
		}

		SwiftType ConvertToGenericFunction (Node node, bool isReference, SwiftName name)
		{
			List<GenericArgument> args = GetGenericArguments (node);
			var theFunction = ConvertToSwiftType (node.Children [1], isReference, name) as SwiftBaseFunctionType;
			theFunction.GenericArguments.AddRange (args);
			return theFunction;
		}

		List<GenericArgument> GetGenericArguments(Node node)
		{
			var paramCountNode = node.Children [0].Children [0];
			if (paramCountNode.Kind != NodeKind.DependentGenericParamCount)
				throw new NotSupportedException ($"Expected a DependentGenericParamCount node but got a {paramCountNode.Kind.ToString ()}");

			var paramCount = (int)paramCountNode.Index;
			List<GenericArgument> args = new List<GenericArgument> (paramCount);
			for (int i = 0; i < paramCount; i++) {
				args.Add (new GenericArgument (0, i));
			}
			if (node.Children [0].Children.Count > 1) {
				var dependentGenericSignature = node.Children [0];
				// the 0th child is the number of generic parameters (see above)
				for (int i = 1; i < dependentGenericSignature.Children.Count; i++) {
					var genericParamReference = ConvertToSwiftType (dependentGenericSignature.Children [i].Children [0], false, null) as SwiftGenericArgReferenceType;
					var genericConstraintType = ConvertToSwiftType (dependentGenericSignature.Children [i].Children [1], false, null) as SwiftClassType;
					MarkGenericConstraint (args, genericParamReference, genericConstraintType);
				}
			}
			return args;
		}

		static void MarkGenericConstraint (List<GenericArgument> args, SwiftGenericArgReferenceType paramReference, SwiftClassType constraintType)
		{
			foreach (var genArg in args) {
				if (genArg.Depth == paramReference.Depth && genArg.Index == paramReference.Index) {
					genArg.Constraints.Add (constraintType);
					return;
				}
			}
		}

		static SwiftBuiltInType TryAsBuiltInType (Node node, SwiftClassName className, bool isReference, SwiftName name)
		{
			switch (className.ToFullyQualifiedName ()) {
			case "Swift.Int":
				return new SwiftBuiltInType (CoreBuiltInType.Int, isReference, name);
			case "Swift.Float":
				return new SwiftBuiltInType (CoreBuiltInType.Float, isReference, name);
			case "Swift.Bool":
				return new SwiftBuiltInType (CoreBuiltInType.Bool, isReference, name);
			case "Swift.UInt":
				return new SwiftBuiltInType (CoreBuiltInType.UInt, isReference, name);
			case "Swift.Double":
				return new SwiftBuiltInType (CoreBuiltInType.Double, isReference, name);
			default:
				return null;
			}
		}

		static SwiftName BuildMemberNesting (Node node, List<MemberNesting> nestings, List<SwiftName> names)
		{
			var nesting = MemberNesting.Class;
			switch (node.Kind) {
			case NodeKind.Class:
				break;
			case NodeKind.Structure:
				nesting = MemberNesting.Struct;
				break;
			case NodeKind.Enum:
				nesting = MemberNesting.Enum;
				break;
			case NodeKind.Protocol:
				nesting = MemberNesting.Protocol;
				break;
			default:
				throw new ArgumentOutOfRangeException (nameof (node), $"Expected a nominal type node kind but got {node.Kind.ToString ()}");
			}

			var privatePublicName = PrivateNamePublicName (node.Children [1]);
			var className = new SwiftName (privatePublicName.Item2, false);
			SwiftName moduleName = null;
			if (node.Children [0].Kind == NodeKind.Identifier || node.Children [0].Kind == NodeKind.Module) {
				moduleName = new SwiftName (node.Children [0].Text, false);
			} else {
				// recurse before adding names.
				moduleName = BuildMemberNesting (node.Children [0], nestings, names);
			}
			names.Add (className);
			nestings.Add (nesting);
			return moduleName;
		}

		static Node ExtractFunctionType (Node n)
		{
			if (n.Kind == NodeKind.Type && n.Children.Count == 1 && n.Children [0].Kind == NodeKind.FunctionType) {
				return n.Children [0];
			}
			return null;
		}


		static Tuple<string, string> PrivateNamePublicName (Node node)
		{
			if (node.Kind == NodeKind.Identifier)
				return new Tuple<string, string> (null, node.Text);
			if (node.Kind == NodeKind.PrivateDeclName)
				return new Tuple<string, string> (node.Children [1].Text, node.Children [0].Text);
			throw new ArgumentOutOfRangeException (nameof (node));
		}



		// Swift does...weird...things with some of the core types from C.
		// The mangler doesn't put in the appropriate swift module, but instead lumps them all
		// together into the module __C.
		// The mangler also puts a number of Foundation types into the namespace __ObjC.
		// Why? No idea.
		// I determined this list of __ObjC types by running the following command on all the Apple built libraries:
		// find . -name "*.dylib" -exec nm {} \; | xcrun swift-demangle | grep "type metadata accessor for __ObjC" | awk '{print $7}' | sort | uniq
		// which also includes a number of inner types which we don't care about (yet).

		// The command to do this for the __C namespace is:
		// find . -name "*.dylib" -exec nm {} \; | xcrun swift-demangle | grep "_type metadata for __C" | awk '{print $6}' | sort | uniq

		static Dictionary<string, string> classNameOntoModule = new Dictionary<string, string> {
			{ "AVError", "AVFoundation" },
			{ "AudioBuffer", "AudioToolbox" },
			{ "AudioBufferList", "AudioToolbox" },
			{ "CATransform3D", "CoreAnimation" },
			{ "CGAffineTransform", "CoreGraphics" },
			{ "CGColorSapceModel", "CoreGraphics" },
			{ "CGPoint", "CoreGraphics" },
			{ "CGRect", "CoreGraphics" },
			{ "CGSize", "CoreGraphics" },
			{ "CGVector", "CoreGraphics" },
			{ "CLError", "CoreLocation" },
			{ "CMTime", "CoreMedia" },
			{ "CMTimeFlags", "CoreMedia" },
			{ "CMTimeMapping", "CoreMedia" },
			{ "CMTimeRange", "CoreMedia" },
			{ "NSComparisonResult", "Foundation" },
			{ "NSDecimal", "Foundation" },
			{ "NSEnumerationOptions", "Foundation" },
			{ "NSKeyValueChange", "Foundation" },
			{ "NSKeyValueObservingOptions", "Foundation" },
			{ "NSFastEnumerationState", "Foundation" },
			{ "NSKeyValueChangeKey", "Foundation" },
			{ "StringTransform", "Foundation" },
			{ "URLFileResourceType", "Foundation" },
			{ "URLResourceKey", "Foundation" },
			{ "URLThumbnailDictionaryItem", "Foundation" },
			{ "URLUbiquitousItemDownloadingStatus", "Foundation" },
			{ "URLUbiquitousSharedItemPermissions", "Foundation" },
			{ "URLUbiquitousSharedItemRole", "Foundation" },
			{ "MKCoordinateSpan", "MapKit" },
			{ "NSAnimationEffect", "AppKit" },
			{ "SCNGeometryPrimitiveType", "SceneKit" },
			{ "SCNVector3", "SceneKit" },
			{ "SCNVector4", "SceneKit" },
			{ "UIContentSizeCategory", "UIKit" },
			{ "UIDeviceOrientation", "UIKit" },
			{ "UIEdgeInsets", "UIKit" },
			{ "UIInterfaceOrientation", "UIKit" },
			{ "UIOffset", "UIKit" },
			{ "CKError", "CloudKit" },
			{ "CNError", "Contacts" },
			{ "MTLSamplePosition", "Metal" },
			{ "XCUIKeyboardKey", "XCTest" },
			{ "BNNSActivationFunction", "Accelerate" },
			{ "BNNSDataType", "Accelerate" },
			{ "simd_double2x2", "Accelerate" },
			{ "simd_double2x3", "Accelerate" },
			{ "simd_double2x4", "Accelerate" },
			{ "simd_double3x2", "Accelerate" },
			{ "simd_double3x3", "Accelerate" },
			{ "simd_double3x4", "Accelerate" },
			{ "simd_double4x2", "Accelerate" },
			{ "simd_double4x3", "Accelerate" },
			{ "simd_double4x4", "Accelerate" },
			{ "simd_float2x2", "Accelerate" },
			{ "simd_float2x3", "Accelerate" },
			{ "simd_float2x4", "Accelerate" },
			{ "simd_float3x2", "Accelerate" },
			{ "simd_float3x3", "Accelerate" },
			{ "simd_float3x4", "Accelerate" },
			{ "simd_float4x2", "Accelerate" },
			{ "simd_float4x3", "Accelerate" },
			{ "simd_float4x4", "Accelerate" },
			{ "simd_quatd", "Accelerate" },
			{ "simd_quatf", "Accelerate" }
		};


		static SwiftName PatchClassName (SwiftName moduleName, List<SwiftName> nestingNames)
		{
			// surprise!
			// When we run XML reflection, the module name we get is ObjectiveC, but in the name mangled version
			// it's __ObjC. This is the only place in this code where we make a module name, so it's a decent enough
			// bottleneck to alias it.
			if (moduleName.Name == "__ObjC")
				moduleName = new SwiftName ("Foundation", false);
			if (moduleName.Name != "__C")
				return moduleName;
			if (nestingNames.Count != 1)
				return moduleName;
			string result = null;
			if (classNameOntoModule.TryGetValue (nestingNames [0].Name, out result))
				return new SwiftName (result, false);
			return moduleName;
		}
	}
}
