// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace System.Reflection.Emit.Experimental
{
    public class CustomAttributeBuilder
    {
        /// <summary>
        /// Stores the constructor of the custom attribute's type. Custom attribute's are identified by their constructor in ECMA.
        /// </summary>
        private ConstructorInfo _constructorInfo;
        private object?[] _constructorArgs;
        internal byte[] _blob;

        public ConstructorInfo Constructor { get => _constructorInfo;  }

        // public constructor to form the custom attribute with constructor and constructor
        // parameters.
        public CustomAttributeBuilder(ConstructorInfo con, object?[] constructorArgs) :
            this(con, constructorArgs, Array.Empty<PropertyInfo>(), Array.Empty<object>(), Array.Empty<FieldInfo>(), Array.Empty<object>())
        {
        }

        // public constructor to form the custom attribute with constructor, constructor
        // parameters and named properties.
        public CustomAttributeBuilder(ConstructorInfo con, object?[] constructorArgs, PropertyInfo[] namedProperties, object?[] propertyValues) :
            this(con, constructorArgs, namedProperties, propertyValues, Array.Empty<FieldInfo>(), Array.Empty<object>())
        {
        }

        // public constructor to form the custom attribute with constructor and constructor
        // parameters.
        public CustomAttributeBuilder(ConstructorInfo con, object?[] constructorArgs, FieldInfo[] namedFields, object?[] fieldValues) :
            this(con, constructorArgs, Array.Empty<PropertyInfo>(), Array.Empty<object>(), namedFields, fieldValues)
        {
        }

        // public constructor to form the custom attribute with constructor and constructor
        // parameters.
        public CustomAttributeBuilder(ConstructorInfo con, object?[] constructorArgs, PropertyInfo[] namedProperties, object?[] propertyValues, FieldInfo[] namedFields, object?[] fieldValues)
        {
            ArgumentNullException.ThrowIfNull(con);
            ArgumentNullException.ThrowIfNull(constructorArgs);
            ArgumentNullException.ThrowIfNull(namedProperties);
            ArgumentNullException.ThrowIfNull(propertyValues);
            ArgumentNullException.ThrowIfNull(namedFields);
            ArgumentNullException.ThrowIfNull(fieldValues);

#pragma warning disable CA2208 // Instantiate argument exceptions correctly, combination of arguments used
            if (namedProperties.Length != propertyValues.Length)
            {
                throw new ArgumentException($"{nameof(namedProperties)} and {nameof(propertyValues)} should have the same length.");
            }
            if (namedFields.Length != fieldValues.Length)
            {
                throw new ArgumentException($"{nameof(namedFields)} and {nameof(fieldValues)} should have the same length.");
            }
#pragma warning restore CA2208

            if ((con.Attributes & MethodAttributes.Static) == MethodAttributes.Static ||
                (con.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private)
                throw new ArgumentException("The passed-in constructor is either static or private");

            if ((con.CallingConvention & CallingConventions.Standard) != CallingConventions.Standard)
                throw new ArgumentException("Non standard calling convention for constructor.");

            // Cache information used elsewhere.
            _constructorInfo = con;
            _constructorArgs = new object?[constructorArgs.Length];
            Array.Copy(constructorArgs, _constructorArgs, constructorArgs.Length);

            Type[] paramTypes;
            int i;
            ParameterInfo[] parameters = con.GetParameters();
            // Get the types of the constructor's formal parameters.
            paramTypes = Array.ConvertAll(parameters, parameter => parameter.ParameterType);

            // Since we're guaranteed a non-var calling convention, the number of arguments must equal the number of parameters.
            if (paramTypes.Length != constructorArgs.Length)
                throw new ArgumentException("Bad Parameter Count For Constructor");

            // Verify that the constructor has a valid signature (custom attributes only support a subset of our type system).
            for (i = 0; i < paramTypes.Length; i++)
                if (!ValidateType(paramTypes[i]))
                    throw new ArgumentException("Bad Type In Custom Attribute: " + paramTypes[i]);

            // Now verify that the types of the actual parameters are compatible with the types of the formal parameters.
            for (i = 0; i < paramTypes.Length; i++)
            {
                object? constructorArg = constructorArgs[i];
                if (constructorArg == null)
                {
                    if (paramTypes[i].IsValueType)
                    {
                        throw new ArgumentNullException($"{nameof(constructorArgs)}[{i}]");
                    }
                    continue;
                }
                VerifyTypeAndPassedObjectType(paramTypes[i], constructorArg.GetType(), $"{nameof(constructorArgs)}[{i}]");
            }

            // Allocate a memory stream to represent the CA blob in the metadata and a binary writer to help format it.
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            // Write the blob protocol version (currently 1).
            writer.Write((ushort)1);

            // Now emit the constructor argument values (no need for types, they're inferred from the constructor signature).
            for (i = 0; i < constructorArgs.Length; i++)
                EmitValue(writer, paramTypes[i], constructorArgs[i]);

            // Next a short with the count of properties and fields.
            writer.Write((ushort)(namedProperties.Length + namedFields.Length));

            // Emit all the property sets.
            for (i = 0; i < namedProperties.Length; i++)
            {
                // Validate the property.
                PropertyInfo property = namedProperties[i];
                if (property == null)
                    throw new ArgumentNullException("namedProperties[" + i + "]");

                // Allow null for non-primitive types only.
                Type propType = property.PropertyType;
                object? propertyValue = propertyValues[i];
                if (propertyValue == null && propType.IsValueType)
                    throw new ArgumentNullException("propertyValues[" + i + "]");

                // Validate property type.
                if (!ValidateType(propType))
                    throw new ArgumentException("Bad Type In Custom Attribute");

                // Property has to be writable.
                if (!property.CanWrite)
                    throw new ArgumentException("Not A Writable Property");


                // Make sure the property's type can take the given value.
                // Note that there will be no coercion.
                if (propertyValue != null)
                {
                    VerifyTypeAndPassedObjectType(propType, propertyValue.GetType(), $"{nameof(propertyValues)}[{i}]");
                }

                // First a byte indicating that this is a property.
                writer.Write((byte)CustomAttributeEncoding.Property);

                // Emit the property type, name and value.
                EmitType(writer, propType);
                EmitString(writer, namedProperties[i].Name);
                EmitValue(writer, propType, propertyValue);
            }

            // Emit all the field sets.
            for (i = 0; i < namedFields.Length; i++)
            {
                // Validate the field.
                FieldInfo namedField = namedFields[i];
                if (namedField == null)
                    throw new ArgumentNullException("namedFields[" + i + "]");

                // Allow null for non-primitive types only.
                Type fldType = namedField.FieldType;
                object? fieldValue = fieldValues[i];
                if (fieldValue == null && fldType.IsValueType)
                    throw new ArgumentNullException("fieldValues[" + i + "]");

                // Validate field type.
                if (!ValidateType(fldType))
                    throw new ArgumentException("Bad Type In Custom Attribute");


                // Make sure the field's type can take the given value.
                // Note that there will be no coercion.
                if (fieldValue != null)
                {
                    VerifyTypeAndPassedObjectType(fldType, fieldValue.GetType(), $"{nameof(fieldValues)}[{i}]");
                }

                // First a byte indicating that this is a field.
                writer.Write((byte)CustomAttributeEncoding.Field);

                // Emit the field type, name and value.
                EmitType(writer, fldType);
                EmitString(writer, namedField.Name);
                EmitValue(writer, fldType, fieldValue);
            }

            // Create the blob array.
            _blob = ((MemoryStream)writer.BaseStream).ToArray();
        }

        // Check that a type is suitable for use in a custom attribute.
        private bool ValidateType(Type t)
        {
            if (t.IsPrimitive)
            {
                return t != typeof(IntPtr) && t != typeof(UIntPtr);
            }
            if (t == typeof(string) || t == typeof(Type))
            {
                return true;
            }
            if (t.IsEnum)
            {
                switch (Type.GetTypeCode(Enum.GetUnderlyingType(t)))
                {
                    case TypeCode.SByte:
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                        return true;
                    default:
                        return false;
                }
            }
            if (t.IsArray)
            {
                return t.GetArrayRank() == 1 && ValidateType(t.GetElementType()!);
            }
            return t == typeof(object);
        }

        private static void VerifyTypeAndPassedObjectType(Type type, Type passedType, string paramName)
        {
            if (type != typeof(object) && Type.GetTypeCode(passedType) != Type.GetTypeCode(type))
            {
                throw new ArgumentException("Constant Doesnt Match");
            }
            if (passedType == typeof(IntPtr) || passedType == typeof(UIntPtr))
            {
                throw new ArgumentException("Bad arugment for custom attribute builder");
            }
        }

        private static void EmitType(BinaryWriter writer, Type type)
        {
            if (type.IsPrimitive)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.SByte:
                        writer.Write((byte)CustomAttributeEncoding.SByte);
                        break;
                    case TypeCode.Byte:
                        writer.Write((byte)CustomAttributeEncoding.Byte);
                        break;
                    case TypeCode.Char:
                        writer.Write((byte)CustomAttributeEncoding.Char);
                        break;
                    case TypeCode.Boolean:
                        writer.Write((byte)CustomAttributeEncoding.Boolean);
                        break;
                    case TypeCode.Int16:
                        writer.Write((byte)CustomAttributeEncoding.Int16);
                        break;
                    case TypeCode.UInt16:
                        writer.Write((byte)CustomAttributeEncoding.UInt16);
                        break;
                    case TypeCode.Int32:
                        writer.Write((byte)CustomAttributeEncoding.Int32);
                        break;
                    case TypeCode.UInt32:
                        writer.Write((byte)CustomAttributeEncoding.UInt32);
                        break;
                    case TypeCode.Int64:
                        writer.Write((byte)CustomAttributeEncoding.Int64);
                        break;
                    case TypeCode.UInt64:
                        writer.Write((byte)CustomAttributeEncoding.UInt64);
                        break;
                    case TypeCode.Single:
                        writer.Write((byte)CustomAttributeEncoding.Float);
                        break;
                    case TypeCode.Double:
                        writer.Write((byte)CustomAttributeEncoding.Double);
                        break;
                    default:
                        Debug.Fail("Invalid primitive type");
                        break;
                }
            }
            else if (type.IsEnum)
            {
                writer.Write((byte)CustomAttributeEncoding.Enum);
                EmitString(writer, type.AssemblyQualifiedName!);
            }
            else if (type == typeof(string))
            {
                writer.Write((byte)CustomAttributeEncoding.String);
            }
            else if (type == typeof(Type))
            {
                writer.Write((byte)CustomAttributeEncoding.Type);
            }
            else if (type.IsArray)
            {
                writer.Write((byte)CustomAttributeEncoding.Array);
                EmitType(writer, type.GetElementType()!);
            }
            else
            {
                // Tagged object case.
                writer.Write((byte)CustomAttributeEncoding.Object);
            }
        }

        private static void EmitString(BinaryWriter writer, string str)
        {
            // Strings are emitted with a length prefix in a compressed format (1, 2 or 4 bytes) as used internally by metadata.
            byte[] utf8Str = Encoding.UTF8.GetBytes(str);
            uint length = (uint)utf8Str.Length;
            if (length <= 0x7f)
            {
                writer.Write((byte)length);
            }
            else if (length <= 0x3fff)
            {
                writer.Write(BinaryPrimitives.ReverseEndianness((short)(length | 0x80_00)));
            }
            else
            {
                writer.Write(BinaryPrimitives.ReverseEndianness(length | 0xC0_00_00_00));
            }
            writer.Write(utf8Str);
        }

        private static void EmitValue(BinaryWriter writer, Type type, object? value)
        {
            if (type.IsEnum)
            {
                switch (Type.GetTypeCode(Enum.GetUnderlyingType(type)))
                {
                    case TypeCode.SByte:
                        writer.Write((sbyte)value!);
                        break;
                    case TypeCode.Byte:
                        writer.Write((byte)value!);
                        break;
                    case TypeCode.Int16:
                        writer.Write((short)value!);
                        break;
                    case TypeCode.UInt16:
                        writer.Write((ushort)value!);
                        break;
                    case TypeCode.Int32:
                        writer.Write((int)value!);
                        break;
                    case TypeCode.UInt32:
                        writer.Write((uint)value!);
                        break;
                    case TypeCode.Int64:
                        writer.Write((long)value!);
                        break;
                    case TypeCode.UInt64:
                        writer.Write((ulong)value!);
                        break;
                    default:
                        Debug.Fail("Invalid enum base type");
                        break;
                }
            }
            else if (type == typeof(string))
            {
                if (value == null)
                    writer.Write((byte)0xff);
                else
                    EmitString(writer, (string)value);
            }
            else if (type == typeof(Type))
            {
                if (value == null)
                    writer.Write((byte)0xff);
                else
                {
                    string? typeName = TypeNameBuilder.ToString((Type)value, TypeNameBuilder.Format.AssemblyQualifiedName);
                    if (typeName == null)
                        throw new ArgumentException("Invalid Type For CA");
                    EmitString(writer, typeName);
                }
            }
            else if (type.IsArray)
            {
                if (value == null)
                    writer.Write((uint)0xffffffff);
                else
                {
                    Array a = (Array)value;
                    Type et = type.GetElementType()!;
                    writer.Write(a.Length);
                    for (int i = 0; i < a.Length; i++)
                        EmitValue(writer, et, a.GetValue(i));
                }
            }
            else if (type.IsPrimitive)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.SByte:
                        writer.Write((sbyte)value!);
                        break;
                    case TypeCode.Byte:
                        writer.Write((byte)value!);
                        break;
                    case TypeCode.Char:
                        writer.Write(Convert.ToUInt16((char)value!));
                        break;
                    case TypeCode.Boolean:
                        writer.Write((byte)((bool)value! ? 1 : 0));
                        break;
                    case TypeCode.Int16:
                        writer.Write((short)value!);
                        break;
                    case TypeCode.UInt16:
                        writer.Write((ushort)value!);
                        break;
                    case TypeCode.Int32:
                        writer.Write((int)value!);
                        break;
                    case TypeCode.UInt32:
                        writer.Write((uint)value!);
                        break;
                    case TypeCode.Int64:
                        writer.Write((long)value!);
                        break;
                    case TypeCode.UInt64:
                        writer.Write((ulong)value!);
                        break;
                    case TypeCode.Single:
                        writer.Write((float)value!);
                        break;
                    case TypeCode.Double:
                        writer.Write((double)value!);
                        break;
                    default:
                        Debug.Fail("Invalid primitive type");
                        break;
                }
            }
            else if (type == typeof(object))
            {
                // Tagged object case. Type instances aren't actually Type, they're some subclass (such as RuntimeType or
                // TypeBuilder), so we need to canonicalize this case back to Type. If we have a null value we follow the convention
                // used by C# and emit a null typed as a string (it doesn't really matter what type we pick as long as it's a
                // reference type).
                Type ot = value == null ? typeof(string) : value is Type ? typeof(Type) : value.GetType();

                // value cannot be a "System.Object" object.
                // If we allow this we will get into an infinite recursion
                if (ot == typeof(object))
                    throw new ArgumentException("Bad Parameter Type For CAB");

                EmitType(writer, ot);
                EmitValue(writer, ot, value);
            }
            else
            {
                string typename = "null";

                if (value != null)
                    typename = value.GetType().ToString();

                throw new ArgumentException("Bad Parameter Type For CAB");
            }
        }

        internal enum CustomAttributeEncoding : int
        {
            Undefined = 0,
            Boolean = CorElementType.ELEMENT_TYPE_BOOLEAN,
            Char = CorElementType.ELEMENT_TYPE_CHAR,
            SByte = CorElementType.ELEMENT_TYPE_I1,
            Byte = CorElementType.ELEMENT_TYPE_U1,
            Int16 = CorElementType.ELEMENT_TYPE_I2,
            UInt16 = CorElementType.ELEMENT_TYPE_U2,
            Int32 = CorElementType.ELEMENT_TYPE_I4,
            UInt32 = CorElementType.ELEMENT_TYPE_U4,
            Int64 = CorElementType.ELEMENT_TYPE_I8,
            UInt64 = CorElementType.ELEMENT_TYPE_U8,
            Float = CorElementType.ELEMENT_TYPE_R4,
            Double = CorElementType.ELEMENT_TYPE_R8,
            String = CorElementType.ELEMENT_TYPE_STRING,
            Array = CorElementType.ELEMENT_TYPE_SZARRAY,
            Type = 0x50,
            Object = 0x51,
            Field = 0x53,
            Property = 0x54,
            Enum = 0x55
        }

        public enum CorElementType : byte
        {
            Invalid = 0x0,

            ELEMENT_TYPE_VOID = 0x1,
            ELEMENT_TYPE_BOOLEAN = 0x2,
            ELEMENT_TYPE_CHAR = 0x3,
            ELEMENT_TYPE_I1 = 0x4, // SByte
            ELEMENT_TYPE_U1 = 0x5, // Byte
            ELEMENT_TYPE_I2 = 0x6, // Int16
            ELEMENT_TYPE_U2 = 0x7, // UInt16
            ELEMENT_TYPE_I4 = 0x8, // Int32
            ELEMENT_TYPE_U4 = 0x9, // UInt32
            ELEMENT_TYPE_I8 = 0xA, // Int64
            ELEMENT_TYPE_U8 = 0xB, // UInt64
            ELEMENT_TYPE_R4 = 0xC, // Single
            ELEMENT_TYPE_R8 = 0xD, // Double
            ELEMENT_TYPE_STRING = 0xE,

            // every type above PTR will be simple type
            ELEMENT_TYPE_PTR = 0xF,      // PTR <type>
            ELEMENT_TYPE_BYREF = 0x10,     // BYREF <type>

            // Please use ELEMENT_TYPE_VALUETYPE. ELEMENT_TYPE_VALUECLASS is deprecated.
            ELEMENT_TYPE_VALUETYPE = 0x11,     // VALUETYPE <class Token>
            ELEMENT_TYPE_CLASS = 0x12,     // CLASS <class Token>
            ELEMENT_TYPE_VAR = 0x13,     // a class type variable VAR <U1>
            ELEMENT_TYPE_ARRAY = 0x14,     // MDARRAY <type> <rank> <bcount> <bound1> ... <lbcount> <lb1> ...
            ELEMENT_TYPE_GENERICINST = 0x15,     // GENERICINST <generic type> <argCnt> <arg1> ... <argn>
            ELEMENT_TYPE_TYPEDBYREF = 0x16,     // TYPEDREF  (it takes no args) a typed reference to some other type

            ELEMENT_TYPE_I = 0x18,     // native integer size
            ELEMENT_TYPE_U = 0x19,     // native unsigned integer size

            ELEMENT_TYPE_FNPTR = 0x1B,     // FNPTR <complete sig for the function including calling convention>
            ELEMENT_TYPE_OBJECT = 0x1C,     // Shortcut for System.Object
            ELEMENT_TYPE_SZARRAY = 0x1D,     // Shortcut for single dimension zero lower bound array
                                             // SZARRAY <type>
            ELEMENT_TYPE_MVAR = 0x1E,     // a method type variable MVAR <U1>

            // This is only for binding
            ELEMENT_TYPE_CMOD_REQD = 0x1F,     // required C modifier : E_T_CMOD_REQD <mdTypeRef/mdTypeDef>
            ELEMENT_TYPE_CMOD_OPT = 0x20,     // optional C modifier : E_T_CMOD_OPT <mdTypeRef/mdTypeDef>

            ELEMENT_TYPE_HANDLE = 0x40,
            ELEMENT_TYPE_SENTINEL = 0x41, // sentinel for varargs
            ELEMENT_TYPE_PINNED = 0x45,
        }
    }
}
