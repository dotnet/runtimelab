// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// Represents a strongly-typed property to prevent boxing and to create a direct delegate to the getter\setter.
    /// </summary>
    /// <typeparamref name="T"/> is the <see cref="JsonConverter{T}.TypeToConvert"/> for either the property's converter,
    /// or a type's converter, if the current instance is a <see cref="JsonTypeInfo.PropertyInfoForTypeInfo"/>.
    public sealed class JsonPropertyInfo<T> : JsonPropertyInfo
    {
        /// <summary>
        /// todo
        /// </summary>
        public Func<object, T>? Get { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        public Action<object, T>? Set { get; set; }

        /// <summary>
        /// todo
        /// </summary>
        public JsonConverter<T> Converter { get; internal set; } = null!;

        /// <summary>
        /// TODO
        /// </summary>
        /// <returns></returns>
        public static JsonPropertyInfo<T> Create() => new();

        internal override void Initialize(Type parentClassType,
            Type declaredPropertyType,
            Type? runtimePropertyType,
            ClassType runtimeClassType,
            MemberInfo? memberInfo,
            JsonConverter converter,
            JsonIgnoreCondition? ignoreCondition,
            JsonNumberHandling? parentTypeNumberHandling,
            JsonSerializerOptions options)
        {
            base.Initialize(
                parentClassType,
                declaredPropertyType,
                runtimePropertyType,
                runtimeClassType,
                memberInfo,
                converter,
                ignoreCondition,
                parentTypeNumberHandling,
                options);

            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    {
                        bool useNonPublicAccessors = GetAttribute<JsonIncludeAttribute>(propertyInfo) != null;

                        MethodInfo? getMethod = propertyInfo.GetMethod;
                        if (getMethod != null && (getMethod.IsPublic || useNonPublicAccessors))
                        {
                            HasGetter = true;
                            Get = Options.MemberAccessorStrategy.CreatePropertyGetter<T>(propertyInfo);
                        }

                        MethodInfo? setMethod = propertyInfo.SetMethod;
                        if (setMethod != null && (setMethod.IsPublic || useNonPublicAccessors))
                        {
                            HasSetter = true;
                            Set = Options.MemberAccessorStrategy.CreatePropertySetter<T>(propertyInfo);
                        }
                    }
                    break;
                case FieldInfo fieldInfo:
                    {
                        Debug.Assert(fieldInfo.IsPublic);

                        HasGetter = true;
                        Get = Options.MemberAccessorStrategy.CreateFieldGetter<T>(fieldInfo);

                        if (!fieldInfo.IsInitOnly)
                        {
                            HasSetter = true;
                            Set = Options.MemberAccessorStrategy.CreateFieldSetter<T>(fieldInfo);
                        }
                    }
                    break;

                default:
                    {
                        IsForTypeInfo = true;
                        HasGetter = true;
                        HasSetter = true;
                    }
                    break;
            }

            _converterIsExternalAndPolymorphic = !converter.IsInternalConverter && DeclaredPropertyType != converter.TypeToConvert;
            PropertyTypeCanBeNull = DeclaredPropertyType.CanBeNull();
            _propertyTypeEqualsTypeToConvert = typeof(T) == DeclaredPropertyType;

            GetPolicies(ignoreCondition, parentTypeNumberHandling);
        }

        /// <summary>
        /// TODO
        /// </summary>
        public void CompleteInitialization()
        {
            if (IgnoreCondition == JsonIgnoreCondition.Always)
            {
                // TODO: test [JsonIgnore] + [JsonPropertyName(name)] for both code gen and non-code-gen paths.
                IsIgnored = true;
                Debug.Assert(!ShouldSerialize);
                Debug.Assert(!ShouldDeserialize);
            }
            else
            {
                // TODO:
                _converterIsExternalAndPolymorphic = !Converter.IsInternalConverter && DeclaredPropertyType != Converter.TypeToConvert;
                PropertyTypeCanBeNull = DeclaredPropertyType.CanBeNull();
                _propertyTypeEqualsTypeToConvert = typeof(T) == DeclaredPropertyType;
                HasGetter = Get != null;
                HasSetter = Set != null;
                ClassType = Converter!.ClassType;
                RuntimePropertyType = DeclaredPropertyType;
                DetermineIgnoreCondition(IgnoreCondition);
                DetermineNumberHandlingForProperty(NumberHandling);
                DetermineSerializationCapabilities(IgnoreCondition, MemberType);

                if (NameAsString == null)
                {
                    throw new InvalidOperationException("TODO: 'NameAsString' cannot be null.");
                }

                // TODO: separate this for more linker trimming?
                NameAsUtf8Bytes ??= Encoding.UTF8.GetBytes(NameAsString);
                EscapedNameSection ??= JsonHelpers.GetEscapedPropertyNameSection(NameAsUtf8Bytes, Options.Encoder);
            }
        }

        /// <summary>
        /// todo
        /// </summary>
        public override JsonConverter ConverterBase
        {
            get
            {
                return Converter;
            }
            set
            {
                Debug.Assert(value is JsonConverter<T>);
                Converter = (JsonConverter<T>)value;
            }
        }

        internal override object? GetValueAsObject(object obj)
        {
            if (IsForTypeInfo)
            {
                return obj;
            }

            Debug.Assert(HasGetter);
            return Get!(obj);
        }

        internal override bool GetMemberAndWriteJson(object obj, ref WriteStack state, Utf8JsonWriter writer)
        {
            T value = Get!(obj);

            if (Options.ReferenceHandlingStrategy == ReferenceHandlingStrategy.IgnoreCycles &&
                !Converter.IsValueType && value != null &&
                state.ReferenceResolver.ContainsReferenceForCycleDetection(value))
            {
                // If a reference cycle is detected, treat value as null.
                value = default!;
                Debug.Assert(value == null);
            }

            if (IgnoreDefaultValuesOnWrite)
            {
                // If value is null, it is a reference type or nullable<T>.
                if (value == null)
                {
                    return true;
                }

                if (!PropertyTypeCanBeNull)
                {
                    if (_propertyTypeEqualsTypeToConvert)
                    {
                        // The converter and property types are the same, so we can use T for EqualityComparer<>.
                        if (EqualityComparer<T>.Default.Equals(default, value))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        Debug.Assert(RuntimeTypeInfo.Type == DeclaredPropertyType);

                        // Use a late-bound call to EqualityComparer<DeclaredPropertyType>.
                        if (RuntimeTypeInfo.GenericMethods.IsDefaultValue(value))
                        {
                            return true;
                        }
                    }
                }
            }

            if (value == null)
            {
                Debug.Assert(PropertyTypeCanBeNull);

                if (Converter.HandleNullOnWrite)
                {
                    // No object, collection, or re-entrancy converter handles null.
                    Debug.Assert(Converter.ClassType == ClassType.Value);

                    if (state.Current.PropertyState < StackFramePropertyState.Name)
                    {
                        state.Current.PropertyState = StackFramePropertyState.Name;
                        writer.WritePropertyNameSection(EscapedNameSection);
                    }

                    int originalDepth = writer.CurrentDepth;
                    Converter.Write(writer, value, Options);
                    if (originalDepth != writer.CurrentDepth)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterWrite(Converter);
                    }
                }
                else
                {
                    writer.WriteNullSection(EscapedNameSection);
                }

                return true;
            }
            else
            {
                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    state.Current.PropertyState = StackFramePropertyState.Name;
                    writer.WritePropertyNameSection(EscapedNameSection);
                }

                return Converter.TryWrite(writer, value, Options, ref state);
            }
        }

        internal override bool GetMemberAndWriteJsonExtensionData(object obj, ref WriteStack state, Utf8JsonWriter writer)
        {
            bool success;
            T value = Get!(obj);

            if (value == null)
            {
                success = true;
            }
            else
            {
                success = Converter.TryWriteDataExtensionProperty(writer, value, Options, ref state);
            }

            return success;
        }

        internal override bool ReadJsonAndSetMember(object obj, ref ReadStack state, ref Utf8JsonReader reader)
        {
            bool success;

            bool isNullToken = reader.TokenType == JsonTokenType.Null;
            if (isNullToken && !Converter.HandleNullOnRead && !state.IsContinuation)
            {
                if (!PropertyTypeCanBeNull)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Converter.TypeToConvert);
                }

                Debug.Assert(default(T) == null);

                if (!IgnoreDefaultValuesOnRead)
                {
                    T? value = default;
                    Set!(obj, value!);
                }

                success = true;
            }
            else if (Converter.CanUseDirectReadOrWrite && !state.Current.NumberHandling.HasValue)
            {
                // CanUseDirectReadOrWrite == false when using streams
                Debug.Assert(!state.IsContinuation);

                if (!isNullToken || !IgnoreDefaultValuesOnRead || !PropertyTypeCanBeNull)
                {
                    // Optimize for internal converters by avoiding the extra call to TryRead.
                    T? fastValue = Converter.Read(ref reader, RuntimePropertyType!, Options);
                    Set!(obj, fastValue!);
                }

                success = true;
            }
            else
            {
                success = true;
                if (!isNullToken || !IgnoreDefaultValuesOnRead || !PropertyTypeCanBeNull || state.IsContinuation)
                {
                    success = Converter.TryRead(ref reader, RuntimePropertyType!, Options, ref state, out T? value);
                    if (success)
                    {
#if !DEBUG
                        if (_converterIsExternalAndPolymorphic)
#endif
                        {
                            if (value != null)
                            {
                                Type typeOfValue = value.GetType();
                                if (!DeclaredPropertyType.IsAssignableFrom(typeOfValue))
                                {
                                    ThrowHelper.ThrowInvalidCastException_DeserializeUnableToAssignValue(typeOfValue, DeclaredPropertyType);
                                }
                            }
                            else if (!PropertyTypeCanBeNull)
                            {
                                ThrowHelper.ThrowInvalidOperationException_DeserializeUnableToAssignNull(DeclaredPropertyType);
                            }
                        }

                        Set!(obj, value!);
                    }
                }
            }

            return success;
        }

        internal override bool ReadJsonAsObject(ref ReadStack state, ref Utf8JsonReader reader, out object? value)
        {
            bool success;
            bool isNullToken = reader.TokenType == JsonTokenType.Null;
            if (isNullToken && !Converter.HandleNullOnRead && !state.IsContinuation)
            {
                if (!PropertyTypeCanBeNull)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(Converter.TypeToConvert);
                }

                value = default(T);
                success = true;
            }
            else
            {
                // Optimize for internal converters by avoiding the extra call to TryRead.
                if (Converter.CanUseDirectReadOrWrite && !state.Current.NumberHandling.HasValue)
                {
                    // CanUseDirectReadOrWrite == false when using streams
                    Debug.Assert(!state.IsContinuation);

                    value = Converter.Read(ref reader, RuntimePropertyType!, Options);
                    success = true;
                }
                else
                {
                    success = Converter.TryRead(ref reader, RuntimePropertyType!, Options, ref state, out T? typedValue);
                    value = typedValue;
                }
            }

            return success;
        }

        internal override void SetExtensionDictionaryAsObject(object obj, object? extensionDict)
        {
            Debug.Assert(HasSetter);
            T typedValue = (T)extensionDict!;
            Set!(obj, typedValue);
        }
    }
}
