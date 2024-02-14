// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftRuntimeLibrary;

namespace SwiftReflector.Demangling {
	public class MatchRule {
		public MatchRule ()
		{
			Name = "";
			NodeKind = NodeKind.Type; // arbitrary
			MatchContent = MatchNodeContentType.AlwaysMatch;
			ChildRules = new List<MatchRule> ();
			MatchChildCount = false;
			Reducer = (n, b, name) => null;
		}

		public string Name { get; set; }
		public NodeKind NodeKind {
			get {
				if (NodeKindList.Count != 1)
					throw new InvalidOperationException ($"NodeKind is invalid when NodeKindList has {NodeKindList.Count} entries.");
				return NodeKindList [0];
			}
			set {
				NodeKindList = new List<NodeKind> { value };
			}
		}
		public List<NodeKind> NodeKindList { get; set; }
		public MatchNodeContentType MatchContent { get; set; }
		public List<MatchRule> ChildRules { get; set; }
		public bool MatchChildCount { get; set; }
		public Func<Node, bool, SwiftName, SwiftType> Reducer { get; set; }


		public bool Matches(Node n)
		{
			Exceptions.ThrowOnNull (n, nameof (n));
			// 3 match criteria: NodeKind, Content type, children
			return NodeKindMatches (n) && ContentMatches (n) &&
				ChildrenMatches (n);
		}

		bool NodeKindMatches(Node n)
		{
			return NodeKindList.Contains (n.Kind);
		}

		bool ContentMatches(Node n)
		{
			// Only care about the content type not its value
			switch (MatchContent)
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
				throw new InvalidOperationException ($"Unknown match instruction {MatchContent} in match rule.");
			}
		}

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

		public override string ToString()
		{
			return Name;
		}
	}
}
