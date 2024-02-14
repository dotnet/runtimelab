// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SwiftReflector.SwiftXmlReflection {

	public static class ExtensionMethods {
		// surprise! You'd think that you could write this code:
		//		public static T DefaultedAttribute<T>(this XElement elem, XName name, T defaultValue = default(T))
		//		{
		//			XAttribute attr = elem.Attribute (name);
		//			if (attr == null)
		//				return defaultValue;
		//			return (T)attr;
		//		}
		// but the last cast won't work because generics aren't templates.

		// So instead, we get to repeat ourselves a lot.

		public static bool BoolAttribute (this XElement elem, XName name, bool defaultValue = default (bool))
		{
			XAttribute attr = elem.Attribute (name);
			if (attr == null)
				return defaultValue;
			return (bool)attr;
		}

		public static double DoubleAttribute (this XElement elem, XName name, double defaultValue = default (double))
		{
			XAttribute attr = elem.Attribute (name);
			if (attr == null)
				return defaultValue;
			return (double)attr;
		}

		public static bool IsPrivateOrInternal (this Accessibility a)
		{
			return a == Accessibility.Private || a == Accessibility.Internal;
		}
	}
}

