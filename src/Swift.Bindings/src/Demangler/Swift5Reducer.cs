// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Net.Sockets;
using System.CommandLine;

namespace BindingsGeneration.Demangling;

/// <summary>
/// Swift5Reducer takes a tree of Node and attempts to reduce it completely to an easier to
/// to manipulate data structure. It does this by matching patterns in the tree and on match
/// applying a reduction function on the matched Node.
/// </summary>
internal class Swift5Reducer {
    string mangledName;
    RuleRunner ruleRunner;
    List<MatchRule> rules;

    /// <summary>
    /// Construct Swift5Reducer to operate on a tree of nodes build from the given mangled name
    /// </summary>
    public Swift5Reducer (string mangledName)
    {
        this.mangledName = mangledName;
        rules = BuildMatchRules ();
        ruleRunner = new RuleRunner (rules, mangledName);
    }

    /// <summary>
    /// Build a set of match rules for Node reduction
    /// </summary>
    List<MatchRule> BuildMatchRules () =>
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
            Name = "Module", NodeKind = NodeKind.Module, Reducer = (n, s) => ProvenanceReduction.TopLevel (mangledName, n.Text)
        },
        new MatchRule() {
            Name = "Tuple", NodeKind = NodeKind.Tuple, Reducer = ConvertTuple
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
            Name = "FunctionType", NodeKind = NodeKind.FunctionType, Reducer = ConvertFunctionType,
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
    };

    /// <summary>
    /// Convert a Node into an IReduction. On failure, the IReduction will be type ReductionError
    /// </summary>
    public IReduction Convert (Node node)
    {
        return ruleRunner.RunRules (node, null);
    }

    /// <summary>
    /// Given a ProtocolWitnessTable node, convert to a ProtocolWitnessTable reduction
    /// </summary>
    IReduction ConvertProtocolConformance (Node node, string? name)
    {
        // What to expect here:
        // ProtocolConformance
        //     Type
        //     Type
        //     Module - ONLY FOR ProtocolConformanceDescriptor

        var isDescriptor = node.Kind == NodeKind.ProtocolConformanceDescriptor;

        var child = node.Children [0];
        if (child.Kind != NodeKind.ProtocolConformance) {
            return ReductionError (ExpectedButGot ("ProtocolConformance", node.Kind.ToString ()));
        }
        var grandchild = Convert (child.Children [0]);
        if (grandchild is ReductionError) return grandchild;
        var implementingType = grandchild as TypeSpecReduction;
        if (implementingType is null) {
            return ReductionError (ExpectedButGot ("Nominal type implementing protocol", grandchild.GetType().Name));
        }

        grandchild = Convert (child.Children [1]);
        if (grandchild is ReductionError) return grandchild;
        var protocolType = grandchild as TypeSpecReduction;
        if (protocolType is null) {
            return ReductionError (ExpectedButGot ("Nominal type protocol", grandchild.GetType().Name));
        }

        var impNamed = (NamedTypeSpec)implementingType.TypeSpec;
        var protoNamed = (NamedTypeSpec)protocolType.TypeSpec;

        if (isDescriptor) {
            grandchild = Convert (child.Children [2]);
            if (grandchild is ProvenanceReduction prov) {
                if (!prov.Provenance.IsTopLevel)
                    return ReductionError (ExpectedButGot ("A top-level module name", prov.Provenance.ToString()));
                return new ProtocolConformanceDescriptorReduction() { Symbol = mangledName, ImplementingType = impNamed, ProtocolType = protoNamed, Module = prov.Provenance.Module};
            } else if (grandchild is ReductionError) {
                return grandchild;
            } else {
                return ReductionError (ExpectedButGot ("ProvenanceReduction", grandchild.GetType().Name));
            }
            
        } else {
            return new ProtocolWitnessTableReduction() { Symbol = mangledName, ImplementingType = impNamed, ProtocolType = protoNamed };
        }
    }

    /// <summary>
    /// Convert a nominal node into TypeSpecReduction
    /// </summary>
    IReduction ConvertNominal (Node node, string? name)
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

        var sb = new StringBuilder ();
        try {
            GetNestedNominalName (node, sb);
        } catch (Exception err) {
            return ReductionError (err.Message);
        }
        return new TypeSpecReduction() { Symbol = mangledName, TypeSpec = new NamedTypeSpec (sb.ToString ())};
    }

    /// <summary>
    /// Converts a node of type Tuple into a TypeSpecReduction.
    /// </summary>
    /// <param name="node">The node for a Tuple</param>
    /// <param name="name">an optional name</param>
    /// <returns>a TypeSpecReduction</returns>
    /// 
    IReduction ConvertTuple (Node node, string? name)
    {
        // What to expect here:
        // Tuple
        //   TupleElement*   - 0 or more TupleElement nodes

        // No children, empty tuple
        if (node.Children.Count == 0)
            return new TypeSpecReduction () { Symbol = mangledName, TypeSpec = TupleTypeSpec.Empty };

        var types = new List<TypeSpec> ();

        foreach (var child in node.Children) {
            var reduction = Convert (child);
            if (reduction is ReductionError error)
                return error;
            else if (reduction is TypeSpecReduction typeSpecReduction) {
                // Every child should be a Type node which turns into a TypeSpecReduction
                types.Add (typeSpecReduction.TypeSpec);
            } else {
                return ReductionError (ExpectedButGot ("TypeSpecReduction in tuple", reduction.GetType ().Name));
            }
        }

        return new TypeSpecReduction () { Symbol = mangledName, TypeSpec = new TupleTypeSpec (types)};
    }

    /// <summary>
    /// Converts a node of type TupleElement into a TypeSpecReduction
    /// </summary>
    /// <param name="node">The node for a TupleElement</param>
    /// <param name="name">an optional name</param>
    /// <returns>a TypeSpecReduction</returns>
    IReduction ConvertTupleElement (Node node, string? name)
    {
        // What to expect here:
        // TupleElement
        //    TupleElementName
        //    Type
        // --- OR ---
        // TupleElement
        //    Type
        var isNamedElement = node.Children [0].Kind == NodeKind.TupleElementName;
        var label = isNamedElement ? node.Children [0].Text : null;
        var index = isNamedElement ? 1 : 0;
        var reduction = Convert (node.Children [index]);
        if (reduction is ReductionError error)
            return error;
        else if (reduction is TypeSpecReduction typeSpecReduction) {
            typeSpecReduction.TypeSpec.TypeLabel = label;
            return typeSpecReduction;
        } else {
            return ReductionError (ExpectedButGot ("TypeSpecReduction in tuple element", reduction.GetType ().Name));
        }
    }

    /// <summary>
    /// Converts a TupleElement with a VariadicMarker into a TypeSpecReduction of type Swift.Array<Type>
    /// </summary>
    /// <param name="node">A TupleElement node with a VariadicMarker</param>
    /// <param name="name">an optional name</param>
    /// <returns>A TypeSpecReduction with a Swift.Array<T></returns>
    IReduction ConvertVariadicTupleElement (Node node, string? name)
    {
        // What to expect here:
        // TupleElement
        //    VariadicMarker
        //    Type

        // will turn this into a named type spec Swift.Array<Type>
        var reduction = Convert (node.Children [1]);
        if (reduction is ReductionError error)
            return error;
        else if (reduction is TypeSpecReduction typeSpecReduction) {
            var newSpec = new NamedTypeSpec ("Swift.Array");
            newSpec.GenericParameters.Add (typeSpecReduction.TypeSpec);
            typeSpecReduction.TypeSpec.IsVariadic = true;
            return new TypeSpecReduction () { Symbol = typeSpecReduction.Symbol, TypeSpec = newSpec };
        } else {
            return ReductionError (ExpectedButGot ("TypeSpecReduction in variadic tuple element", reduction.GetType ().Name));
        }
    }

    /// <summary>
    /// Convert a FunctionType node into a TypeSpecReduction
    /// </summary>
    /// <param name="node">a FunctionType node</param>
    /// <param name="name">an optional string</param>
    /// <returns>A TypeSpecReduction</returns>
    IReduction ConvertFunctionType (Node node, string? name)
    {
        // What to expect here:
        // FunctionType
        //    ArgumentTuple
        //        Type
        //    ReturnType
        //        Type

        var argTuple = node.Children [0];
        var @return = node.Children [1];

        var reduction = ConvertFirstChild (argTuple, name);
        if (reduction is ReductionError error)
            return error;
        else if (reduction is TypeSpecReduction argsTypeSpecReduction) {
            reduction = ConvertFirstChild (@return, name);
            if (reduction is ReductionError returnError)
                return returnError;
            else if (reduction is TypeSpecReduction returnTypeSpecReduction) {
                var closure = new ClosureTypeSpec (argsTypeSpecReduction.TypeSpec, returnTypeSpecReduction.TypeSpec);
                return new TypeSpecReduction () { Symbol = argsTypeSpecReduction.Symbol, TypeSpec = closure };
            } else {
                return ReductionError (ExpectedButGot ("TypeSpecReduction in function return type", reduction.GetType ().Name));
            }            
        } else {
            return ReductionError (ExpectedButGot ("TypeSpecReduction in argument tuple type", reduction.GetType ().Name));
        }
    }

    /// <summary>
    /// Convert a Function node into a FunctionReduction
    /// </summary>
    /// <param name="node">A Function node</param>
    /// <param name="name">an optional string</param>
    /// <returns>A FunctionReduction</returns>
    IReduction ConvertFunction (Node node, string? name)
    {
        // Expecting:
        // Function
        //   Module/Nominal (provenance)
        //   Identifier
        //   [LabelList]
        //   Type
        var provenanceNode = node.Children [0];
        var identifierNode = node.Children [1];
        var labelList = node.Children [2].Kind == NodeKind.LabelList ? node.Children [2] : null;
        var typeNodeIndex = labelList is not null ? 3 : 2;
        var typeNode = node.Children [typeNodeIndex];

        var reduction = Convert (provenanceNode);
        if (reduction is TypeSpecReduction ts)
            reduction = TypeSpecToProvenance (ts);

        if (reduction is ReductionError error)
            return error;
        else if (reduction is ProvenanceReduction provenance) {
            var identifier = identifierNode.Text;
            var labels = labelList is not null ? labelList.Children.Select (n => n.Kind == NodeKind.Identifier ? n.Text : "").ToArray() : new string [0];
            reduction = Convert (typeNode);
            if (reduction is ReductionError typeError)
                return typeError;
            else if (reduction is TypeSpecReduction typeSpecReduction) {
                if (typeSpecReduction.TypeSpec is ClosureTypeSpec closure) {

                    var args = closure.ArgumentsAsTuple;
                    for (var i = 0; i < labels.Length; i++) {
                        if (!string.IsNullOrEmpty (labels [i]))
                            args.Elements [i].TypeLabel = labels [i];
                    }
                    var function = new SwiftFunction () { Name = identifier, ParameterList = args, Provenance = provenance.Provenance, Return = closure.ReturnType };
                    return new FunctionReduction () { Symbol = mangledName, Function = function };
                } else {
                    return ReductionError (ExpectedButGot ("ClosureTypeSpec as Function Type", typeSpecReduction.TypeSpec.GetType ().Name));
                }
            } else {
                return ReductionError (ExpectedButGot ("TypeSpecReduction in Function Type", reduction.GetType ().Name));
            }
        } else {
            return ReductionError (ExpectedButGot ("ProvenanceReduction in Function Module", reduction.GetType ().Name));
        }
    }

    /// <summary>
    /// ConvertAllocator converts an Allocator node to a FunctionRetuction. An allocator is similar to a function except that it
    /// never has an implicit name instead of an Identifier and it never has a LabelList since there are never any parameters.
    /// </summary>
    /// <param name="node">An Allocator node</param>
    /// <param name="name">an optional name</param>
    /// <returns>A FunctionReduction</returns>
    IReduction ConvertAllocator (Node node, string? name)
    {
        // Expecting:
        // Function
        //   Module/Nominal (provenance)
        //   Type
        var provenanceNode = node.Children [0];
        var typeNode = node.Children [1];

        var reduction = Convert (provenanceNode);
        if (reduction is TypeSpecReduction ts)
            reduction = TypeSpecToProvenance (ts);

        if (reduction is ReductionError error)
            return error;
        else if (reduction is ProvenanceReduction provenance) {
            var identifier = "__allocating_init";
            reduction = Convert (typeNode);
            if (reduction is ReductionError typeError)
                return typeError;
            else if (reduction is TypeSpecReduction typeSpecReduction) {
                if (typeSpecReduction.TypeSpec is ClosureTypeSpec closure) {
                    var args = closure.ArgumentsAsTuple;
                    var function = new SwiftFunction () { Name = identifier, ParameterList = args, Provenance = provenance.Provenance, Return = closure.ReturnType };
                    return new FunctionReduction () { Symbol = mangledName, Function = function };
                } else {
                    return ReductionError (ExpectedButGot ("ClosureTypeSpec as Allocator Type", typeSpecReduction.TypeSpec.GetType ().Name));
                }
            } else {
                return ReductionError (ExpectedButGot ("TypeSpecReduction in Allocator Type", reduction.GetType ().Name));
            }
        } else {
            return ReductionError (ExpectedButGot ("ProvenanceReduction in Allocator Module", reduction.GetType ().Name));
        }
    }

    /// <summary>
    /// ConvertDispatchThunkFunction converts a DispatchThunk node to a DispatchThunkFunctionReduction. This is essentially
    /// the same as a FunctionReduction but in a different type so it's easy to locate
    /// </summary>
    /// <param name="node">A DispatchThunk node</param>
    /// <param name="name">An optional name</param>
    /// <returns>A DispatchThunkFunctionReduction</returns>
    IReduction ConvertDispatchThunkFunction (Node node, string? name)
    {
        var childReduction = ConvertFirstChild (node, name);
        if (childReduction is ReductionError error)
            return error;
        if (childReduction is FunctionReduction funcReduction) {
            return DispatchThunkFunctionReduction.FromFunctionReduction (funcReduction);
        } else {
            return ReductionError (ExpectedButGot ("FunctionReduction in DispatchThunk", childReduction.GetType ().Name));
        }
    }

    /// <summary>
    /// Returns the nested nominal name from node in the StringBuilder or throw an exception on error
    /// </summary>
    void GetNestedNominalName (Node node, StringBuilder sb)
    {
        if (IsNominal(node.Children [0])) {
            GetNestedNominalName (node.Children [0], sb);
            sb.Append('.').Append (FirstChildIdentifierText (node));
        } else {
            var moduleName = node.Children [0].Text;
            var typeName = FirstChildIdentifierText (node);
            sb.Append (moduleName).Append ('.').Append (typeName);
        }
    }

    /// <summary>
    /// Returns the text of the first child of a node if and only if that child is an Identifier, else throw
    /// </summary>
    string FirstChildIdentifierText (Node node)
    {
        if (node.Children [1].Kind != NodeKind.Identifier)
            throw new Exception (ExpectedButGot ("Identifier", node.Children [1].Kind.ToString()));
        return node.Children [1].Text;
    }

    /// <summary>
    /// Returns true if and only if the node Kind is one of the swift nominal types
    /// </summary>
    static bool IsNominal (Node node)
    {
        var kind = node.Kind;
        return kind == NodeKind.Class || kind == NodeKind.Structure || kind == NodeKind.Enum || kind == NodeKind.Protocol;
    }

    /// <summary>
    /// Recurce on Convert with the first child
    /// </summary>
    IReduction ConvertFirstChild (Node node, string? name)
    {
        return Convert (node.Children [0]);
    }

    /// <summary>
    /// Return a string in the form mangledName: expected xxxx but got yyyy
    /// </summary>
    string ExpectedButGot (string expected, string butGot)
    {
        return $"Demangling {mangledName}: expected {expected} but got {butGot}";
    }

    /// <summary>
    /// Convenience factory for reduction errors
    /// </summary>
    ReductionError ReductionError (string message)
    {
        return new ReductionError() { Symbol = mangledName, Message = message };
    }

    /// <summary>
    /// Converts a TypeSpecReduction to a ProvenanceReduction, expecting it to contain a NamedTypeSpec
    /// </summary>
    /// <param name="ts">a TypeSpecReduction to convert</param>
    /// <returns>A ProvenanceReduction if the TypeSpecReduction contains a NamedTypeSpec, otherwise a ReductionError</returns>
    IReduction TypeSpecToProvenance (TypeSpecReduction ts)
    {
        if (ts.TypeSpec is NamedTypeSpec ns)
            return ProvenanceReduction.Instance (ts.Symbol, ns);
        return ReductionError (ExpectedButGot ("NamedTypeSpec in TypeSpecReduction", ts.TypeSpec.GetType ().Name));
    }
}
