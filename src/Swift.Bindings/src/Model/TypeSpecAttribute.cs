// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace BindingsGeneration;

public class TypeSpecAttribute
{
    public TypeSpecAttribute(string name)
    {
        Name = name;
        Parameters = new List<string>();
    }
    public string Name { get; set; }
    public List<string> Parameters { get; private set; }
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
