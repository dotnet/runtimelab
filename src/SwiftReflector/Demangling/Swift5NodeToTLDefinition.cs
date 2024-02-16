// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftRuntimeLibrary;

namespace SwiftReflector.Demangling
{
    public class Swift5NodeToTLDefinition
    {
        static List<NodeKind> nominalNodeKinds = new List<NodeKind> {
            NodeKind.Class, NodeKind.Enum, NodeKind.Structure, NodeKind.Protocol
        };

        static List<NodeKind> nominalNodeAndModuleKinds = new List<NodeKind> {
            NodeKind.Class, NodeKind.Enum, NodeKind.Structure, NodeKind.Protocol, NodeKind.Module
        };

        static List<NodeKind> identifierOrOperatorOrPrivateDecl = new List<NodeKind> {
            NodeKind.Identifier, NodeKind.PrefixOperator, NodeKind.InfixOperator, NodeKind.PostfixOperator, NodeKind.PrivateDeclName
        };

        static List<NodeKind> boundGenericNominalNodeKinds = new List<NodeKind> {
            NodeKind.BoundGenericEnum, NodeKind.BoundGenericClass, NodeKind.BoundGenericStructure
        };

        string mangledName;
        ulong offset;

        RuleRunner ruleRunner;
        List<MatchRule> rules;

        public Swift5NodeToTLDefinition(string mangledName, ulong offset = 0)
        {
            this.mangledName = mangledName;
            this.offset = offset;
            rules = BuildMatchRules();
            ruleRunner = new RuleRunner(rules);
        }

        List<MatchRule> BuildMatchRules()
        {
            return new List<MatchRule>() {
                new MatchRule {
                    Name = "TypeAlias",
                        NodeKind = NodeKind.TypeAlias,
                    Reducer = ConvertToTypeAlias
                },
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
                new MatchRule  {
                    Name = "AutoClosure",
                    NodeKind = NodeKind.AutoClosureType,
                    Reducer = ConvertToFunctionType
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
                    Name = "Getter",
                        NodeKind = NodeKind.Getter,
                    Reducer = ConvertToGetter,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "GetterChild",
                                NodeKind = NodeKind.Variable
                        }
                    }

                },
                new MatchRule {
                    Name = "Setter",
                        NodeKind = NodeKind.Setter,
                    Reducer = ConvertToSetter,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "SetterChild",
                                NodeKind = NodeKind.Variable
                        }
                    }

                },
                new MatchRule {
                    Name = "DispatchThunk",
                    NodeKind = NodeKind.DispatchThunk,
                    Reducer = ConvertToDispatchThunk,
                    ChildRules = new List<MatchRule> () { }
                },
                new MatchRule {
                    Name = "DynamicSelf",
                        NodeKind = NodeKind.DynamicSelf,
                    Reducer = ConvertFirstChildToSwiftType
                },
                new MatchRule {
                    Name = "SubscriptModifier",
                        NodeKind = NodeKind.ModifyAccessor,
                    Reducer = ConvertToSubscriptModifier,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "SubscriptModifierChild",
                                NodeKind = NodeKind.Subscript
                        }
                    }
                },
                new MatchRule {
                    Name = "SubscriptGetter",
                        NodeKind = NodeKind.Getter,
                    Reducer = ConvertToSubscriptGetter,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "SubscriptSetterChild",
                                NodeKind = NodeKind.Subscript
                        }
                    }
                },
                new MatchRule {
                    Name = "SubscriptSetter",
                        NodeKind = NodeKind.Setter,
                    Reducer = ConvertToSubscriptSetter,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "SubscriptSetterChild",
                                NodeKind = NodeKind.Subscript
                        }
                    }
                },
                new MatchRule {
                    Name = "DidSet",
                    NodeKind = NodeKind.DidSet,
                    Reducer = ConvertToDidSet,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "DidSetChild",
                            NodeKind = NodeKind.Variable,
                        },
                    }
                },
                new MatchRule {
                    Name = "WillSet",
                    NodeKind = NodeKind.WillSet,
                    Reducer = ConvertToWillSet,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "DidSetChild",
                            NodeKind = NodeKind.Variable,
                        },
                    }
                },
                new MatchRule {
                    Name = "ModifyAccessor",
                        NodeKind = NodeKind.ModifyAccessor,
                    Reducer = ConvertToModifyAccessor,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "ModifyAccessorChild",
                                NodeKind = NodeKind.Variable
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
                    Name = "ProtocolListWithAnyObject",
                    NodeKind = NodeKind.ProtocolListWithAnyObject,
                    Reducer = ConvertToProtocolListAnyObject,
                },
                new MatchRule {
                    Name = "Protocol",
                    NodeKind = NodeKind.Protocol,
                    Reducer = ConvertToClass
                },
                new MatchRule {
                    Name = "TupleElement",
                    NodeKind = NodeKind.TupleElement,
                    Reducer = ConvertToTupleElement,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "TupleElementType",
                            NodeKind = NodeKind.Type
                        }
                    }
                },
                new MatchRule {
                    Name = "NamedTupleElement",
                    NodeKind = NodeKind.TupleElement,
                    Reducer = ConvertToTupleElement,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "TupleElementName",
                                NodeKind = NodeKind.TupleElementName
                        },
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
                    Reducer = ConvertToDependentMember,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "Type",
                            NodeKind = NodeKind.Type
                        },
                    }
                },
                new MatchRule {
                    Name = "Static",
                    NodeKind = NodeKind.Static,
                    Reducer = ConvertToStatic,
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
                    Name = "ConstructorList",
                    NodeKind = NodeKind.Constructor,
                    Reducer = ConvertToNonAllocatingConstructor,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "ConstructorNominalChild",
                            NodeKindList = nominalNodeKinds,
                        },
                        new MatchRule {
                            Name = "FunctionLabelListChild",
                                NodeKind = NodeKind.LabelList,
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
                    Name = "AllocatorNoTypeList",
                    NodeKind = NodeKind.Allocator,
                    Reducer = ConvertToAllocatingConstructor,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "AllocatorNominalChild",
                            NodeKindList = nominalNodeKinds,
                        },
                        new MatchRule {
                            Name = "FunctionLabelListChild",
                                NodeKind = NodeKind.LabelList,
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
                    Name = "AllocatorExtensionLableList",
                    NodeKind = NodeKind.Allocator,
                    Reducer = ConvertToAllocatingConstructor,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "ExtensionChild",
                            NodeKind = NodeKind.Extension,
                        },
                        new MatchRule {
                            Name = "FunctionLabelListChild",
                                NodeKind = NodeKind.LabelList,
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
                    Name = "ConstructorNoTypeList",
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
                    Name = "AllocatorNoTypeList",
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
                    Name = "AllocatorExtensionsNoTypeList",
                    NodeKind = NodeKind.Allocator,
                    Reducer = ConvertToAllocatingConstructor,
                    ChildRules = new List<MatchRule> {
                        new MatchRule {
                            Name = "AllocatorNominalChild",
                            NodeKind = NodeKind.Extension,
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
                                    NodeKind = NodeKind.Module
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
				// a Function is:
				// Context Identifier [LabelList] Type FunctionType
				new MatchRule () {
                    Name = "FunctionWithLabelList",
                        NodeKind = NodeKind.Function,
                    Reducer = ConvertToFunction,
                    ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "FunctionTLContext",
                                NodeKindList = nominalNodeAndModuleKinds,
                        },
                        new MatchRule () {
                            Name = "FunctionTLName",
                                NodeKindList = identifierOrOperatorOrPrivateDecl
                        },
                        new MatchRule () {
                            Name = "FunctionTLLabelList",
                                NodeKind = NodeKind.LabelList,
                        },
                        new MatchRule () {
                            Name = "FunctionTLType",
                                NodeKind = NodeKind.Type
                        },
                    }
                },
                new MatchRule () {
                    Name = "FunctionExtensionWithLabelList",
                        NodeKind = NodeKind.Function,
                    Reducer = ConvertToFunction,
                    ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "FunctionExtension",
                                NodeKind = NodeKind.Extension,
                        },
                        new MatchRule () {
                            Name = "FunctionExtTLName",
                                NodeKindList = identifierOrOperatorOrPrivateDecl
                        },
                        new MatchRule () {
                            Name = "FunctionExtTLLabelList",
                                NodeKind = NodeKind.LabelList,
                        },
                        new MatchRule () {
                            Name = "FunctionExtTLType",
                                NodeKind = NodeKind.Type
                        },
                    }
                },
                new MatchRule {
                    Name = "GenericFunctionWithLabelList",
                    NodeKind = NodeKind.Function,
                    Reducer = ConvertToFunction,
                    ChildRules = new List<MatchRule> {
                        new MatchRule () {
                            Name = "GenFunctionTLContext",
                                NodeKindList = nominalNodeAndModuleKinds,
                        },
                        new MatchRule () {
                            Name = "GenFunctionTLName",
                                NodeKindList = identifierOrOperatorOrPrivateDecl
                        },
                        new MatchRule () {
                            Name = "FunctionTLLabelList",
                                NodeKind = NodeKind.LabelList,
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
                new MatchRule () {
                    Name = "FunctionNoTypeList",
                        NodeKind = NodeKind.Function,
                    Reducer = ConvertToFunction,
                    ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "FunctionContext",
                                NodeKindList = nominalNodeAndModuleKinds,
                        },
                        new MatchRule () {
                            Name = "FunctionName",
                                NodeKindList = identifierOrOperatorOrPrivateDecl
                        },
                        new MatchRule () {
                            Name = "FunctionType",
                                NodeKind = NodeKind.Type
                        },
                    }
                },
                new MatchRule () {
                    Name = "FunctionExtensionNoTypeList",
                        NodeKind = NodeKind.Function,
                    Reducer = ConvertToFunction,
                    ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "FunctionExtension",
                                NodeKind = NodeKind.Extension,
                        },
                        new MatchRule () {
                            Name = "FunctionName",
                                NodeKindList = identifierOrOperatorOrPrivateDecl
                        },
                        new MatchRule () {
                            Name = "FunctionType",
                                NodeKind = NodeKind.Type
                        },
                    }
                },
                new MatchRule () {
                    Name = "FunctionType",
                        NodeKind = NodeKind.FunctionType,
                    Reducer = ConvertToFunctionType,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "FunctionTypeArgs",
                                NodeKind = NodeKind.ArgumentTuple
                        },
                        new MatchRule () {
                            Name = "FunctionTypeReturn",
                                NodeKind = NodeKind.ReturnType
                        }
                    }
                },
                new MatchRule () {
                    Name = "FunctionThrows",
                        NodeKind = NodeKind.FunctionType,
                    Reducer = ConvertToFunctionType,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "FunctionTypeThrows",
                                NodeKind = NodeKind.ThrowsAnnotation
                        },
                        new MatchRule () {
                            Name = "FunctionTypeArgs",
                                NodeKind = NodeKind.ArgumentTuple
                        },
                        new MatchRule () {
                            Name = "FunctionTypeReturn",
                                NodeKind = NodeKind.ReturnType
                        }
                    }
                },
                new MatchRule () {
                    Name = "NoEscapeFunctionType",
                        NodeKind = NodeKind.NoEscapeFunctionType,
                    Reducer = ConvertToNoEscapeFunctionType,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "FunctionTypeArgs",
                                NodeKind = NodeKind.ArgumentTuple
                        },
                        new MatchRule () {
                            Name = "FunctionTypeReturn",
                                NodeKind = NodeKind.ReturnType
                        }
                    }
                },
                new MatchRule () {
                    Name = "NoEscapeFunctionTypeThrows",
                        NodeKind = NodeKind.NoEscapeFunctionType,
                    Reducer = ConvertToNoEscapeFunctionType,
                        ChildRules = new List<MatchRule> () {
                        new MatchRule () {
                            Name = "FunctionTypeThrows",
                                NodeKind = NodeKind.ThrowsAnnotation
                        },
                        new MatchRule () {
                            Name = "FunctionTypeArgs",
                                NodeKind = NodeKind.ArgumentTuple
                        },
                        new MatchRule () {
                            Name = "FunctionTypeReturn",
                                NodeKind = NodeKind.ReturnType
                        }
                    }
                },
                new MatchRule () {
                    Name = "Metatype",
                    NodeKind = NodeKind.Metatype,
                    Reducer = ConvertToMetatype,
                },
                new MatchRule () {
                    Name = "ExistentialMetatype",
                        NodeKind = NodeKind.ExistentialMetatype,
                    Reducer = ConvertToExistentialMetatype,
                        MatchChildCount = false
                },
				// Probably should be last
				new MatchRule {
                    Name = "Type",
                    NodeKind = NodeKind.Type,
                    Reducer = ConvertFirstChildToSwiftType,
                    MatchChildCount = false
                },
            };
        }

        public TLDefinition Convert(Node node)
        {
            Exceptions.ThrowOnNull(node, nameof(node));


            switch (node.Kind)
            {
                case NodeKind.Type:
                    return Convert(node.Children[0]);
                case NodeKind.Static:
                    return ConvertStatic(node);
                case NodeKind.Function:
                    return ConvertFunction(node, isMethodDescriptor: false, isEnumCase: false);
                case NodeKind.MethodDescriptor:
                    return ConvertFunction(node.Children[0], isMethodDescriptor: true, isEnumCase: false);
                case NodeKind.EnumCase:
                    return ConvertFunction(node.Children[0], isMethodDescriptor: false, isEnumCase: true);
                case NodeKind.Constructor:
                case NodeKind.Allocator:
                    return ConvertFunctionConstructor(node);
                case NodeKind.Destructor:
                case NodeKind.Deallocator:
                    return ConvertFunctionDestructor(node);
                case NodeKind.Getter:
                case NodeKind.Setter:
                case NodeKind.DidSet:
                case NodeKind.MaterializeForSet:
                case NodeKind.WillSet:
                case NodeKind.ModifyAccessor:
                    return ConvertFunctionProp(node);
                case NodeKind.DispatchThunk:
                    return ConvertDispatchThunk(node);
                case NodeKind.Variable:
                    return ConvertVariable(node, false);
                case NodeKind.PropertyDescriptor:
                    return ConvertPropertyDescriptor(node);
                case NodeKind.TypeMetadataAccessFunction:
                    return ConvertCCtor(node);
                case NodeKind.TypeMetadata:
                    return ConvertMetadata(node);
                case NodeKind.NominalTypeDescriptor:
                    return ConvertNominalTypeDescriptor(node);
                case NodeKind.ProtocolConformanceDescriptor:
                    return ConvertProtocolConformanceDescriptor(node);
                case NodeKind.ProtocolDescriptor:
                    return ConvertProtocolDescriptor(node);
                case NodeKind.Initializer:
                    return ConvertInitializer(node);
                case NodeKind.TypeMetadataLazyCache:
                    return ConvertLazyCacheVariable(node);
                case NodeKind.ProtocolWitnessTable:
                case NodeKind.ProtocolWitnessTableAccessor:
                case NodeKind.ValueWitnessTable:
                    return ConvertProtocolWitnessTable(node);
                case NodeKind.FieldOffset:
                    return ConvertFieldOffset(node);
                case NodeKind.DefaultArgumentInitializer:
                    return ConvertDefaultArgumentInitializer(node);
                case NodeKind.Metaclass:
                    return ConvertMetaclass(node);
                case NodeKind.UnsafeMutableAddressor:
                    return ConvertUnsafeMutableAddressor(node);
                case NodeKind.CurryThunk:
                    return ConvertCurryThunk(node);
                case NodeKind.GenericTypeMetadataPattern:
                    return ConvertTypeMetadataPattern(node);
                case NodeKind.Global:
                    return Convert(node);
                case NodeKind.ModuleDescriptor:
                    return ConvertModuleDescriptor(node);
                case NodeKind.ReflectionMetadataFieldDescriptor:
                    return ConvertMetadataFieldDescriptor(node, false);
                case NodeKind.ReflectionMetadataBuiltinDescriptor:
                    return ConvertMetadataFieldDescriptor(node, true);
                case NodeKind.ProtocolRequirementsBaseDescriptor:
                    return ConvertProtocolRequirementsBaseDescriptor(node);
                case NodeKind.BaseConformanceDescriptor:
                    return ConvertBaseConformanceDescriptor(node);
                case NodeKind.AssociatedTypeDescriptor:
                    return ConvertAssocatedTypeDescriptor(node);
                case NodeKind.ClassMetadataBaseOffset:
                    return ConvertMetadataBaseOffset(node);
                case NodeKind.MethodLookupFunction:
                    return ConvertMethodLookupFunction(node);
                default:
                    return null;
            }
        }


        TLDefinition ConvertDispatchThunk(Node node)
        {
            switch (node.Children[0].Kind)
            {
                case NodeKind.Getter:
                case NodeKind.Setter:
                case NodeKind.DidSet:
                case NodeKind.MaterializeForSet:
                case NodeKind.WillSet:
                case NodeKind.ModifyAccessor:
                    return ConvertFunctionProp(node);
                case NodeKind.Static:
                    return ConvertStaticDispatchThunk(node);
                case NodeKind.Function:
                case NodeKind.Allocator:
                    return ConvertFunction(node, isMethodDescriptor: false, isEnumCase: false);
                default:
                    return null;
            }
        }

        TLDefinition ConvertStaticDispatchThunk(Node node)
        {
            if (node.Children[0].Children[0].Kind == NodeKind.Variable)
            {
                return ConvertStaticDispatchThunkVariable(node);
            }
            else
            {
                return ConvertStatic(node);
            }
        }

        TLDefinition ConvertStaticDispatchThunkVariable(Node node)
        {
            return null;
        }


        TLFunction ConvertFunction(Node node, bool isMethodDescriptor, bool isEnumCase)
        {
            var swiftType = ConvertToSwiftType(node, false, null);
            var uncurriedFunction = swiftType as SwiftUncurriedFunctionType;
            if (uncurriedFunction != null)
            {
                // method
                var context = uncurriedFunction.DiscretionaryString.Split('.');
                var module = new SwiftName(context[0], false);
                var functionName = new SwiftName(context.Last(), false);
                return isMethodDescriptor ?
                    new TLMethodDescriptor(mangledName, module, functionName, uncurriedFunction.UncurriedParameter as SwiftClassType,
                              uncurriedFunction, offset) :
                    isEnumCase ?
                    new TLEnumCase(mangledName, module, functionName, uncurriedFunction.UncurriedParameter as SwiftClassType,
                              uncurriedFunction, offset) :
                    new TLFunction(mangledName, module, functionName, uncurriedFunction.UncurriedParameter as SwiftClassType,
                              uncurriedFunction, offset);
            }

            var plainFunction = swiftType as SwiftFunctionType;
            if (plainFunction != null)
            {
                var context = plainFunction.DiscretionaryString.Split('.');
                var module = new SwiftName(context[0], false);
                var operatorType = OperatorType.None;
                if (context.Length > 2 && context[context.Length - 2][0] == '|')
                {
                    Enum.TryParse(context[context.Length - 2].Substring(1), out operatorType);
                }
                var functionName = new SwiftName(context.Last(), false);
                return isMethodDescriptor ?
                    new TLMethodDescriptor(mangledName, module, functionName, null, plainFunction, offset, operatorType) :
                    new TLFunction(mangledName, module, functionName, null, plainFunction, offset, operatorType);
            }
            return null;
        }

        TLDefinition ConvertStatic(Node node)
        {
            if (node.Children[0].Kind == NodeKind.Variable)
            {
                return ConvertVariable(node.Children[0], true);
            }
            var swiftType = ConvertToSwiftType(node, false, null);
            var propType = swiftType as SwiftPropertyType;
            if (propType != null)
            {
                var context = propType.DiscretionaryString.Split('.');
                var uncurriedParam = propType.UncurriedParameter as SwiftClassType;
                return new TLFunction(mangledName, new SwiftName(context[0], false), propType.Name, uncurriedParam, propType, offset);
            }

            var staticFunction = swiftType as SwiftStaticFunctionType;
            if (staticFunction != null)
            {
                var context = staticFunction.DiscretionaryString.Split('.');
                var module = new SwiftName(context[0], false);
                var operatorType = OperatorType.None;
                if (context.Length > 2 && context[context.Length - 2][0] == '|')
                {
                    Enum.TryParse(context[context.Length - 2].Substring(1), out operatorType);
                }
                var functionName = new SwiftName(context.Last(), false);
                return new TLFunction(mangledName, module, functionName, staticFunction.OfClass, staticFunction, offset, operatorType);
            }
            return null;
        }

        TLFunction ConvertFunctionConstructor(Node node)
        {
            var swiftType = ConvertToSwiftType(node, false, null);
            var constructor = swiftType as SwiftConstructorType;
            if (constructor != null)
            {
                var context = constructor.DiscretionaryString.Split('.');
                var module = new SwiftName(context[0], false);
                var functionName = constructor.Name;
                var metaType = constructor.UncurriedParameter as SwiftMetaClassType;
                return new TLFunction(mangledName, module, functionName, metaType.Class, constructor, offset);
            }
            return null;
        }

        TLFunction ConvertFunctionDestructor(Node node)
        {
            var swiftType = ConvertToSwiftType(node, false, null);
            var destructor = swiftType as SwiftDestructorType;
            if (destructor != null)
            {
                var context = destructor.DiscretionaryString.Split('.');
                var module = new SwiftName(context[0], false);
                var functionName = destructor.Name;
                var className = destructor.Parameters as SwiftClassType;
                if (className == null)
                    throw new NotSupportedException($"Expected a SwiftClassType as the parameter to the destructor bute got {destructor.Parameters.GetType().Name}");
                return new TLFunction(mangledName, module, functionName, className, destructor, offset);
            }
            return null;
        }

        TLFunction ConvertFunctionProp(Node node)
        {
            var propType = ConvertToSwiftType(node, false, null) as SwiftPropertyType;
            if (propType == null)
                return null;
            var context = propType.DiscretionaryString.Split('.');
            var uncurriedParam = propType.UncurriedParameter as SwiftClassType;
            return new TLFunction(mangledName, new SwiftName(context[0], false), propType.Name, uncurriedParam, propType, offset);
        }

        TLVariable ConvertVariable(Node node, bool isStatic)
        {
            // Variable
            // 
            if (node.Children.Count != 3 && node.Children.Count != 4)
                throw new ArgumentOutOfRangeException(nameof(node), $"Expected 3 or 4 children in a variable node, but got {node.Children.Count}");

            if (node.Kind == NodeKind.Subscript)
            {
                var subFunc = ConvertToSubscriptEtter(node, false, null, PropertyType.Getter) as SwiftPropertyType;
                var module = subFunc.DiscretionaryString.Split('.')[0];
                return new TLVariable(mangledName, new SwiftName(module, false), subFunc.UncurriedParameter as SwiftClassType,
                    new SwiftName("subscript", false), subFunc.OfType, isStatic, offset);
            }

            if (node.Children[2].Kind == NodeKind.LabelList && node.Children[3].Kind == NodeKind.Type &&
                    node.Children[3].Children[0].Kind == NodeKind.FunctionType)
            {
                var functionNode = new Node(NodeKind.Function);
                functionNode.Children.AddRange(node.Children);
                var function = ConvertFunction(functionNode, isMethodDescriptor: false, isEnumCase: false);
                SwiftClassType classOn = null;
                if (function.Signature is SwiftUncurriedFunctionType ucf)
                {
                    classOn = ucf.UncurriedParameter as SwiftClassType;
                }
                return new TLVariable(mangledName, function.Module, classOn, function.Name, function.Signature, isStatic, offset);
            }
            else
            {
                var isExtension = node.Children[0].Kind == NodeKind.Extension;
                SwiftName module = null;
                SwiftType extensionOn = null;
                SwiftClassType context = null;


                if (isExtension)
                {
                    module = new SwiftName(node.Children[0].Children[0].Text, false);
                    extensionOn = ConvertToSwiftType(node.Children[0].Children[1], false, null);
                    if (extensionOn == null)
                        return null;
                }
                else
                {
                    var nesting = new List<MemberNesting>();
                    var nestingNames = new List<SwiftName>();
                    module = BuildMemberNesting(node.Children[0], nesting, nestingNames);
                    context = nesting.Count > 0 ? new SwiftClassType(new SwiftClassName(module, nesting, nestingNames), false) : null;
                }

                var name = new SwiftName(node.Children[1].Text, false);
                var variableType = ConvertToSwiftType(node.Children[2], false, null);
                return new TLVariable(mangledName, module, context, name, variableType, isStatic, offset, extensionOn);
            }
        }

        TLPropertyDescriptor ConvertPropertyDescriptor(Node node)
        {
            var tlvar = ConvertVariable(node.Children[0], false);
            if (tlvar == null)
                return null;
            return new TLPropertyDescriptor(tlvar.MangledName, tlvar.Module, tlvar.Class, tlvar.Name, tlvar.OfType, false, tlvar.Offset, tlvar.ExtensionOn);
        }

        TLFunction ConvertCCtor(Node node)
        {
            var classType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            var metaType = new SwiftMetaClassType(classType, false);
            var cctor = new SwiftClassConstructorType(metaType, false);
            return new TLFunction(mangledName, classType.ClassName.Module, Decomposer.kSwiftClassConstructorName, classType,
                          cctor, offset);
        }

        TLDefaultArgumentInitializer ConvertDefaultArgumentInitializer(Node node)
        {
            if (node.Children.Count != 2)
                return null;
            var baseFunction = ConvertToSwiftType(node.Children[0], false, null) as SwiftBaseFunctionType;
            if (baseFunction == null)
                return null;
            var context = baseFunction.DiscretionaryString.Split('.');
            var module = new SwiftName(context[0], false);
            var argumentIndex = (int)node.Children[1].Index;
            return new TLDefaultArgumentInitializer(mangledName, module, baseFunction, argumentIndex, offset);
        }

        TLFieldOffset ConvertFieldOffset(Node node)
        {
            if (node.Children.Count != 2 || node.Children[0].Kind != NodeKind.Directness)
                return null;
            var variable = ConvertVariable(node.Children[1], false) as TLVariable;
            if (variable == null)
                return null;

            return new TLFieldOffset(mangledName, variable.Module, variable.Class, node.Children[0].Index == 0, variable.Name,
                variable.OfType, offset);
        }

        TLDirectMetadata ConvertMetadata(Node node)
        {
            var classType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            return new TLDirectMetadata(mangledName, classType.ClassName.Module, classType, offset);
        }

        TLNominalTypeDescriptor ConvertNominalTypeDescriptor(Node node)
        {
            var classType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            return new TLNominalTypeDescriptor(mangledName, classType.ClassName.Module, classType, offset);
        }

        TLProtocolConformanceDescriptor ConvertProtocolConformanceDescriptor(Node node)
        {
            node = node.Children[0];
            if (node.Kind != NodeKind.ProtocolConformance)
                return null;
            if (node.Children.Count != 3)
                return null;
            var implementingType = ConvertToSwiftType(node.Children[0], false, null);
            if (implementingType == null)
                return null;
            var forProtocol = ConvertToSwiftType(node.Children[1], false, null) as SwiftClassType;
            if (forProtocol == null)
                return null;
            var module = new SwiftName(node.Children[2].Text, false);
            return new TLProtocolConformanceDescriptor(mangledName, module, implementingType, forProtocol, offset);
        }

        TLProtocolTypeDescriptor ConvertProtocolDescriptor(Node node)
        {
            var classType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            return new TLProtocolTypeDescriptor(mangledName, classType.ClassName.Module, classType, offset);
        }

        TLUnsafeMutableAddressor ConvertUnsafeMutableAddressor(Node node)
        {
            if (node.Children[0].Kind != NodeKind.Variable)
                throw new ArgumentOutOfRangeException($"Expected a Variable child but got a {node.Children[0].Kind}");
            var variable = ConvertVariable(node.Children[0], false);
            return new TLUnsafeMutableAddressor(mangledName, variable.Module, null, variable.Name, variable.OfType, variable.Offset);
        }

        TLMetaclass ConvertMetaclass(Node node)
        {
            if (node.Children[0].Kind != NodeKind.Type)
                return null;
            var classType = ConvertToSwiftType(node.Children[0].Children[0], false, null) as SwiftClassType;
            if (classType == null)
                return null;
            var module = classType.ClassName.Module;
            return new TLMetaclass(mangledName, module, classType, offset);
        }

        TLGenericMetadataPattern ConvertTypeMetadataPattern(Node node)
        {
            var type = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            if (type == null)
                return null;
            return new TLGenericMetadataPattern(mangledName, type.ClassName.Module, type, offset);
        }

        TLFunction ConvertInitializer(Node node)
        {
            var variable = node.Children[0];
            var context = ConvertToSwiftType(variable.Children[0], false, null) as SwiftClassType;
            if (context == null)
                return null;
            var privatePublicName = PrivateNamePublicName(variable.Children[1]);
            var name = new SwiftName(privatePublicName.Item2, false);
            var typeIndex = variable.Children[2].Kind == NodeKind.LabelList ? 3 : 2;
            var type = ConvertToSwiftType(variable.Children[typeIndex], false, null);
            var initializer = new SwiftInitializerType(InitializerType.Variable, type, context, name);
            return new TLFunction(mangledName, context.ClassName.Module, name, initializer.Owner, initializer, offset);
        }

        TLLazyCacheVariable ConvertLazyCacheVariable(Node node)
        {
            var classType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            return new TLLazyCacheVariable(mangledName, classType.ClassName.Module, classType, offset);
        }

        TLFunction ConvertProtocolWitnessTable(Node node)
        {
            var witnessTable = ConvertToSwiftType(node, false, null) as SwiftWitnessTableType;

            var rebuiltWitnessTable = new SwiftWitnessTableType(witnessTable.WitnessType, witnessTable.ProtocolType, witnessTable.UncurriedParameter as SwiftClassType);

            return new TLFunction(mangledName, new SwiftName(witnessTable.DiscretionaryString, false), null,
                           witnessTable.UncurriedParameter as SwiftClassType, rebuiltWitnessTable, offset);
        }

        TLThunk ConvertCurryThunk(Node node)
        {
            TLFunction func = ConvertFunction(node.Children[0], isMethodDescriptor: false, isEnumCase: false);
            return new TLThunk(ThunkType.Curry, func.MangledName, func.Module, func.Class, func.Offset);
        }

        TLModuleDescriptor ConvertModuleDescriptor(Node node)
        {
            var firstChild = node.Children[0];
            if (firstChild.Kind != NodeKind.Module)
                return null;
            return new TLModuleDescriptor(mangledName, new SwiftName(firstChild.Text, false), offset);
        }

        TLMetadataDescriptor ConvertMetadataFieldDescriptor(Node node, bool isBuiltIn)
        {
            var ofType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            if (ofType == null)
                return null;
            return new TLMetadataDescriptor(ofType, isBuiltIn, mangledName, ofType.ClassName.Module, offset);
        }

        TLProtocolRequirementsBaseDescriptor ConvertProtocolRequirementsBaseDescriptor(Node node)
        {
            var ofType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            if (ofType == null)
                return null;
            return new TLProtocolRequirementsBaseDescriptor(mangledName, ofType.ClassName.Module, ofType, offset);
        }

        TLBaseConformanceDescriptor ConvertBaseConformanceDescriptor(Node node)
        {
            var protocol = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            if (protocol == null)
                return null;
            var requirement = ConvertToSwiftType(node.Children[1], false, null) as SwiftClassType;
            if (requirement == null)
                return null;
            return new TLBaseConformanceDescriptor(mangledName, protocol.ClassName.Module, protocol, requirement, offset);
        }

        TLAssociatedTypeDescriptor ConvertAssocatedTypeDescriptor(Node node)
        {
            var name = new SwiftName(node.Children[0].Text, false);
            var protocol = ConvertToSwiftType(node.Children[0].Children[0], false, null) as SwiftClassType;
            if (protocol == null)
                return null;
            return new TLAssociatedTypeDescriptor(mangledName, protocol.ClassName.Module, protocol, name, offset);
        }

        TLMetadataBaseOffset ConvertMetadataBaseOffset(Node node)
        {
            var type = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            if (type == null)
                return null;
            return new TLMetadataBaseOffset(mangledName, type.ClassName.Module, type, offset);
        }

        TLMethodLookupFunction ConvertMethodLookupFunction(Node node)
        {
            var type = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            if (type == null)
                return null;
            return new TLMethodLookupFunction(mangledName, type.ClassName.Module, type, offset);
        }


        // ConvertToFunctions are usually called from rules.

        SwiftType ConvertToReferenceType(Node node, bool isReference, SwiftName name)
        {
            return ConvertToSwiftType(node.Children[0].Children[0], true, name);
        }

        SwiftType ConvertToSwiftType(Node node, bool isReference, SwiftName name)
        {
            return ruleRunner.RunRules(node, isReference, name);
        }

        SwiftType ConvertFirstChildToSwiftType(Node node, bool isReference, SwiftName name)
        {
            if (node.Children.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(node));
            return ConvertToSwiftType(node.Children[0], isReference, name);
        }

        SwiftType ConvertToGeneralFunctionType(Node node, bool isReference, SwiftName name, bool isEscaping)
        {
            var throws = node.Children[0].Kind == NodeKind.ThrowsAnnotation;
            var startIndex = throws ? 1 : 0;
            var args = ConvertToSwiftType(node.Children[startIndex + 0], false, null);
            var returnType = ConvertToSwiftType(node.Children[startIndex + 1], false, null);
            return new SwiftFunctionType(args, returnType, isReference, throws, name, isEscaping: isEscaping);
        }

        SwiftType ConvertToFunctionType(Node node, bool isReference, SwiftName name)
        {
            return ConvertToGeneralFunctionType(node, isReference, name, true);
        }

        SwiftType ConvertToNoEscapeFunctionType(Node node, bool isReference, SwiftName name)
        {
            return ConvertToGeneralFunctionType(node, isReference, name, false);
        }

        OperatorType ToOperatorType(NodeKind kind)
        {
            switch (kind)
            {
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

        SwiftType ConvertToAllocatingConstructor(Node node, bool isReference, SwiftName name)
        {
            return ConvertToConstructor(node, isReference, name, true);
        }

        SwiftType ConvertToNonAllocatingConstructor(Node node, bool isReference, SwiftName name)
        {
            return ConvertToConstructor(node, isReference, name, false);
        }

        SwiftType ConvertToConstructor(Node node, bool isReference, SwiftName name, bool isAllocating)
        {
            var isExtension = node.Children[0].Kind == NodeKind.Extension;
            SwiftName module = null;
            SwiftType extensionOn = null;
            SwiftClassType instanceType = null;

            if (isExtension)
            {
                module = new SwiftName(node.Children[0].Children[0].Text, false);
                extensionOn = ConvertToSwiftType(node.Children[0].Children[1], false, null);
                if (extensionOn == null)
                    return null;
                if (extensionOn is SwiftBuiltInType builtIn)
                {
                    extensionOn = new SwiftClassType(SwiftClassName.FromFullyQualifiedName($"Swift.{builtIn.BuiltInType}", OperatorType.None, "V"), false);
                }
                instanceType = extensionOn as SwiftClassType;
            }
            else
            {
                instanceType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
                module = instanceType.ClassName.Module;
            }

            var metadata = new SwiftMetaClassType(instanceType, false, null);
            var labels = node.Children[1].Kind == NodeKind.LabelList ? ToLabelList(node.Children[1]) : null;
            var typeIndex = labels == null ? 1 : 2;
            var functionType = node.Children[typeIndex].Children[0];
            var functionThrows = functionType.Children.Count == 3 && functionType.Children[0].Kind == NodeKind.ThrowsAnnotation ? 1 : 0;
            var args = ConvertToSwiftType(functionType.Children[0 + functionThrows], false, null);
            args = labels != null && labels.Count > 0 ? RenameFunctionParameters(args, labels) : args;
            var ret = ConvertToSwiftType(functionType.Children[1 + functionThrows], false, null);
            var constructor = new SwiftConstructorType(isAllocating, metadata, args, ret, isReference, functionThrows != 0, extensionOn);

            constructor.DiscretionaryString = $"{module.Name}.{instanceType.ClassName.ToFullyQualifiedName(false)}";
            return constructor;
        }

        SwiftType ConvertToDestructor(Node node, bool isReference, SwiftName name)
        {
            return ConvertToDestructor(node, isReference, name, false);
        }

        SwiftType ConvertToDeallocator(Node node, bool isReference, SwiftName name)
        {
            return ConvertToDestructor(node, isReference, name, true);
        }

        SwiftType ConvertToDestructor(Node node, bool isReference, SwiftName name, bool isDeallocating)
        {
            var instanceType = ConvertToSwiftType(node.Children[0], false, null);
            var sct = instanceType as SwiftClassType;
            if (sct == null)
                throw new NotSupportedException($"Expected an SwiftClassType for the instance type in destructor but got {instanceType.GetType().Name}.");
            var destructor = new SwiftDestructorType(isDeallocating, sct, isReference, false);
            destructor.DiscretionaryString = sct.ClassName.ToFullyQualifiedName(true);
            return destructor;
        }
        SwiftType ConvertToStruct(Node node, bool isReference, SwiftName name)
        {
            var className = ToSwiftClassName(node);
            var bit = TryAsBuiltInType(node, className, isReference, name);
            return (SwiftType)bit ?? new SwiftClassType(className, isReference, name);
        }

        SwiftType ConvertToTypeAlias(Node node, bool isReference, SwiftName name)
        {
            if (node.Children[0].Kind == NodeKind.Module && node.Children[0].Text == "__C")
            {
                var shamNode = new Node(NodeKind.Structure);
                shamNode.Children.AddRange(node.Children);
                var className = ToSwiftClassName(shamNode);
                className = RemapTypeAlias(className);
                return new SwiftClassType(className, isReference, name);
            }
            else
            {
                return null;
            }
        }

        SwiftType ConvertToClass(Node node, bool isReference, SwiftName name)
        {
            var className = ToSwiftClassName(node);
            return new SwiftClassType(className, isReference, name);
        }

        SwiftClassName ToSwiftClassName(Node node)
        {
            var memberNesting = new List<MemberNesting>();
            var nestingNames = new List<SwiftName>();
            var moduleName = BuildMemberNesting(node, memberNesting, nestingNames);

            return PatchClassName(moduleName, memberNesting, nestingNames);
        }

        SwiftType ConvertToDependentMember(Node node, bool isReference, SwiftName name)
        {
            // format should be
            // DependentReferenceType
            //   Type
            //      GenericReference (depth, index)
            //   AssocPathItem (name)
            // For multuple path elements, these get nested with the head being deepest.
            // Weird flex, but OK.
            var genericReference = ConvertFirstChildToSwiftType(node, isReference, name) as SwiftGenericArgReferenceType;
            if (genericReference == null)
                return null;
            if (node.Children.Count < 2)
                return genericReference;
            var assocChild = node.Children[1];
            genericReference.AssociatedTypePath.Add(assocChild.Text);
            return genericReference;
        }

        SwiftType ConvertToCFunctionPointerType(Node node, bool isReference, SwiftName name)
        {
            var args = ConvertToSwiftType(node.Children[0], false, null);
            var ret = ConvertToSwiftType(node.Children[1], false, null);
            var function = new SwiftCFunctionPointerType(args, ret, isReference, false, name);
            return function;
        }

        SwiftType ConvertToTuple(Node node, bool isReference, SwiftName name)
        {
            var types = new List<SwiftType>();
            if (node.Children.Count == 0)
            {
                return SwiftTupleType.Empty;
            }
            if (node.Children.Count == 1)
            {
                var onlyChild = ConvertFirstChildToSwiftType(node, false, null);
                return new SwiftTupleType(isReference, name, onlyChild);
            }
            foreach (var child in node.Children)
            {
                var type = ConvertToSwiftType(child, false, null);
                if (type == null)
                    return null;
                types.Add(type);
            }
            return new SwiftTupleType(types, isReference, name);
        }

        SwiftType ConvertToProtocolList(Node node, bool isReference, SwiftName name)
        {
            return ConvertToProtocolList(node, isReference, name, NodeKind.Type);
        }

        SwiftType ConvertToProtocolListAnyObject(Node node, bool isReference, SwiftName name)
        {
            return ConvertToProtocolList(node.Children[0], isReference, name, node.Kind);
        }

        SwiftType ConvertToProtocolList(Node node, bool isReference, SwiftName name, NodeKind nodeKind)
        {
            if (node.Children.Count != 1)
                throw new NotSupportedException("ProtocolList node with more than 1 child not supported");
            if (node.Children[0].Kind != NodeKind.TypeList)
                throw new NotSupportedException($"Given a ProtocolList node with child type {node.Children[0].Kind.ToString()} and {node.Children[0].Children.Count} children, but expected a TypeList with exactly 1 child.");
            // If the number of elements is 0, it means that this is an "Any" type in swift.
            // I'm assuming it's lodged here as a protocol list is that an empty protocol list is
            // represented by an existential container which is also used to represent a protocol list.
            if (node.Children[0].Children.Count == 0)
            {
                var anyName = nodeKind == NodeKind.ProtocolListWithAnyObject ? "Swift.AnyObject" : "Swift.Any";
                var anyProtocolType = nodeKind == NodeKind.ProtocolListWithAnyObject ? 'C' : 'P';
                var className = SwiftClassName.FromFullyQualifiedName(anyName, OperatorType.None, anyProtocolType);
                var classType = new SwiftClassType(className, isReference, name);
                return classType;
            }
            var typeList = ConvertTypeList(node.Children[0]).Select(t => t as SwiftClassType);
            var protoListType = new SwiftProtocolListType(typeList, isReference, name);
            if (protoListType.Protocols.Count == 1)
                return protoListType.Protocols[0].RenamedCloneOf(name);
            return protoListType;
        }

        SwiftType ConvertToProtocolWitnessTable(Node node, bool isReference, SwiftName name)
        {
            var witnessType = node.Kind == NodeKind.ProtocolWitnessTable ?
                          WitnessType.Protocol : WitnessType.ProtocolAccessor;
            var classType = ConvertToSwiftType(node.Children[0].Children[0], false, null) as SwiftClassType;
            var protoType = ConvertToSwiftType(node.Children[0].Children[1], false, null) as SwiftClassType;
            var protoWitness = new SwiftWitnessTableType(witnessType, protoType, classType);
            protoWitness.DiscretionaryString = node.Children[0].Children[2].Text;
            return protoWitness;
        }

        SwiftType ConvertToValueWitnessTable(Node node, bool isReference, SwiftName name)
        {
            var valueType = ConvertToSwiftType(node.Children[0], false, null) as SwiftClassType;
            var valueWitnessTable = new SwiftWitnessTableType(WitnessType.Value, null, valueType);
            valueWitnessTable.DiscretionaryString = valueType.ClassName.Module.Name;
            return valueWitnessTable;
        }

        SwiftType ConvertToEtter(Node node, bool isReference, SwiftName name, PropertyType propertyType)
        {
            var isExtension = node.Children[0].Kind == NodeKind.Extension;
            SwiftName module = null;
            SwiftType extensionOn = null;
            SwiftClassType context = null;
            SwiftClassName className = null;

            if (isExtension)
            {
                module = new SwiftName(node.Children[0].Children[0].Text, false);
                extensionOn = ConvertToSwiftType(node.Children[0].Children[1], false, null);
                if (extensionOn == null)
                    return null;
            }
            else
            {
                var nestings = new List<MemberNesting>();
                var names = new List<SwiftName>();

                module = BuildMemberNesting(node.Children[0], nestings, names);
                if (module == null)
                    return null;
                className = nestings.Count > 0 ? new SwiftClassName(module, nestings, names) : null;
                context = className != null ? new SwiftClassType(className, false) : null;
            }


            var privatePublicName = PrivateNamePublicName(node.Children[1]);
            var propName = new SwiftName(privatePublicName.Item2, false);
            var privateName = privatePublicName.Item1 != null ? new SwiftName(privatePublicName.Item1, false) : null;

            var funcChildIndex = node.Children[2].Kind == NodeKind.LabelList ? 3 : 2;

            var getterType = ConvertToSwiftType(node.Children[funcChildIndex], false, propertyType == PropertyType.Setter ? new SwiftName("newValue", false) : null);
            var prop = new SwiftPropertyType(context, propertyType, propName, privateName,
                getterType, false, isReference, extensionOn);
            prop.DiscretionaryString = context != null ? context.ClassName.ToFullyQualifiedName(true)
                : $"{module.Name}.{propName.Name}";
            return prop;

        }

        SwiftType ConvertToGetter(Node node, bool isReference, SwiftName name)
        {
            // Getter
            //   Variable
            //      Context
            //      Type
            return ConvertToEtter(node.Children[0], isReference, name, PropertyType.Getter);
        }

        SwiftType ConvertToSetter(Node node, bool isReference, SwiftName name)
        {
            return ConvertToEtter(node.Children[0], isReference, name, PropertyType.Setter);
        }

        SwiftType ConvertToWillSet(Node node, bool isReference, SwiftName name)
        {
            return ConvertToEtter(node.Children[0], isReference, name, PropertyType.WillSet);
        }

        SwiftType ConvertToDidSet(Node node, bool isReference, SwiftName name)
        {
            return ConvertToEtter(node.Children[0], isReference, name, PropertyType.DidSet);
        }

        SwiftType ConvertToModifyAccessor(Node node, bool isReference, SwiftName name)
        {
            return ConvertToEtter(node.Children[0], isReference, name, PropertyType.ModifyAccessor);
        }

        SwiftType ConvertToSubscriptEtter(Node node, bool isReference, SwiftName name, PropertyType propertyType)
        {
            var functionNode = new Node(NodeKind.Function);
            functionNode.Children.AddRange(node.Children);
            functionNode.Children.Insert(1, new Node(NodeKind.Identifier, "subscript"));
            var theFunc = ConvertToFunction(functionNode, isReference, name) as SwiftBaseFunctionType;
            if (theFunc == null)
                return null;

            var uncurriedFunctionType = theFunc as SwiftUncurriedFunctionType;

            // if this is normal subscript, uncurriedFunctionType will be non-null
            // if this is an extension, uncurriedFunctionType will be null.

            SwiftFunctionType accessor = null;
            switch (propertyType)
            {
                case PropertyType.Setter:
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
                    var newParameters = theFunc.ParameterCount == 1 ?
                                         new SwiftTupleType(false, null, theFunc.ReturnType,
                                                     theFunc.Parameters.RenamedCloneOf(new SwiftName(theFunc.ReturnType.Name == null ||
                                                                              theFunc.ReturnType.Name.Name != "a" ? "a" : "b", false)))
                                         : new SwiftTupleType(Enumerable.Concat(theFunc.ReturnType.Yield(), theFunc.EachParameter), false, null);
                    accessor = new SwiftFunctionType(newParameters, SwiftTupleType.Empty, false, theFunc.CanThrow, theFunc.Name, theFunc.ExtensionOn);
                    break;
                default:
                    // Because I decided to get clever and reuse existing code to demangle the underlying function,
                    // I get back either a SwiftFunctionType for an extension for a SwiftUncurriedFunctionType for
                    // an instance subscript. These types share a common base class, but are otherwise unrelated.
                    // I need a SwiftFunctionType, so here we are.
                    accessor = new SwiftFunctionType(theFunc.Parameters, theFunc.ReturnType, false, theFunc.IsReference, theFunc.Name, theFunc.ExtensionOn);
                    break;
            }


            var propType = theFunc.ReturnType;
            var propName = theFunc.Name;
            var prop = new SwiftPropertyType(uncurriedFunctionType?.UncurriedParameter, propertyType, propName, null, accessor, false, isReference);
            prop.ExtensionOn = accessor.ExtensionOn;
            prop.DiscretionaryString = theFunc.DiscretionaryString;
            return prop;
        }

        SwiftType ConvertToSubscriptGetter(Node node, bool isReference, SwiftName name)
        {
            return ConvertToSubscriptEtter(node.Children[0], isReference, name, PropertyType.Getter);
        }

        SwiftType ConvertToSubscriptSetter(Node node, bool isReference, SwiftName name)
        {
            return ConvertToSubscriptEtter(node.Children[0], isReference, name, PropertyType.Setter);
        }

        SwiftType ConvertToSubscriptModifier(Node node, bool isReference, SwiftName name)
        {
            return ConvertToSubscriptEtter(node.Children[0], isReference, name, PropertyType.ModifyAccessor);
        }

        SwiftType ConvertToDispatchThunk(Node node, bool isReference, SwiftName name)
        {
            var thunkType = ConvertFirstChildToSwiftType(node, isReference, name);
            if (thunkType == null)
                return null;
            if (thunkType is SwiftBaseFunctionType funcType)
                return funcType.AsThunk();
            return null;
        }

        SwiftType ConvertToTupleElement(Node node, bool isReference, SwiftName name)
        {
            name = node.Children[0].Kind == NodeKind.TupleElementName ? new SwiftName(node.Children[0].Text, false) : name;
            var index = node.Children[0].Kind == NodeKind.TupleElementName ? 1 : 0;
            return ConvertToSwiftType(node.Children[index], isReference, name);
        }

        SwiftType ConvertToVariadicTupleElement(Node node, bool isReference, SwiftName name)
        {
            var type = ConvertToSwiftType(node.Children[1], false, null);
            if (type == null)
                return null;

            // in the past, this came through as an array with the variadic marker set.
            // no longer is the case, so we synthesize the array and set the variadic marker.
            var nesting = new List<MemberNesting>() { MemberNesting.Struct };
            var names = new List<SwiftName>() { new SwiftName("Array", false) };
            var arrayName = new SwiftClassName(new SwiftName("Swift", false), nesting, names);
            var container = new SwiftBoundGenericType(new SwiftClassType(arrayName, false), new List<SwiftType>() { type }, isReference, name);
            container.IsVariadic = true;
            return container;
        }

        SwiftType ConvertToGenericReference(Node node, bool isReference, SwiftName name)
        {
            long depth = node.Children[0].Index;
            long index = node.Children[1].Index;
            return new SwiftGenericArgReferenceType((int)depth, (int)index, isReference, name);
        }

        SwiftType ConvertToBoundGeneric(Node node, bool isReference, SwiftName name)
        {
            var baseType = ConvertToSwiftType(node.Children[0], false, null);
            var boundTypes = ConvertTypeList(node.Children[1]);
            return new SwiftBoundGenericType(baseType, boundTypes, isReference, name);
        }

        List<SwiftType> ConvertTypeList(Node node)
        {
            var typeList = new List<SwiftType>(node.Children.Count);
            foreach (var childNode in node.Children)
            {
                typeList.Add(ConvertToSwiftType(childNode, false, null));
            }
            return typeList;
        }

        SwiftType ConvertToFunction(Node node, bool isReference, SwiftName name)
        {
            var isExtension = node.Children[0].Kind == NodeKind.Extension;
            SwiftType extensionOn = null;
            SwiftName module = null;
            var nesting = new List<MemberNesting>();
            var names = new List<SwiftName>();

            if (isExtension)
            {
                module = new SwiftName(node.Children[0].Children[0].Text, false);
                extensionOn = ConvertToSwiftType(node.Children[0].Children[1], false, null);
            }
            else
            {
                module = BuildMemberNesting(node.Children[0], nesting, names);
            }

            var operatorType = ToOperatorType(node.Children[1].Kind);
            var funcName = operatorType == OperatorType.None ? PrivateNamePublicName(node.Children[1]).Item2 : node.Children[1].Text;
            var labelsList = node.Children[2].Kind == NodeKind.LabelList ? ToLabelList(node.Children[2]) : new List<SwiftName>();
            var typeIndex = node.Children[2].Kind == NodeKind.LabelList ? 3 : 2;
            var functionType = ConvertToSwiftType(node.Children[typeIndex], isReference, new SwiftName(funcName, false)) as SwiftFunctionType;
            if (functionType == null)
                return null;

            var args = labelsList.Count > 0 ? RenameFunctionParameters(functionType.Parameters, labelsList) : functionType.Parameters;
            if (nesting.Count > 0)
            {
                var classType = new SwiftClassType(new SwiftClassName(module, nesting, names), false);
                var methodName = classType.ClassName.ToFullyQualifiedName(true) +
                              (operatorType != OperatorType.None ? $".|{operatorType.ToString()}" : "") +
                              "." + funcName;
                var uncurried = new SwiftUncurriedFunctionType(classType, args, functionType.ReturnType,
                    functionType.IsReference, functionType.CanThrow, new SwiftName(funcName, false));
                uncurried.DiscretionaryString = methodName;
                uncurried.GenericArguments.AddRange(functionType.GenericArguments);
                return uncurried;
            }
            var functionName = module +
                           (operatorType != OperatorType.None ? $".|{operatorType.ToString()}" : "") +
                           "." + funcName;
            var generics = functionType.GenericArguments;
            functionType = new SwiftFunctionType(args, functionType.ReturnType, functionType.IsReference, functionType.CanThrow, new SwiftName(funcName, false), extensionOn);
            functionType.GenericArguments.AddRange(generics);
            functionType.DiscretionaryString = functionName;
            return functionType;
        }

        SwiftType ConvertToStatic(Node node, bool isReference, SwiftName name)
        {
            var functionType = ConvertToSwiftType(node.Children[0], isReference, name);
            if (functionType == null)
                return null;
            var propType = functionType as SwiftPropertyType;
            if (propType != null)
            {
                return propType.RecastAsStatic();
            }
            var uncurriedFunction = functionType as SwiftUncurriedFunctionType;
            if (uncurriedFunction != null)
            {
                var staticFunction = new SwiftStaticFunctionType(uncurriedFunction.Parameters, uncurriedFunction.ReturnType,
                                          uncurriedFunction.IsReference, uncurriedFunction.CanThrow,
                                          uncurriedFunction.UncurriedParameter as SwiftClassType, uncurriedFunction.Name);
                staticFunction.DiscretionaryString = uncurriedFunction.DiscretionaryString;
                return staticFunction;
            }
            var baseFunctionType = functionType as SwiftBaseFunctionType;
            if (baseFunctionType != null)
            {
                var staticFunction = new SwiftStaticFunctionType(baseFunctionType.Parameters, baseFunctionType.ReturnType,
                                          baseFunctionType.IsReference, baseFunctionType.CanThrow,
                                          null, baseFunctionType.Name);
                staticFunction.DiscretionaryString = baseFunctionType.DiscretionaryString;
                staticFunction.ExtensionOn = baseFunctionType.ExtensionOn;
                return staticFunction;
            }
            var initializerType = functionType as SwiftInitializerType;
            if (initializerType != null)
            {
                return initializerType; // this doesn't need a static recast?
            }
            throw new ArgumentOutOfRangeException($"Expected a SwiftUncurriedFunctionType, a SwiftPropertyType, a SwiftBaseFunctionType or a SwiftInitializerType in a static node, but got {functionType.GetType().Name}");
        }

        List<SwiftName> ToLabelList(Node node)
        {
            var result = new List<SwiftName>();
            foreach (var child in node.Children)
            {
                if (child.Kind == NodeKind.Identifier)
                {
                    result.Add(new SwiftName(child.Text, false));
                }
                else
                {
                    result.Add(new SwiftName("_", false));
                }
            }
            return result;
        }

        SwiftType ConvertToExistentialMetatype(Node node, bool isReference, SwiftName name)
        {
            var child = ConvertFirstChildToSwiftType(node, false, name);
            var childType = child as SwiftProtocolListType;
            if (child is SwiftClassType classType)
            {
                childType = new SwiftProtocolListType(classType, classType.IsReference, classType.Name);
            }
            if (childType == null)
                return null;
            return new SwiftExistentialMetaType(childType, isReference, null);
        }

        SwiftType ConvertToMetatype(Node node, bool isReference, SwiftName name)
        {
            var child = ConvertFirstChildToSwiftType(node, false, name);
            if (child is SwiftClassType cl)
            {
                return new SwiftMetaClassType(cl, isReference, name);
            }
            else if (child is SwiftGenericArgReferenceType classReference)
            {
                return new SwiftMetaClassType(classReference, isReference, name);
            }
            else
            {
                return null;
            }
        }

        SwiftType ConvertToGenericFunction(Node node, bool isReference, SwiftName name)
        {
            List<GenericArgument> args = GetGenericArguments(node);
            var theFunction = ConvertToSwiftType(node.Children[1], isReference, name) as SwiftBaseFunctionType;
            theFunction.GenericArguments.AddRange(args);
            return theFunction;
        }

        List<GenericArgument> GetGenericArguments(Node node)
        {
            var paramCountNode = node.Children[0].Children[0];
            if (paramCountNode.Kind != NodeKind.DependentGenericParamCount)
                throw new NotSupportedException($"Expected a DependentGenericParamCount node but got a {paramCountNode.Kind.ToString()}");

            var paramCount = (int)paramCountNode.Index;
            List<GenericArgument> args = new List<GenericArgument>(paramCount);
            for (int i = 0; i < paramCount; i++)
            {
                args.Add(new GenericArgument(0, i));
            }
            if (node.Children[0].Children.Count > 1)
            {
                var dependentGenericSignature = node.Children[0];
                // the 0th child is the number of generic parameters (see above)
                for (int i = 1; i < dependentGenericSignature.Children.Count; i++)
                {
                    var genericParamReference = ConvertToSwiftType(dependentGenericSignature.Children[i].Children[0], false, null) as SwiftGenericArgReferenceType;
                    var genericConstraintType = ConvertToSwiftType(dependentGenericSignature.Children[i].Children[1], false, null) as SwiftClassType;
                    MarkGenericConstraint(args, genericParamReference, genericConstraintType);
                }
            }
            return args;
        }

        static void MarkGenericConstraint(List<GenericArgument> args, SwiftGenericArgReferenceType paramReference, SwiftClassType constraintType)
        {
            foreach (var genArg in args)
            {
                if (genArg.Depth == paramReference.Depth && genArg.Index == paramReference.Index)
                {
                    genArg.Constraints.Add(constraintType);
                    return;
                }
            }
        }

        static SwiftBuiltInType TryAsBuiltInType(Node node, SwiftClassName className, bool isReference, SwiftName name)
        {
            switch (className.ToFullyQualifiedName())
            {
                case "Swift.Int":
                    return new SwiftBuiltInType(CoreBuiltInType.Int, isReference, name);
                case "Swift.Float":
                    return new SwiftBuiltInType(CoreBuiltInType.Float, isReference, name);
                case "Swift.Bool":
                    return new SwiftBuiltInType(CoreBuiltInType.Bool, isReference, name);
                case "Swift.UInt":
                    return new SwiftBuiltInType(CoreBuiltInType.UInt, isReference, name);
                case "Swift.Double":
                    return new SwiftBuiltInType(CoreBuiltInType.Double, isReference, name);
                default:
                    return null;
            }
        }

        static SwiftName BuildMemberNesting(Node node, List<MemberNesting> nestings, List<SwiftName> names)
        {
            if (node.Children.Count > 0 && node.Children[0].Kind == NodeKind.Extension)
                node = node.Children[0].Children[1];
            var nesting = MemberNesting.Class;
            switch (node.Kind)
            {
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
                case NodeKind.Module:
                    return new SwiftName(node.Text, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(node), $"Expected a nominal type node kind but got {node.Kind.ToString()}");
            }

            var privatePublicName = PrivateNamePublicName(node.Children[1]);
            var className = new SwiftName(privatePublicName.Item2, false);
            SwiftName moduleName = null;
            if (node.Children[0].Kind == NodeKind.Identifier || node.Children[0].Kind == NodeKind.Module)
            {
                moduleName = new SwiftName(node.Children[0].Text, false);
            }
            else
            {
                // recurse before adding names.
                moduleName = BuildMemberNesting(node.Children[0], nestings, names);
            }
            names.Add(className);
            nestings.Add(nesting);
            return moduleName;
        }


        static Tuple<string, string> PrivateNamePublicName(Node node)
        {
            if (node.Kind == NodeKind.Identifier)
                return new Tuple<string, string>(null, node.Text);
            if (node.Kind == NodeKind.PrivateDeclName)
                return new Tuple<string, string>(node.Children[1].Text, node.Children[0].Text);
            throw new ArgumentOutOfRangeException(nameof(node));
        }

        static SwiftType RenameFunctionParameters(SwiftType parameters, List<SwiftName> labels)
        {
            if (labels.Count < 1)
                throw new ArgumentOutOfRangeException(nameof(parameters));

            var oldTuple = parameters as SwiftTupleType;
            if (oldTuple == null)
                throw new NotSupportedException($"{parameters} is not a tuple, it's a {parameters.GetType().Name}");

            var newTuple = new SwiftTupleType(oldTuple.Contents.Select((elem, index) => RenamedCloneOf(elem, labels[index])),
                                    oldTuple.IsReference, oldTuple.Name);
            return newTuple;
        }

        static SwiftType RenamedCloneOf(SwiftType st, SwiftName newName)
        {
            return st.RenamedCloneOf(newName.Name == "_" ? new SwiftName("", false) : newName);
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

        struct ModuleOrType
        {
            public ModuleOrType(string replacementModule)
            {
                ReplacementModule = replacementModule;
                ReplacementFullClassName = null;
                ReplacementNesting = null;
            }

            public ModuleOrType(string replacementFullClassName, string replacementNesting)
            {
                ReplacementFullClassName = replacementFullClassName;
                ReplacementNesting = replacementNesting;
                ReplacementModule = null;
            }

            public string ReplacementModule;
            public string ReplacementFullClassName;
            public string ReplacementNesting;
        }


        static Dictionary<string, ModuleOrType> classNameOntoModuleOrType = new Dictionary<string, ModuleOrType> {
            { "AVError", new ModuleOrType ("AVFoundation") },
            { "AudioBuffer", new ModuleOrType ("AudioToolbox") },
            { "AudioBufferList", new ModuleOrType ("AudioToolbox") },
            { "CATransform3D", new ModuleOrType ("CoreAnimation") },
            { "CGAffineTransform", new ModuleOrType ("CoreGraphics") },
            { "CGColorSapceModel", new ModuleOrType ("CoreGraphics") },
            { "CGPoint", new ModuleOrType ("CoreGraphics") },
            { "CGRect", new ModuleOrType ("CoreGraphics") },
            { "CGSize", new ModuleOrType ("CoreGraphics") },
            { "CGVector", new ModuleOrType ("CoreGraphics") },
            { "CLError", new ModuleOrType ("CoreLocation") },
            { "CMTime", new ModuleOrType ("CoreMedia") },
            { "CMTimeFlags", new ModuleOrType ("CoreMedia") },
            { "CMTimeMapping", new ModuleOrType ("CoreMedia") },
            { "CMTimeRange", new ModuleOrType ("CoreMedia") },
            { "NSComparisonResult", new ModuleOrType ("Foundation") },
            { "NSDecimal", new ModuleOrType ("Foundation") },
            { "NSEnumerationOptions", new ModuleOrType ("Foundation") },
            { "NSKeyValueChange", new ModuleOrType ("Foundation") },
            { "NSKeyValueObservingOptions", new ModuleOrType ("Foundation") },
            { "NSFastEnumerationState", new ModuleOrType ("Foundation") },
            { "NSKeyValueChangeKey", new ModuleOrType ("Foundation") },
            { "NSBundle", new ModuleOrType ("Foundation.Bundle", "C") },
            { "NSURL", new ModuleOrType ("Foundation") },
            { "StringTransform", new ModuleOrType ("Foundation") },
            { "URLFileResourceType", new ModuleOrType ("Foundation") },
            { "URLResourceKey", new ModuleOrType ("Foundation") },
            { "URLThumbnailDictionaryItem", new ModuleOrType ("Foundation") },
            { "URLUbiquitousItemDownloadingStatus", new ModuleOrType ("Foundation") },
            { "URLUbiquitousSharedItemPermissions", new ModuleOrType ("Foundation") },
            { "URLUbiquitousSharedItemRole", new ModuleOrType ("Foundation") },
            { "MKCoordinateSpan", new ModuleOrType ("MapKit") },
            { "NSAnimationEffect", new ModuleOrType ("AppKit") },
            { "SCNGeometryPrimitiveType", new ModuleOrType ("SceneKit") },
            { "SCNVector3", new ModuleOrType ("SceneKit") },
            { "SCNVector4", new ModuleOrType ("SceneKit") },
            { "UIContentSizeCategory", new ModuleOrType ("UIKit") },
            { "UIControlState", new ModuleOrType ("UIKit.UIControl.State", "CV") },
            { "UIDeviceOrientation", new ModuleOrType ("UIKit") },
            { "UIEdgeInsets", new ModuleOrType ("UIKit") },
            { "UIInterfaceOrientation", new ModuleOrType ("UIKit") },
            { "UIControlEvents", new ModuleOrType ("UIKit.UIControl.Event", "CV") },
            { "UIViewAnimationOptions", new ModuleOrType ("UIKit.UIView.AnimationOptions", "CV") },
            { "UIOffset", new ModuleOrType ("UIKit") },
            { "UIBlurEffectStyle", new ModuleOrType ("UIKit.UIBlurEffect.Style","CV") },
            { "UIColor", new ModuleOrType ("UIKit")},
            { "UIImage", new ModuleOrType ("UIImage") },
            { "UITableViewStyle", new ModuleOrType ("UIKit.UITableView.Style", "CV") },
            { "UIView", new ModuleOrType ("UIKit") },
            { "UIViewControllerConditioning", new ModuleOrType ("UIKit") },
            { "UIVisualEffectView", new ModuleOrType ("UIKit") },
            { "CKError", new ModuleOrType ("CloudKit") },
            { "CNError", new ModuleOrType ("Contacts") },
            { "MTLSamplePosition", new ModuleOrType ("Metal") },
            { "XCUIKeyboardKey", new ModuleOrType ("XCTest") },
            { "BNNSActivationFunction", new ModuleOrType ("Accelerate") },
            { "BNNSDataType", new ModuleOrType ("Accelerate") },
            { "simd_double2x2", new ModuleOrType ("Accelerate") },
            { "simd_double2x3", new ModuleOrType ("Accelerate") },
            { "simd_double2x4", new ModuleOrType ("Accelerate") },
            { "simd_double3x2", new ModuleOrType ("Accelerate") },
            { "simd_double3x3", new ModuleOrType ("Accelerate") },
            { "simd_double3x4", new ModuleOrType ("Accelerate") },
            { "simd_double4x2", new ModuleOrType ("Accelerate") },
            { "simd_double4x3", new ModuleOrType ("Accelerate") },
            { "simd_double4x4", new ModuleOrType ("Accelerate") },
            { "simd_float2x2", new ModuleOrType ("Accelerate") },
            { "simd_float2x3", new ModuleOrType ("Accelerate") },
            { "simd_float2x4", new ModuleOrType ("Accelerate") },
            { "simd_float3x2", new ModuleOrType ("Accelerate") },
            { "simd_float3x3", new ModuleOrType ("Accelerate") },
            { "simd_float3x4", new ModuleOrType ("Accelerate") },
            { "simd_float4x2", new ModuleOrType ("Accelerate") },
            { "simd_float4x3", new ModuleOrType ("Accelerate") },
            { "simd_float4x4", new ModuleOrType ("Accelerate") },
            { "simd_quatd", new ModuleOrType ("Accelerate") },
            { "simd_quatf", new ModuleOrType ("Accelerate") },
        };

        static SwiftClassName PatchClassName(SwiftName moduleName, List<MemberNesting> nesting, List<SwiftName> nestingNames)
        {
            // surprise!
            // When we run XML reflection, the module name we get is ObjectiveC, but in the name mangled version
            // it's __ObjC. This is the only place in this code where we make a module name, so it's a decent enough
            // bottleneck to alias it.
            if (moduleName.Name == "__ObjC")
                moduleName = new SwiftName("Foundation", false);
            if (moduleName.Name != "__C" || nestingNames.Count != 1)
                return new SwiftClassName(moduleName, nesting, nestingNames);
            if (classNameOntoModuleOrType.ContainsKey(nestingNames[0].Name))
            {
                var moduleOrType = classNameOntoModuleOrType[nestingNames[0].Name];
                if (moduleOrType.ReplacementModule == null)
                {
                    return SwiftClassName.FromFullyQualifiedName(moduleOrType.ReplacementFullClassName, OperatorType.None, moduleOrType.ReplacementNesting);
                }
                else
                {
                    moduleName = new SwiftName(moduleOrType.ReplacementModule, false);
                }
            }
            return new SwiftClassName(moduleName, nesting, nestingNames);
        }

        static Dictionary<string, SwiftClassName> aliasNameOntoClassName = new Dictionary<string, SwiftClassName> {
            { "__C.NSOperatingSystemVersion", SwiftClassName.FromFullyQualifiedName ("Foundation.OperatingSystemVersion", OperatorType.None, 'V') },
        };

        static SwiftClassName RemapTypeAlias(SwiftClassName className)
        {
            SwiftClassName newName = null;
            return aliasNameOntoClassName.TryGetValue(className.ToFullyQualifiedName(true), out newName) ? newName : className;
        }
    }
}
