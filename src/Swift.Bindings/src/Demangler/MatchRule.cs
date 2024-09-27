// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace BindingsGeneration.Demangling;

/// <summary>
/// MatchRule represents a tree reducing rule for a demangled Swift symbol.
/// Given a tree of nodes, a match rule contains conditions for the match,
/// which include:
/// - If and how the node content should be matched
/// - If the number of children should match
/// - Rules to run on the children
/// - One or more NodeKinds to match
/// If a match occurs, a reducer function can be run on the node.
/// </summary>
[DebuggerDisplay("{ToString()}")]
internal class MatchRule {
    /// <summary>
    /// The name of this rule - useful for debugging
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// A list of node kinds to match. 
    /// </summary>
    public List<NodeKind> NodeKindList { get; set; } = new List<NodeKind>();

    /// <summary>
    /// A convenience accessor to match on a single NodeKind
    /// </summary>
    public NodeKind NodeKind {
        get {
            if (NodeKindList.Count != 1) {
                throw new InvalidOperationException($"NodeKind is invalid when NodeKindList has {NodeKindList.Count} entries.");
            }
            return NodeKindList[0];
        }
        set {
            NodeKindList = new List<NodeKind> { value };
        }
    }

    /// <summary>
    /// If and how to match the content of the Node
    /// </summary>
    public MatchNodeContentType MatchContentType { get; init; } = MatchNodeContentType.None;

    /// <summary>
    /// Rules to apply to children node.
    /// </summary>
    public List<MatchRule> ChildRules { get; init; } = new List<MatchRule>();

    /// <summary>
    /// Whether or not the total number of children should match
    /// </summary>
    public bool MatchChildCount { get; init; } = false;

    /// <summary>
    /// A reducer to apply if the node matches
    /// </summary>
    public required Func<Node, string?, IReduction> Reducer { get; init; }

    /// <summary>
    /// Returns true if and only if the given node matches this rule
    /// </summary>
    /// <param name="n">a node to match on</param>
    /// <returns></returns>
    public bool Matches(Node n)
    {
        return NodeKindMatches(n) && ContentTypeMatches(n) && ChildrenMatches(n);
    }

    /// <summary>
    /// Returns true if and only if the NodeKind matches
    /// </summary>
    /// <param name="n">a node to match on</param>
    /// <returns></returns>
    bool NodeKindMatches(Node n)
    {
        return NodeKindList.Contains(n.Kind);
    }

    /// <summary>
    /// Returns true if and only if the content of the given node matches
    /// </summary>
    /// <param name="n">a node to match on</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    bool ContentTypeMatches(Node n)
    {
        // Only care about the content type not its value
        switch (MatchContentType)
        {
        case MatchNodeContentType.AlwaysMatch:
            return true;
        case MatchNodeContentType.Index:
            return n.HasIndex;
        case MatchNodeContentType.Text:
            return n.HasText;
        case MatchNodeContentType.None:
            return !n.HasIndex && !n.HasText;
        default:
            throw new InvalidOperationException ($"Unknown match instruction {MatchContentType} in match rule.");
        }
    }

    /// <summary>
    /// Returns true if and only if the children rules matches the given node's children
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    bool ChildrenMatches (Node n)
    {
        // if the rule says the child count matters, apply
        if (MatchChildCount && n.Children.Count != ChildRules.Count)
            return false;

        // match up to the minimum of each list
        // if MatchChileCount is true, min is the size of both lists
        int minimumChildCount = Math.Min (n.Children.Count, ChildRules.Count);
        for (var i = 0; i < minimumChildCount; i++) {
            var childRule = ChildRules [i];
            // recurse
            if (!childRule.Matches (n.Children [i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Creates a simple string representation of this rule
    /// </summary>
    /// <returns>a string representation of the rule</returns>
    public override string ToString() => Name;
}
