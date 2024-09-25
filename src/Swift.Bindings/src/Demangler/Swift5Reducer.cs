// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            Name = "ProtocolWitnessTable", NodeKind = NodeKind.ProtocolWitnessTable, Reducer = ConvertProtocolWitnessTable
        },
        new MatchRule() {
            Name = "Type", NodeKind = NodeKind.Type, Reducer = ConvertFirstChild
        },
        new MatchRule() {
            Name = "Nominal", NodeKindList = new List<NodeKind>() { NodeKind.Class, NodeKind.Structure, NodeKind.Protocol, NodeKind.Enum },
            Reducer = ConvertNominal
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
    IReduction ConvertProtocolWitnessTable (Node node, string? name)
    {
        // What to expect here:
        // ProtocolConformance
        //     Type
        //     Type
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

        return new ProtocolWitnessTableReduction() { Symbol = mangledName, ImplementingType = impNamed, ProtocolType = protoNamed };
    }

    /// <summary>
    /// Convert a nominal node into TypeSpecReduction
    /// </summary>
    IReduction ConvertNominal (Node node, string? name)
    {
        // What to expect here:
        // Class/Structure/Protocol/Enum
        //    Module Name
        //    Identifier Name
        var kind = node.Kind;
        if (kind == NodeKind.Class || kind == NodeKind.Structure || kind == NodeKind.Enum || kind == NodeKind.Protocol) {
            var moduleName = node.Children [0].Text;
            if (node.Children[1].Kind != NodeKind.Identifier)
                return ReductionError(ExpectedButGot("Identifier", node.Children[1].Kind.ToString()));
            var typeName = node.Children [1].Text;    
            return new TypeSpecReduction() { Symbol = mangledName, TypeSpec = new NamedTypeSpec ($"{moduleName}.{typeName}")};
        }
        return ReductionError(ExpectedButGot("Class/Struct/Enum/Protocol", kind.ToString()));
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