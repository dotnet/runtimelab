// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Collections.Generic;

namespace System.Reflection
{
    public partial class CustomAttributeData
    {
        protected CustomAttributeData() { }

        public virtual ConstructorInfo Constructor => null;
        public virtual IList<CustomAttributeTypedArgument> ConstructorArguments { get { throw new NullReferenceException(); } }
        public virtual IList<CustomAttributeNamedArgument> NamedArguments => null;
    }
}
