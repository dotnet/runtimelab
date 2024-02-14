// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SwiftReflector.Demangling {
	public class RuleRunner {
		List<MatchRule> rules = new List<MatchRule> ();
		public RuleRunner (IEnumerable<MatchRule> rules)
		{
			this.rules.AddRange (rules);
		}

		public SwiftType RunRules(Node node, bool isReference, SwiftName name)
		{
			var rule = rules.FirstOrDefault (r => r.Matches (node));

			return rule != null ? rule.Reducer (node, isReference, name) : null;
		}
	}
}
