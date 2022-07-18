// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection.Metadata;

namespace System.Reflection.Emit.Experimental
{
    // Summary:
    //     Defines and represents a field. This class cannot be inherited.
    public sealed class FieldBuilder : FieldInfo
    {
        #region Private Data Members
        internal TypeBuilder _typeBuilder;
        internal ModuleBuilder _moduleBuilder;
        private string _fieldName;
        private FieldAttributes _attributes;
        private Type _fieldType;
        private BlobBuilder _fieldSignature;
        #endregion

        internal EntityHandle _fieldTok;

        #region Constructor
        internal FieldBuilder(TypeBuilder typeBuilder, string fieldName, Type type,
#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
            Type[]? requiredCustomModifiers, FieldAttributes attributes, EntityHandle fieldTok)
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
        {
            ArgumentException.ThrowIfNullOrEmpty(fieldName);

            if (fieldName[0] == '\0')
            {
                throw new ArgumentException("Illegal field name: " + nameof(fieldName));
            }

            ArgumentNullException.ThrowIfNull(type);

            if (type == typeof(void))
            {
                throw new ArgumentException("Field cannot be type void");
            }

            _fieldName = fieldName;
            _typeBuilder = typeBuilder;
            _moduleBuilder = typeBuilder.Module;
            _fieldType = type;
            _attributes = attributes & ~FieldAttributes.ReservedMask;
            _fieldSignature = SignatureHelper.FieldSignatureEncoder(FieldType, _moduleBuilder);
            _fieldTok = fieldTok;
        }

        public BlobBuilder FieldSignature
        {
            get { return _fieldSignature; }
        }
        #endregion
        //
        // Summary:
        //     Indicates the reference to the System.Type object from which this object was
        //     obtained. This property is read-only.
        //
        // Returns:
        //     A reference to the System.Type object from which this instance was obtained.
        public override Type? ReflectedType
        {
            get { return _typeBuilder; }
        }

        // Summary:
        //     Gets the module in which the type that contains this field is being defined.
        //
        // Returns:
        //     A System.Reflection.Module that represents the dynamic module in which this field
        //     is being defined.
        public override Module Module => _typeBuilder.Module;

        // Summary:
        //     Gets a token that identifies the current dynamic module in metadata.
        //
        // Returns:
        //     An integer token that identifies the current module in metadata.
       // public override int MetadataToken => _fieldTok.;

        //
        // Summary:
        //     Indicates the System.Type object that represents the type of this field. This
        //     property is read-only.
        //
        // Returns:
        //     The System.Type object that represents the type of this field.
        public override Type FieldType => _fieldType;

        // Summary:
        //     Indicates the internal metadata handle for this field. This property is read-only.
        //
        // Returns:
        //     The internal metadata handle for this field.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not supported.
        public override RuntimeFieldHandle FieldHandle
            => throw new NotSupportedException("Not supported for dynamic modules");

        // Summary:
        //     Indicates a reference to the System.Type object for the type that declares this
        //     field. This property is read-only.
        //
        // Returns:
        //     A reference to the System.Type object for the type that declares this field.
        public override Type? DeclaringType
        {
            get { return _typeBuilder; }
        }

        // Summary:
        //     Indicates the attributes of this field. This property is read-only.
        //
        // Returns:
        //     The attributes of this field.
        public override FieldAttributes Attributes
            => _attributes;

        // Summary:
        //     Indicates the name of this field. This property is read-only.
        //
        // Returns:
        //     A System.String containing the name of this field.
        public override string Name
            => _fieldName;

        // Summary:
        //     Returns all the custom attributes defined for this field.
        //
        // Parameters:
        //   inherit:
        //     Controls inheritance of custom attributes from base classes.
        //
        // Returns:
        //     An array of type System.Object representing all the custom attributes of the
        //     constructor represented by this System.Reflection.Emit.FieldBuilder instance.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not supported.
        public override object[] GetCustomAttributes(bool inherit)
            => throw new NotSupportedException("Not supported for dynamic modules");

        // Summary:
        //     Returns all the custom attributes defined for this field identified by the given
        //     type.
        //
        // Parameters:
        //   attributeType:
        //     The custom attribute type.
        //
        //   inherit:
        //     Controls inheritance of custom attributes from base classes.
        //
        // Returns:
        //     An array of type System.Object representing all the custom attributes of the
        //     constructor represented by this System.Reflection.Emit.FieldBuilder instance.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not supported.
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            => throw new NotSupportedException("Not supported for dynamic modules");

        // Summary:
        //     Retrieves the value of the field supported by the given object.
        //
        // Parameters:
        //   obj:
        //     The object on which to access the field.
        //
        // Returns:
        //     An System.Object containing the value of the field reflected by this instance.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not supported.
        public override object? GetValue(object? obj)
            => throw new NotSupportedException("Not supported for dynamic modules");

        // Summary:
        //     Indicates whether an attribute having the specified type is defined on a field.
        //
        // Parameters:
        //   attributeType:
        //     The type of the attribute.
        //
        //   inherit:
        //     Controls inheritance of custom attributes from base classes.
        //
        // Returns:
        //     true if one or more instance of attributeType is defined on this field; otherwise,
        //     false.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not currently supported. Retrieve the field using System.Type.GetField(System.String,System.Reflection.BindingFlags)
        //     and call System.Reflection.MemberInfo.IsDefined(System.Type,System.Boolean) on
        //     the returned System.Reflection.FieldInfo.
        public override bool IsDefined(Type attributeType, bool inherit)
            => throw new NotSupportedException("Not supported for dynamic modules");

        // Summary:
        //     Sets the default value of this field.
        //
        // Parameters:
        //   defaultValue:
        //     The new default value for this field.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     The containing type has been created using System.Reflection.Emit.TypeBuilder.CreateType.
        //
        //   T:System.ArgumentException:
        //     The field is not one of the supported types. -or- The type of defaultValue does
        //     not match the type of the field. -or- The field is of type System.Object or other
        //     reference type, defaultValue is not null, and the value cannot be assigned to
        //     the reference type.
        public void SetConstant(object? defaultValue)
            => throw new NotImplementedException();

        // Summary:
        //     Sets a custom attribute using a custom attribute builder.
        //
        // Parameters:
        //   customBuilder:
        //     An instance of a helper class to define the custom attribute.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     con is null.
        //
        //   T:System.InvalidOperationException:
        //     The parent type of this field is complete.
        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
            => throw new NotImplementedException();

        // Summary:
        //     Sets a custom attribute using a specified custom attribute blob.
        //
        // Parameters:
        //   con:
        //     The constructor for the custom attribute.
        //
        //   binaryAttribute:
        //     A byte blob representing the attributes.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     con or binaryAttribute is null.
        //
        //   T:System.InvalidOperationException:
        //     The parent type of this field is complete.
        public void SetCustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
            => throw new NotImplementedException();

        // Summary:
        //     Specifies the field layout.
        //
        // Parameters:
        //   iOffset:
        //     The offset of the field within the type containing this field.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     The containing type has been created using System.Reflection.Emit.TypeBuilder.CreateType.
        //
        //   T:System.ArgumentException:
        //     iOffset is less than zero.
        public void SetOffset(int iOffset)
            => throw new NotImplementedException();

        // Summary:
        //     Sets the value of the field supported by the given object.
        //
        // Parameters:
        //   obj:
        //     The object on which to access the field.
        //
        //   val:
        //     The value to assign to the field.
        //
        //   invokeAttr:
        //     A member of IBinder that specifies the type of binding that is desired (for example,
        //     IBinder.CreateInstance, IBinder.ExactBinding).
        //
        //   binder:
        //     A set of properties and enabling for binding, coercion of argument types, and
        //     invocation of members using reflection. If binder is null, then IBinder.DefaultBinding
        //     is used.
        //
        //   culture:
        //     The software preferences of a particular culture.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not supported.
        public override void SetValue(object? obj, object? val, BindingFlags invokeAttr, Binder? binder, CultureInfo? culture)
        {
            throw new NotSupportedException("Not supported for dynamic modules");
        }
    }
}
