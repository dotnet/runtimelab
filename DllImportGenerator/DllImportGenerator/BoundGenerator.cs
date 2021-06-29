using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Interop
{
    struct BoundGenerator : IEquatable<BoundGenerator>
    {
        public BoundGenerator(TypePositionInfo typeInfo, IMarshallingGenerator marshallingGenerator)
        {
            TypeInfo = typeInfo;
            Generator = marshallingGenerator;
        }

        public TypePositionInfo TypeInfo { get; }
        public IMarshallingGenerator Generator { get; }

        public void Deconstruct(out TypePositionInfo typeInfo, out IMarshallingGenerator generator)
        {
            typeInfo = TypeInfo;
            generator = Generator;
        }

        public override bool Equals(object obj)
        {
            return obj is BoundGenerator other && Equals(other);
        }

        public bool Equals(BoundGenerator other)
        {
            // Only compare the type info as the selected generator is deterministically
            // determined based on the overall scenario (P/Invoke) and the TypeInfo exclusively.
            return TypeInfo.Equals(other.TypeInfo);
        }

        public override int GetHashCode()
        {
            return TypeInfo.GetHashCode();
        }
    }
}
