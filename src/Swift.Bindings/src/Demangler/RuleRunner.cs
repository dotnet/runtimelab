// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration.Demangling;

/// <summary>
/// RuleRunner contains a a collection of rules that get run to reduce nodes
/// </summary>
internal class RuleRunner {
    string mangledName;
    List<MatchRule> rules = new List<MatchRule>();

    /// <summary>
    /// Constructs a new rules runner initialized with the give rule set
    /// </summary>
    /// <param name="rules">Rules to test against a node</param>
    public RuleRunner(IEnumerable<MatchRule> rules, string mangledName)
    {
        this.mangledName = mangledName;
        this.rules.AddRange(rules);
    }

    /// <summary>
    /// Run a set of rules on the given node and return a reduction on that node. If there was no match
    /// or there was an error, this will return a IReduction of type ReductionError
    /// </summary>
    /// <param name="node">A node to attempt to match</param>
    /// <param name="name">A name used for the reduction</param>
    /// <returns></returns>
    public IReduction RunRules(Node node, string? name)
    {
        var rule = rules.FirstOrDefault (r => r.Matches(node));
        
        if (rule is null)
            return new ReductionError() { Symbol = mangledName, Message = $"No rule for node {node.Kind}" };
        return rule.Reducer(node, name);
    }
}
