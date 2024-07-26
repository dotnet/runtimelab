// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace BindingsGeneration;

/// <summary>
/// An attribute attached to a type spec
/// </summary>
public class TypeSpecAttribute
{
	/// <summary>
	/// Constructs a new TypeSpecAttribute with the given name. The name doesn't include the '@'
	/// </summary>
    public TypeSpecAttribute(string name)
    {
        Name = name;
        Parameters = new List<string>();
    }

	/// <summary>
	/// Gets or sets the name of the attribute. Does not include the '@'
	/// </summary>
    public string Name { get; set; }

	/// <summary>
	/// A list of optional parameters for the attribute
	/// </summary>
    public List<string> Parameters { get; private set; }

    /// <summary>
	/// Returns a string representation of the attribute
	/// </summary>

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append('@');
        sb.Append(Name);
        if (Parameters.Count > 0)
        {
            sb.Append('(');
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(Parameters[i]);
            }
            sb.Append(')');
        }
        return sb.ToString();
    }
}
