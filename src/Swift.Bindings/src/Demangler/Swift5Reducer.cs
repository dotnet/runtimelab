// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace BindingsGeneration.Demangling;

/// <summary>
/// Swift5Reducer takes a tree of Node and attempts to reduce it completely to an easier to
/// to manipulate data structure. It does this by matching patterns in the tree and on match
/// applying a reduction function on the matched Node.
/// </summary>
internal class Swift5Reducer
{
    static RuleRunner ruleRunner = new RuleRunner(BuildMatchRules());

    /// <summary>
    /// Build a set of match rules for Node reduction
    /// </summary>
    static List<MatchRule> BuildMatchRules() =>
    new List<MatchRule>() {
        new MatchRule() {
            Name = "Global", NodeKind = NodeKind.Global, Reducer = ConvertFirstChild
        },
        new MatchRule() {
            Name = "ProtocolConformance", NodeKindList = new List<NodeKind>() { NodeKind.ProtocolWitnessTable, NodeKind.ProtocolConformanceDescriptor, } , Reducer = ConvertProtocolConformance
        },
        new MatchRule() {
            Name = "Type", NodeKind = NodeKind.Type, Reducer = ConvertFirstChild
        },
        new MatchRule() {
            Name = "Nominal", NodeKindList = new List<NodeKind>() { NodeKind.Class, NodeKind.Structure, NodeKind.Protocol, NodeKind.Enum },
            Reducer = ConvertNominal
        },
        new MatchRule() {
            Name = "Module", NodeKind = NodeKind.Module, Reducer = (n, s) => ProvenanceReduction.TopLevel (s, n.Text)
        },
        new MatchRule() {
            Name = "Tuple", NodeKind = NodeKind.Tuple, Reducer = ConvertTuple
        },
        new MatchRule() {
            Name = "Static", NodeKind = NodeKind.Static, Reducer = ConvertFirstChild
        },
        new MatchRule() {
            Name = "TupleElement", NodeKind = NodeKind.TupleElement, Reducer = ConvertTupleElement,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "TupleElementType", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() { // named tuple element has a tuple element name node as the first child, otherwise it's a tuple element
            Name = "NamedTupleElement", NodeKind = NodeKind.TupleElement, Reducer = ConvertTupleElement,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "TupleElementName", NodeKind = NodeKind.TupleElementName, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "TupleElementType", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "VariadicTupleElement", NodeKind = NodeKind.TupleElement, Reducer = ConvertVariadicTupleElement,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "VariadicTupleElementMarker", NodeKind = NodeKind.VariadicMarker, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "VariadicTupleElementType", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "FunctionType", NodeKindList = new List<NodeKind> () { NodeKind.FunctionType, NodeKind.NoEscapeFunctionType },
            Reducer = ConvertFunctionType,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "Arguments", NodeKind = NodeKind.ArgumentTuple, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "ReturnType", NodeKind = NodeKind.ReturnType, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "FunctionType", NodeKindList = new List<NodeKind> () { NodeKind.FunctionType, NodeKind.NoEscapeFunctionType },
            Reducer = ConvertFunctionTypeThrows,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "ThrowsAnnotation", NodeKind = NodeKind.ThrowsAnnotation, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "Arguments", NodeKind = NodeKind.ArgumentTuple, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "ReturnType", NodeKind = NodeKind.ReturnType, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "FunctionType", NodeKindList = new List<NodeKind> () { NodeKind.FunctionType, NodeKind.NoEscapeFunctionType },
            Reducer = ConvertFunctionTypeAsync,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "AsyncAnnotation", NodeKind = NodeKind.AsyncAnnotation, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "Arguments", NodeKind = NodeKind.ArgumentTuple, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "ReturnType", NodeKind = NodeKind.ReturnType, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "FunctionType", NodeKindList = new List<NodeKind> () { NodeKind.FunctionType, NodeKind.NoEscapeFunctionType },
            Reducer = ConvertFunctionTypeAsyncThrows,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "ThrowsAnnotation", NodeKind = NodeKind.ThrowsAnnotation, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "AsyncAnnotation", NodeKind = NodeKind.AsyncAnnotation, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "Arguments", NodeKind = NodeKind.ArgumentTuple, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "ReturnType", NodeKind = NodeKind.ReturnType, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "Function", NodeKind = NodeKind.Function, Reducer = ConvertFunction,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "Provenance", NodeKindList = new List<NodeKind> () {
                        NodeKind.Module, NodeKind.Class, NodeKind.Structure, NodeKind.Protocol, NodeKind.Enum },
                        Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "Identifier", NodeKind = NodeKind.Identifier, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "LabelList", NodeKind = NodeKind.LabelList, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "Type", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "Function", NodeKind = NodeKind.Function, Reducer = ConvertFunction,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "Provenance", NodeKindList = new List<NodeKind> () {
                        NodeKind.Module, NodeKind.Class, NodeKind.Structure, NodeKind.Protocol, NodeKind.Enum },
                        Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "Identifier", NodeKind = NodeKind.Identifier, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "Type", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "Allocator", NodeKind = NodeKind.Allocator, Reducer = ConvertAllocator,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "AllocatorProvenance", NodeKindList = new List<NodeKind> () {
                        NodeKind.Module, NodeKind.Class, NodeKind.Structure, NodeKind.Protocol, NodeKind.Enum },
                        Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "AllocatorType", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                },
            }
        },
        new MatchRule() {
            Name = "DispatchThunkFunction", NodeKind = NodeKind.DispatchThunk, Reducer = ConvertDispatchThunkFunction,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "Function", NodeKindList = new List<NodeKind> () { NodeKind.Function, NodeKind.Allocator },
                    Reducer = MatchRule.ErrorReducer
                }
            }
        },
        new MatchRule() {
            Name = "TypeMetadataAccessFunction", NodeKind = NodeKind.TypeMetadataAccessFunction, Reducer = ConvertMetadataAcessor,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "Type", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                }
            }
        },
        new MatchRule() {
            Name = "BoundGenericNominal", NodeKindList = new List<NodeKind> () { NodeKind.BoundGenericEnum, NodeKind.BoundGenericClass, NodeKind.BoundGenericStructure },
            Reducer = ConvertBoundGenericNominal,
            ChildRules = new List<MatchRule> () {
                new MatchRule () {
                    Name = "BoundGenericNominalType", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule () {
                    Name = "BoundGenericNominalTypeList", NodeKind = NodeKind.TypeList, Reducer = MatchRule.ErrorReducer
                }
            }
        },
        new MatchRule() {
            Name = "DependentGenericParameter", NodeKind = NodeKind.DependentGenericParamType,
            Reducer = ConvertDependentGenericParameter
        },
        new MatchRule() {
            Name = "DependentMemberType", NodeKind = NodeKind.DependentMemberType, Reducer = ConvertDependentMember,
            ChildRules = new List<MatchRule> () {
                new MatchRule {
                    Name = "DependentMemberSubType", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                }
            }
        },
        new MatchRule() {
            Name = "DependentGenericType", NodeKind = NodeKind.DependentGenericType, Reducer = ConvertDependentGenericType,
            ChildRules = new List<MatchRule> () {
                new MatchRule {
                    Name = "DependentGenericSignature", NodeKind = NodeKind.DependentGenericSignature, Reducer = MatchRule.ErrorReducer
                },
                new MatchRule {
                    Name = "EmbeddedType", NodeKind = NodeKind.Type, Reducer = MatchRule.ErrorReducer
                }
            }
        },
    };

    /// <summary>
    /// Convert a Node into an IReduction. On failure, the IReduction will be type ReductionError
    /// </summary>
    public static IReduction Convert(Node node, string mangledName)
    {
        return ruleRunner.RunRules(node, mangledName);
    }

    /// <summary>
    /// Given a ProtocolWitnessTable node, convert to a ProtocolWitnessTable reduction
    /// </summary>
    static IReduction ConvertProtocolConformance(Node node, string mangledName)
    {
        // What to expect here:
        // ProtocolConformance
        //     Type
        //     Type
        //     Module - ONLY FOR ProtocolConformanceDescriptor

        var isDescriptor = node.Kind == NodeKind.ProtocolConformanceDescriptor;

        var child = node.Children[0];
        if (child.Kind != NodeKind.ProtocolConformance)
        {
            return ReductionErrorHigh(ExpectedButGot("ProtocolConformance", node.Kind.ToString(), mangledName), mangledName);
        }
        var grandchild = Convert(child.Children[0], mangledName);
        if (grandchild is ReductionError error)
        {
            error.Severity = ReductionErrorSeverity.High;
            return error;
        }
        var implementingType = grandchild as TypeSpecReduction;
        if (implementingType is null)
        {
            return ReductionErrorHigh(ExpectedButGot("Nominal type implementing protocol", grandchild.GetType().Name, mangledName), mangledName);
        }

        grandchild = Convert(child.Children[1], mangledName);
        if (grandchild is ReductionError error1)
        {
            error1.Severity = ReductionErrorSeverity.High;
            return error1;
        }
        var protocolType = grandchild as TypeSpecReduction;
        if (protocolType is null)
        {
            return ReductionErrorHigh(ExpectedButGot("Nominal type protocol", grandchild.GetType().Name, mangledName), mangledName);
        }

        var impNamed = (NamedTypeSpec)implementingType.TypeSpec;
        var protoNamed = (NamedTypeSpec)protocolType.TypeSpec;

        if (isDescriptor)
        {
            grandchild = Convert(child.Children[2], mangledName);
            if (grandchild is ProvenanceReduction prov)
            {
                if (!prov.Provenance.IsTopLevel)
                    return ReductionErrorHigh(ExpectedButGot("A top-level module name", prov.Provenance.ToString(), mangledName), mangledName);
                return new ProtocolConformanceDescriptorReduction() { Symbol = mangledName, ImplementingType = impNamed, ProtocolType = protoNamed, Module = prov.Provenance.Module };
            }
            else if (grandchild is ReductionError error2)
            {
                error2.Severity = ReductionErrorSeverity.High;
                return error2;
            }
            else
            {
                return ReductionErrorHigh(ExpectedButGot("ProvenanceReduction", grandchild.GetType().Name, mangledName), mangledName);
            }

        }
        else
        {
            return new ProtocolWitnessTableReduction() { Symbol = mangledName, ImplementingType = impNamed, ProtocolType = protoNamed };
        }
    }

    /// <summary>
    /// Convert a nominal node into TypeSpecReduction
    /// </summary>
    /// <param name="node">One of the various nominal nodes</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction on success, an error otherwise</returns>
    static IReduction ConvertNominal(Node node, string mangledName)
    {
        // What to expect here:
        // Nominal (Class/Structure/Protocol/Enum)
        //    Module Name
        //    Identifier Name
        // -- or --
        // depth-first nesting of inner types such that the deepest nesting is the module and type
        // Nominal
        //   Nominal
        //     Nominal
        //       ...
        //       Nominal
        //         Module
        //         Identifier
        //     Identifier
        //   Identifier
        // Identifier

        var sb = new StringBuilder();
        try
        {
            GetNestedNominalName(node, sb, mangledName);
        }
        catch (Exception err)
        {
            return ReductionErrorLow(err.Message, mangledName);
        }
        return new TypeSpecReduction() { Symbol = mangledName, TypeSpec = new NamedTypeSpec(sb.ToString()) };
    }

    /// <summary>
    /// Converts a node of type Tuple into a TypeSpecReduction.
    /// </summary>
    /// <param name="node">The node for a Tuple</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>a TypeSpecReduction</returns>
    ///
    static IReduction ConvertTuple(Node node, string mangledName)
    {
        // What to expect here:
        // Tuple
        //   TupleElement*   - 0 or more TupleElement nodes

        // No children, empty tuple
        if (node.Children.Count == 0)
            return new TypeSpecReduction() { Symbol = mangledName, TypeSpec = TupleTypeSpec.Empty };

        var types = new List<TypeSpec>();

        foreach (var child in node.Children)
        {
            var reduction = Convert(child, mangledName);
            if (reduction is ReductionError error)
                return error;
            else if (reduction is TypeSpecReduction typeSpecReduction)
            {
                // Every child should be a Type node which turns into a TypeSpecReduction
                types.Add(typeSpecReduction.TypeSpec);
            }
            else
            {
                return ReductionErrorLow(ExpectedButGot("TypeSpecReduction in tuple", reduction.GetType().Name, mangledName), mangledName);
            }
        }

        return new TypeSpecReduction() { Symbol = mangledName, TypeSpec = new TupleTypeSpec(types) };
    }

    /// <summary>
    /// Converts a node of type TupleElement into a TypeSpecReduction
    /// </summary>
    /// <param name="node">The node for a TupleElement</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>a TypeSpecReduction</returns>
    static IReduction ConvertTupleElement(Node node, string mangledName)
    {
        // What to expect here:
        // TupleElement
        //    TupleElementName
        //    Type
        // --- OR ---
        // TupleElement
        //    Type
        var isNamedElement = node.Children[0].Kind == NodeKind.TupleElementName;
        var label = isNamedElement ? node.Children[0].Text : null;
        var index = isNamedElement ? 1 : 0;
        var reduction = Convert(node.Children[index], mangledName);
        if (reduction is ReductionError error)
            return error;
        else if (reduction is TypeSpecReduction typeSpecReduction)
        {
            typeSpecReduction.TypeSpec.TypeLabel = label;
            return typeSpecReduction;
        }
        else
        {
            return ReductionErrorLow(ExpectedButGot("TypeSpecReduction in tuple element", reduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// Converts a TupleElement with a VariadicMarker into a TypeSpecReduction of type Swift.Array<Type>
    /// </summary>
    /// <param name="node">A TupleElement node with a VariadicMarker</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction with a Swift.Array<T></returns>
    static IReduction ConvertVariadicTupleElement(Node node, string mangledName)
    {
        // What to expect here:
        // TupleElement
        //    VariadicMarker
        //    Type

        // will turn this into a named type spec Swift.Array<Type>
        var reduction = Convert(node.Children[1], mangledName);
        if (reduction is ReductionError error)
            return error;
        else if (reduction is TypeSpecReduction typeSpecReduction)
        {
            var newSpec = new NamedTypeSpec("Swift.Array");
            newSpec.GenericParameters.Add(typeSpecReduction.TypeSpec);
            typeSpecReduction.TypeSpec.IsVariadic = true;
            return new TypeSpecReduction() { Symbol = typeSpecReduction.Symbol, TypeSpec = newSpec };
        }
        else
        {
            return ReductionErrorLow(ExpectedButGot("TypeSpecReduction in variadic tuple element", reduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// Convert a FunctionType node into a TypeSpecReduction
    /// </summary>
    /// <param name="node">a FunctionType node</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction</returns>
    static IReduction ConvertFunctionType(Node node, string mangledName)
    {
        // What to expect here:
        // FunctionType
        //    ArgumentTuple
        //        Type
        //    ReturnType
        //        Type

        var noEscaping = node.Kind == NodeKind.NoEscapeFunctionType;
        var argTuple = node.Children[0];
        var @return = node.Children[1];

        return ConvertFunctionAsyncThrows(argTuple, @return, false, false, noEscaping, mangledName);

        // var reduction = ConvertFirstChild (argTuple, mangledName);
        // if (reduction is ReductionError error)
        //     return error;
        // else if (reduction is TypeSpecReduction argsTypeSpecReduction) {
        //     reduction = ConvertFirstChild (@return, mangledName);
        //     if (reduction is ReductionError returnError)
        //         return returnError;
        //     else if (reduction is TypeSpecReduction returnTypeSpecReduction) {
        //         var closure = new ClosureTypeSpec (argsTypeSpecReduction.TypeSpec, returnTypeSpecReduction.TypeSpec);
        //         return new TypeSpecReduction () { Symbol = argsTypeSpecReduction.Symbol, TypeSpec = closure };
        //     } else {
        //         return ReductionErrorLow (ExpectedButGot ("TypeSpecReduction in function return type", reduction.GetType ().Name, mangledName), mangledName);
        //     }
        // } else {
        //     return ReductionErrorLow (ExpectedButGot ("TypeSpecReduction in argument tuple type", reduction.GetType ().Name, mangledName), mangledName);
        // }
    }

    /// <summary>
    /// Convert a FunctionType node that can throw into a TypeSpecReduction
    /// </summary>
    /// <param name="node">a FunctionType node</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction</returns>
    static IReduction ConvertFunctionTypeThrows(Node node, string mangledName)
    {
        // Expect:
        // ThrowsAnnotation
        // ArgumentTuple
        // ReturnType
        var noEscaping = node.Kind == NodeKind.NoEscapeFunctionType;
        return ConvertFunctionAsyncThrows(node.Children[1], node.Children[2], false, true, noEscaping, mangledName);
    }

    /// <summary>
    /// Convert a FunctionType node that is async into a TypeSpecReduction
    /// </summary>
    /// <param name="node">a FunctionType node</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction</returns>
    static IReduction ConvertFunctionTypeAsync(Node node, string mangledName)
    {
        // Expect:
        // AsyncAnnotation
        // ArgumentTuple
        // ReturnType
        var noEscaping = node.Kind == NodeKind.NoEscapeFunctionType;
        return ConvertFunctionAsyncThrows(node.Children[1], node.Children[2], true, false, noEscaping, mangledName);
    }

    static IReduction ConvertFunctionTypeAsyncThrows(Node node, string mangledName)
    {
        // Expect:
        // AsyncAnnotation
        // ThrowsAnnotation
        // ArgumentTuple
        // ReturnType
        var noEscaping = node.Kind == NodeKind.NoEscapeFunctionType;
        return ConvertFunctionAsyncThrows(node.Children[2], node.Children[3], true, true, noEscaping, mangledName);
    }

    /// <summary>
    /// Converts an argument tuple and return type to a TypeSpecReduction
    /// </summary>
    /// <param name="argTuple">The function arguments Node</param>
    /// <param name="return">The return type Node</param>
    /// <param name="async">Whether or not the function is async</param>
    /// <param name="throws">Whether or not the function can throw</param>
    /// <param name="noEscaping">Whether or not the function can't escape</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction containing a ClosureTypeSpec</returns>
    static IReduction ConvertFunctionAsyncThrows(Node argTuple, Node @return, bool async, bool throws, bool noEscaping, string mangledName)
    {
        var reduction = ConvertFirstChild(argTuple, mangledName);
        if (reduction is ReductionError error)
            return error;
        else if (reduction is TypeSpecReduction argsTypeSpecReduction)
        {
            reduction = ConvertFirstChild(@return, mangledName);
            if (reduction is ReductionError returnError)
                return returnError;
            else if (reduction is TypeSpecReduction returnTypeSpecReduction)
            {
                var closure = new ClosureTypeSpec(argsTypeSpecReduction.TypeSpec, returnTypeSpecReduction.TypeSpec);
                closure.Throws = throws;
                closure.IsAsync = async;
                if (!noEscaping)
                    closure.Attributes.Add(new TypeSpecAttribute("escaping"));
                return new TypeSpecReduction() { Symbol = argsTypeSpecReduction.Symbol, TypeSpec = closure };
            }
            else
            {
                return ReductionErrorLow(ExpectedButGot("TypeSpecReduction in function return type", reduction.GetType().Name, mangledName), mangledName);
            }
        }
        else
        {
            return ReductionErrorLow(ExpectedButGot("TypeSpecReduction in argument tuple type", reduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// Convert a Function node into a FunctionReduction
    /// </summary>
    /// <param name="node">A Function node</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A FunctionReduction</returns>
    static IReduction ConvertFunction(Node node, string mangledName)
    {
        // Expecting:
        // Function
        //   Module/Nominal (provenance)
        //   Identifier
        //   [LabelList]
        //   Type
        var provenanceNode = node.Children[0];
        var identifierNode = node.Children[1];
        var labelList = node.Children[2].Kind == NodeKind.LabelList ? node.Children[2] : null;
        var typeNodeIndex = labelList is not null ? 3 : 2;
        var typeNode = node.Children[typeNodeIndex];

        var reduction = Convert(provenanceNode, mangledName);
        if (reduction is TypeSpecReduction ts)
            reduction = TypeSpecToProvenance(ts);

        if (reduction is ReductionError error)
        {
            error.Severity = ReductionErrorSeverity.High;
            return error;
        }
        else if (reduction is ProvenanceReduction provenance)
        {
            var identifier = identifierNode.Text;
            var labels = labelList is not null ? labelList.Children.Select(n => n.Kind == NodeKind.Identifier ? n.Text : "").ToArray() : new string[0];
            reduction = Convert(typeNode, mangledName);
            if (reduction is ReductionError typeError)
            {
                typeError.Severity = ReductionErrorSeverity.High;
                return typeError;
            }
            else if (reduction is TypeSpecReduction typeSpecReduction)
            {
                if (typeSpecReduction.TypeSpec is ClosureTypeSpec closure)
                {

                    var args = closure.ArgumentsAsTuple;
                    for (var i = 0; i < labels.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(labels[i]))
                            args.Elements[i].TypeLabel = labels[i];
                    }
                    var function = new SwiftFunction() { Name = identifier, ParameterList = args, Provenance = provenance.Provenance, Return = closure.ReturnType, IsAsync = closure.IsAsync };
                    function.GenericParameters.AddRange(closure.GenericParameters);
                    return new FunctionReduction() { Symbol = mangledName, Function = function };
                }
                else
                {
                    return ReductionErrorHigh(ExpectedButGot("ClosureTypeSpec as Function Type", typeSpecReduction.TypeSpec.GetType().Name, mangledName), mangledName);
                }
            }
            else
            {
                return ReductionErrorHigh(ExpectedButGot("TypeSpecReduction in Function Type", reduction.GetType().Name, mangledName), mangledName);
            }
        }
        else
        {
            return ReductionErrorHigh(ExpectedButGot("ProvenanceReduction in Function Module", reduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// ConvertAllocator converts an Allocator node to a FunctionRetuction. An allocator is similar to a function except that it
    /// never has an implicit name instead of an Identifier and it never has a LabelList since there are never any parameters.
    /// </summary>
    /// <param name="node">An Allocator node</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A FunctionReduction</returns>
    static IReduction ConvertAllocator(Node node, string mangledName)
    {
        // Expecting:
        // Function
        //   Module/Nominal (provenance)
        //   Type
        var provenanceNode = node.Children[0];
        var typeNode = node.Children[1];

        var reduction = Convert(provenanceNode, mangledName);
        if (reduction is TypeSpecReduction ts)
            reduction = TypeSpecToProvenance(ts);

        if (reduction is ReductionError error)
        {
            error.Severity = ReductionErrorSeverity.High;
            return error;
        }
        else if (reduction is ProvenanceReduction provenance)
        {
            var identifier = "__allocating_init";
            reduction = Convert(typeNode, mangledName);
            if (reduction is ReductionError typeError)
            {
                typeError.Severity = ReductionErrorSeverity.High;
                return typeError;
            }
            else if (reduction is TypeSpecReduction typeSpecReduction)
            {
                if (typeSpecReduction.TypeSpec is ClosureTypeSpec closure)
                {
                    var args = closure.ArgumentsAsTuple;
                    var function = new SwiftFunction() { Name = identifier, ParameterList = args, Provenance = provenance.Provenance, Return = closure.ReturnType, IsAsync = closure.IsAsync };
                    return new FunctionReduction() { Symbol = mangledName, Function = function };
                }
                else
                {
                    return ReductionErrorHigh(ExpectedButGot("ClosureTypeSpec as Allocator Type", typeSpecReduction.TypeSpec.GetType().Name, mangledName), mangledName);
                }
            }
            else
            {
                return ReductionErrorHigh(ExpectedButGot("TypeSpecReduction in Allocator Type", reduction.GetType().Name, mangledName), mangledName);
            }
        }
        else
        {
            return ReductionErrorHigh(ExpectedButGot("ProvenanceReduction in Allocator Module", reduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// ConvertDispatchThunkFunction converts a DispatchThunk node to a DispatchThunkFunctionReduction. This is essentially
    /// the same as a FunctionReduction but in a different type so it's easy to locate
    /// </summary>
    /// <param name="node">A DispatchThunk node</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A DispatchThunkFunctionReduction</returns>
    static IReduction ConvertDispatchThunkFunction(Node node, string mangledName)
    {
        var childReduction = ConvertFirstChild(node, mangledName);
        if (childReduction is ReductionError error)
        {
            error.Severity = ReductionErrorSeverity.High;
            return error;
        }
        if (childReduction is FunctionReduction funcReduction)
        {
            return DispatchThunkFunctionReduction.FromFunctionReduction(funcReduction);
        }
        else
        {
            return ReductionErrorHigh(ExpectedButGot("FunctionReduction in DispatchThunk", childReduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// ConvertMetadataAcessor converts a TypeMetadataAccessor node to a MetadataAccessorReduction.
    /// </summary>
    /// <param name="node">A TypeMetadataAccessor node</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A MetadataAccessorReduction</returns>
    static IReduction ConvertMetadataAcessor(Node node, string mangledName)
    {
        // Expecting:
        // TypeMetadataAccessFunction
        //    Type
        //       nominal type
        var childReduction = ConvertFirstChild(node, mangledName);
        if (childReduction is ReductionError error)
        {
            error.Severity = ReductionErrorSeverity.High;
            return error;
        }
        if (childReduction is TypeSpecReduction typeSpec)
        {
            if (typeSpec.TypeSpec is NamedTypeSpec ns)
            {
                return new MetadataAccessorReduction() { Symbol = mangledName, TypeSpec = ns };
            }
            else
            {
                return ReductionErrorHigh(ExpectedButGot("NamedTypeSpec in MetadataAccessor", typeSpec.TypeSpec.GetType().Name, mangledName), mangledName);
            }
        }
        else
        {
            return ReductionErrorHigh(ExpectedButGot("TypeSpecReduction in Metadata Accessor", childReduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// Convert a bound generic nominal type to a named type spec
    /// </summary>
    /// <param name="node">A bound generic struct, class, or enum node</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpectReduction with a named typed spec</returns>
    static IReduction ConvertBoundGenericNominal(Node node, string mangledName)
    {
        // Expecting:
        // Type
        //   nominal type
        // TypeList
        //   Type0
        //   ...
        //   TypeN

        var hostTypeReduction = ConvertFirstChild(node.Children[0], mangledName);
        if (hostTypeReduction is ReductionError err0)
        {
            return err0;
        }
        else if (hostTypeReduction is TypeSpecReduction hostType)
        {
            try
            {
                var typeList = ConvertTypeListToTypeSpecList(node.Children[1], mangledName);
                hostType.TypeSpec.GenericParameters.AddRange(typeList);
                return hostType;
            }
            catch (Exception error)
            {
                return ReductionErrorLow($"Error converting bound generic type list: {error.Message}", mangledName);
            }
        }
        else
        {
            return ReductionErrorLow(ExpectedButGot("TypeSpecReduction in BoundGenericNominal", hostTypeReduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// Converts a TypeList node into a List<TypeSpec>
    /// </summary>
    /// <param name="node">A Node of type TypeList</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A list of TypeSpec</returns>
    /// <exception cref="Exception">Throws on error in convertsion</exception>
    static List<TypeSpec> ConvertTypeListToTypeSpecList(Node node, string mangledName)
    {
        var typeList = new List<TypeSpec>();
        var index = 0;
        foreach (var child in node.Children)
        {
            var reduction = Convert(child, mangledName);
            if (reduction is ReductionError err)
            {
                throw new Exception($"Failed to convert type list child {index} of type {child.Kind}");
            }
            else if (reduction is TypeSpecReduction childType)
            {
                typeList.Add(childType.TypeSpec);
            }
            else
            {
                throw new Exception($"Failed to convert type list child {index}. Expected a TypeSpecReduction but got a {reduction.GetType().Name}");
            }
            index++;
        }
        return typeList;
    }

    /// <summary>
    /// Converts a dependent generic parameter to a TypeSpecReduction
    /// </summary>
    /// <param name="node">A Node of type DependentGenericParameterType</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction containing a NamedTypeSpec</returns>
    static IReduction ConvertDependentGenericParameter(Node node, string mangledName)
    {
        // Expecting:
        //  Index - depth
        //  Index - index

        var depth = node.Children[0].Index;
        var index = node.Children[1].Index;
        var named = new NamedTypeSpec(FormatGenericParameter(depth, index));
        return new TypeSpecReduction() { Symbol = mangledName, TypeSpec = named };
    }

    /// <summary>
    /// Converts a dependent member with optional associated type path to a TypeSpecReduction
    /// </summary>
    /// <param name="node">A Node of type DependentMemberType</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction containing a NamedTypeSpec</returns>
    static IReduction ConvertDependentMember(Node node, string mangledName)
    {
        // Expected:
        // Type
        // [DependentAssociatedTypeRef]
        var childReduction = ConvertFirstChild(node, mangledName);
        if (childReduction is ReductionError error)
            return error;
        else if (childReduction is TypeSpecReduction hostType)
        {
            if (node.Children.Count < 2)
                return hostType;
            if (hostType.TypeSpec is NamedTypeSpec ns)
            {
                var newNS = new NamedTypeSpec($"{ns.ToString()}.{node.Children[1].Text}");
                return new TypeSpecReduction() { Symbol = mangledName, TypeSpec = newNS };
            }
            else
            {
                return ReductionErrorLow(ExpectedButGot("NamedTypeSpec in TypeSpecReduction", hostType.TypeSpec.GetType().Name, mangledName), mangledName);
            }
        }
        else
        {
            return ReductionErrorLow(ExpectedButGot("TypeSpecReduction in DependentMember", childReduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// Convert a dependent generic type into a TypeSpecReduction
    /// </summary>
    /// <param name="node">A Node of type DependentGenericType</param>
    /// <param name="mangledName">the mangled name that generated the Node</param>
    /// <returns>A TypeSpecReduction</returns>
    static IReduction ConvertDependentGenericType(Node node, string mangledName)
    {
        // Expected
        // DependentGenericSignature
        //    DependentGenericParamCount - index = num of generic parameters
        // Type

        var reduction = Convert(node.Children[1], mangledName);
        if (reduction is ReductionError error)
            return error;
        else if (reduction is TypeSpecReduction ts)
        {
            var nArgs = node.Children[0].Children[0].Index;
            ts.TypeSpec.GenericParameters.AddRange(GenerateGenericParameters(0, nArgs));
            return reduction;
        }
        else
        {
            return ReductionErrorLow(ExpectedButGot("TypeSpecReduction in DependentGenericType", reduction.GetType().Name, mangledName), mangledName);
        }
    }

    /// <summary>
    /// Generate a series of generic parameters using the given depth and index
    /// </summary>
    /// <param name="depth">The depth coordinate of the parameter</param>
    /// <param name="maxIndex">The maximum number of parameters to generate</param>
    /// <returns>A sequence of NamedTypeSpec objects named for the spec</returns>
    static IEnumerable<TypeSpec> GenerateGenericParameters(long depth, long maxIndex)
    {
        for (long i = 0; i < maxIndex; i++)
        {
            yield return new NamedTypeSpec(FormatGenericParameter(depth, i));
        }
    }

    /// <summary>
    /// Formats a generic parameter for printing
    /// </summary>
    /// <param name="depth">The depth coordinate of the parameter</param>
    /// <param name="index">The maximum number of parameters to generate</param>
    /// <returns>A string in the form T_depth_index</returns>
    static string FormatGenericParameter(long depth, long index)
    {
        return $"T_{depth}_{index}";
    }

    /// <summary>
    /// Returns the nested nominal name from node in the StringBuilder or throw an exception on error
    /// </summary>
    static void GetNestedNominalName(Node node, StringBuilder sb, string mangledName)
    {
        if (IsNominal(node.Children[0]))
        {
            GetNestedNominalName(node.Children[0], sb, mangledName);
            sb.Append('.').Append(FirstChildIdentifierText(node, mangledName));
        }
        else
        {
            var moduleName = node.Children[0].Text;
            var typeName = FirstChildIdentifierText(node, mangledName);
            sb.Append(moduleName).Append('.').Append(typeName);
        }
    }

    /// <summary>
    /// Returns the text of the first child of a node if and only if that child is an Identifier, else throw
    /// </summary>
    static string FirstChildIdentifierText(Node node, string mangledName)
    {
        if (node.Children[1].Kind != NodeKind.Identifier)
            throw new Exception(ExpectedButGot("Identifier", node.Children[1].Kind.ToString(), mangledName));
        return node.Children[1].Text;
    }

    /// <summary>
    /// Returns true if and only if the node Kind is one of the swift nominal types
    /// </summary>
    static bool IsNominal(Node node)
    {
        var kind = node.Kind;
        return kind == NodeKind.Class || kind == NodeKind.Structure || kind == NodeKind.Enum || kind == NodeKind.Protocol;
    }

    /// <summary>
    /// Recurce on Convert with the first child
    /// </summary>
    static IReduction ConvertFirstChild(Node node, string mangledName)
    {
        return Convert(node.Children[0], mangledName);
    }

    /// <summary>
    /// Return a string in the form mangledName: expected xxxx but got yyyy
    /// </summary>
    static string ExpectedButGot(string expected, string butGot, string mangledName)
    {
        return $"Demangling {mangledName}: expected {expected} but got {butGot}";
    }

    /// <summary>
    /// Convenience factory for reduction errors
    /// </summary>
    static ReductionError ReductionError(string message, string mangledName, ReductionErrorSeverity severity)
    {
        return new ReductionError() { Symbol = mangledName, Message = message, Severity = severity };
    }

    /// <summary>
    /// Convenience factory for a ReductionError with the given message and mangled name with the severity set to low
    /// </summary>
    /// <param name="message">the error message</param>
    /// <param name="mangledName">the mangled name that was demangled and reduced</param>
    /// <returns>a Reduction error of low severity</returns>
    static ReductionError ReductionErrorLow(string message, string mangledName)
    {
        return ReductionError(message, mangledName, ReductionErrorSeverity.Low);
    }

    /// <summary>
    /// Convenience factory for a ReductionError with the given message and mangled name with the severity set to high
    /// </summary>
    /// <param name="message">the error message</param>
    /// <param name="mangledName">the mangled name that was demangled and reduced</param>
    /// <returns>a Reduction error of high severity</returns>
    static ReductionError ReductionErrorHigh(string message, string mangledName)
    {
        return ReductionError(message, mangledName, ReductionErrorSeverity.High);
    }

    /// <summary>
    /// Converts a TypeSpecReduction to a ProvenanceReduction, expecting it to contain a NamedTypeSpec
    /// </summary>
    /// <param name="ts">a TypeSpecReduction to convert</param>
    /// <returns>A ProvenanceReduction if the TypeSpecReduction contains a NamedTypeSpec, otherwise a ReductionError</returns>
    static IReduction TypeSpecToProvenance(TypeSpecReduction ts)
    {
        if (ts.TypeSpec is NamedTypeSpec ns)
            return ProvenanceReduction.Instance(ts.Symbol, ns);
        return ReductionErrorLow(ExpectedButGot("NamedTypeSpec in TypeSpecReduction", ts.TypeSpec.GetType().Name, ts.Symbol), ts.Symbol);
    }
}
