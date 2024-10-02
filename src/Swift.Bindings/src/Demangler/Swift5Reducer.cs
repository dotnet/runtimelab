// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Net.Sockets;

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
        }
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
}