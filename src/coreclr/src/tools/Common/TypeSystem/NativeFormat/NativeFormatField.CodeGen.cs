// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Internal.TypeSystem.NativeFormat
{
    partial class NativeFormatField
    {
        public override bool IsIntrinsic
        {
            get
            {
                return (GetFieldFlags(FieldFlags.AttributeMetadataCache | FieldFlags.Intrinsic) & FieldFlags.Intrinsic) != 0;
            }
        }
    }
}
