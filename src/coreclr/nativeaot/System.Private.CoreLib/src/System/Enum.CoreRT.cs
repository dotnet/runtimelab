// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using Internal.Runtime.CompilerServices;
using Internal.Reflection.Augments;

using CorElementType = System.Reflection.CorElementType;
using EETypeElementType = Internal.Runtime.EETypeElementType;

namespace System
{
    public abstract partial class Enum : ValueType, IComparable, IFormattable, IConvertible
    {
        internal static EnumInfo GetEnumInfo(Type enumType, bool getNames = true)
        {
            Debug.Assert(enumType != null);
            Debug.Assert(enumType is RuntimeType);
            Debug.Assert(enumType.IsEnum);

            return ReflectionAugments.ReflectionCoreCallbacks.GetEnumInfo(enumType);
        }

        private static object InternalBoxEnum(Type enumType, long value)
        {
            return ToObject(enumType.TypeHandle.ToEETypePtr(), value);
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj == null)
                return false;
            EETypePtr eeType = this.EETypePtr;
            if (!eeType.FastEquals(obj.EETypePtr))
                return false;

            ref byte pThisValue = ref this.GetRawData();
            ref byte pOtherValue = ref obj.GetRawData();

            RuntimeImports.RhCorElementTypeInfo corElementTypeInfo = eeType.CorElementTypeInfo;
            switch (corElementTypeInfo.Log2OfSize)
            {
                case 0:
                    return Unsafe.As<byte, byte>(ref pThisValue) == Unsafe.As<byte, byte>(ref pOtherValue);
                case 1:
                    return Unsafe.As<byte, ushort>(ref pThisValue) == Unsafe.As<byte, ushort>(ref pOtherValue);
                case 2:
                    return Unsafe.As<byte, uint>(ref pThisValue) == Unsafe.As<byte, uint>(ref pOtherValue);
                case 3:
                    return Unsafe.As<byte, ulong>(ref pThisValue) == Unsafe.As<byte, ulong>(ref pOtherValue);
                default:
                    Debug.Fail("Invalid primitive type");
                    return false;
            }
        }

        private CorElementType InternalGetCorElementType()
        {
            return this.EETypePtr.CorElementType;
        }

        [Intrinsic]
        public bool HasFlag(Enum flag)
        {
            if (flag == null)
                throw new ArgumentNullException(nameof(flag));

            if (!(this.EETypePtr == flag.EETypePtr))
                throw new ArgumentException(SR.Format(SR.Argument_EnumTypeDoesNotMatch, flag.GetType(), this.GetType()));

            ref byte pThisValue = ref this.GetRawData();
            ref byte pFlagValue = ref flag.GetRawData();

            switch (this.EETypePtr.CorElementTypeInfo.Log2OfSize)
            {
                case 0:
                    return (Unsafe.As<byte, byte>(ref pThisValue) & Unsafe.As<byte, byte>(ref pFlagValue)) == Unsafe.As<byte, byte>(ref pFlagValue);
                case 1:
                    return (Unsafe.As<byte, ushort>(ref pThisValue) & Unsafe.As<byte, ushort>(ref pFlagValue)) == Unsafe.As<byte, ushort>(ref pFlagValue);
                case 2:
                    return (Unsafe.As<byte, uint>(ref pThisValue) & Unsafe.As<byte, uint>(ref pFlagValue)) == Unsafe.As<byte, uint>(ref pFlagValue);
                case 3:
                    return (Unsafe.As<byte, ulong>(ref pThisValue) & Unsafe.As<byte, ulong>(ref pFlagValue)) == Unsafe.As<byte, ulong>(ref pFlagValue);
                default:
                    Debug.Fail("Invalid primitive type");
                    return false;
            }
        }



        //
        // Note: This works on both Enum's and underlying integer values.
        //
        //
        // This returns the underlying enum values as "ulong" regardless of the actual underlying type. Signed integral
        // types get sign-extended into the 64-bit value, unsigned types get zero-extended.
        //
        // The return value is "bool" if "value" is not an enum or an "integer type" as defined by the BCL Enum apis.
        //
        internal static bool TryGetUnboxedValueOfEnumOrInteger(object value, out ulong result)
        {
            EETypePtr eeType = value.EETypePtr;
            // For now, this check is required to flush out pointers.
            if (!eeType.IsDefType)
            {
                result = 0;
                return false;
            }
            EETypeElementType elementType = eeType.ElementType;

            ref byte pValue = ref value.GetRawData();

            switch (elementType)
            {
                case EETypeElementType.Boolean:
                    result = Unsafe.As<byte, bool>(ref pValue) ? 1UL : 0UL;
                    return true;

                case EETypeElementType.Char:
                    result = (ulong)(long)Unsafe.As<byte, char>(ref pValue);
                    return true;

                case EETypeElementType.SByte:
                    result = (ulong)(long)Unsafe.As<byte, sbyte>(ref pValue);
                    return true;

                case EETypeElementType.Byte:
                    result = (ulong)(long)Unsafe.As<byte, byte>(ref pValue);
                    return true;

                case EETypeElementType.Int16:
                    result = (ulong)(long)Unsafe.As<byte, short>(ref pValue);
                    return true;

                case EETypeElementType.UInt16:
                    result = (ulong)(long)Unsafe.As<byte, ushort>(ref pValue);
                    return true;

                case EETypeElementType.Int32:
                    result = (ulong)(long)Unsafe.As<byte, int>(ref pValue);
                    return true;

                case EETypeElementType.UInt32:
                    result = (ulong)(long)Unsafe.As<byte, uint>(ref pValue);
                    return true;

                case EETypeElementType.Int64:
                    result = (ulong)(long)Unsafe.As<byte, long>(ref pValue);
                    return true;

                case EETypeElementType.UInt64:
                    result = (ulong)(long)Unsafe.As<byte, ulong>(ref pValue);
                    return true;

                default:
                    result = 0;
                    return false;
            }
        }

        internal static Type InternalGetUnderlyingType(RuntimeType enumType)
        {
            Debug.Assert(enumType is RuntimeType);
            Debug.Assert(enumType.IsEnum);

            return GetEnumInfo(enumType).UnderlyingType;
        }

        public static TEnum[] GetValues<TEnum>() where TEnum : struct, Enum
        {
            Array values = GetEnumInfo(typeof(TEnum)).ValuesAsUnderlyingType;
            TEnum[] result = new TEnum[values.Length];
            Array.Copy(values, result, values.Length);
            return result;
        }

        //
        // Checks if value.GetType() matches enumType exactly.
        //
        internal static bool ValueTypeMatchesEnumType(Type enumType, object value)
        {
            EETypePtr enumEEType;
            if (!enumType.TryGetEEType(out enumEEType))
                return false;
            if (!(enumEEType == value.EETypePtr))
                return false;
            return true;
        }

        [Conditional("BIGENDIAN")]
        private static unsafe void AdjustForEndianness(ref byte* pValue, EETypePtr enumEEType)
        {
            // On Debug builds, include the big-endian code to help deter bitrot (the "Conditional("BIGENDIAN")" will prevent it from executing on little-endian).
            // On Release builds, exclude code to deter IL bloat and toolchain work.
#if BIGENDIAN || DEBUG
            EETypeElementType elementType = enumEEType.ElementType;
            switch (elementType)
            {
                case EETypeElementType.SByte:
                case EETypeElementType.Byte:
                    pValue += sizeof(long) - sizeof(byte);
                    break;

                case EETypeElementType.Int16:
                case EETypeElementType.UInt16:
                    pValue += sizeof(long) - sizeof(short);
                    break;

                case EETypeElementType.Int32:
                case EETypeElementType.UInt32:
                    pValue += sizeof(long) - sizeof(int);
                    break;

                case EETypeElementType.Int64:
                case EETypeElementType.UInt64:
                    break;

                default:
                    throw new NotSupportedException();
            }
#endif //BIGENDIAN || DEBUG
        }

        #region ToObject

        internal static unsafe object ToObject(EETypePtr enumEEType, long value)
        {
            Debug.Assert(enumEEType.IsEnum);

            byte* pValue = (byte*)&value;
            AdjustForEndianness(ref pValue, enumEEType);
            return RuntimeImports.RhBox(enumEEType, ref *pValue);
        }
        #endregion
    }
}
