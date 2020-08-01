// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.CommandLine
{
    public sealed class Argument<T> : Argument
    {
        internal Argument(ArgumentCommand command, IEnumerable<string> names, T defaultValue, bool isRequired)
            : base(command, names, true, isRequired)
        {
            Value = defaultValue;
            Value = DefaultValue = defaultValue;
        }

        internal Argument(ArgumentCommand command, string name, T defaultValue)
            : base(command, new[] { name }, false, true)
        {
            Value = defaultValue;
            DefaultValue = defaultValue;
        }

        public new T Value { get; private set; }

        public new T DefaultValue { get; private set; }

        public override bool IsFlag
        {
            get { return typeof(T) == typeof(bool); }
        }

        internal override object GetValue()
        {
            return Value;
        }

        internal override object GetDefaultValue()
        {
            return DefaultValue;
        }

        internal void SetValue(T value)
        {
            Value = value;
            MarkSpecified();
        }
    }
}