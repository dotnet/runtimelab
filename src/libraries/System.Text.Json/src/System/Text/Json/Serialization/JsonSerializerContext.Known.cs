// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// todo
    /// </summary>
    public partial class JsonSerializerContext
    {
        // TODO: do we need to call CompleteInitialization for each TypeInfo here (to add them to the class info cache)

        private JsonTypeInfo<bool>? _boolean;
        private static JsonTypeInfo<bool>? s_boolean;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<bool> Boolean
        {
            get
            {
                if (_boolean == null)
                {
                    {
                        if (s_boolean == null)
                        {
                            s_boolean = new JsonValueInfo<bool>(new BooleanConverter(), _options);
                        }

                        _boolean = s_boolean;
                    }
                }

                return _boolean;
            }
        }

        private JsonTypeInfo<byte[]>? _byteArray;
        private static JsonTypeInfo<byte[]>? s_byteArray;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<byte[]> ByteArray
        {
            get
            {
                if (_byteArray == null)
                {
                    {
                        if (s_byteArray == null)
                        {
                            s_byteArray = new JsonValueInfo<byte[]>(new ByteArrayConverter(), _options);
                        }

                        _byteArray = s_byteArray;
                    }
                }

                return _byteArray;
            }
        }

        private JsonTypeInfo<byte>? _byte;
        private static JsonTypeInfo<byte>? s_byte;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<byte> Byte
        {
            get
            {
                if (_byte == null)
                {
                    {
                        if (s_byte == null)
                        {
                            s_byte = new JsonValueInfo<byte>(new ByteConverter(), _options);
                        }

                        _byte = s_byte;
                    }
                }

                return _byte;
            }
        }

        private JsonTypeInfo<char>? _char;
        private static JsonTypeInfo<char>? s_char;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<char> Char
        {
            get
            {
                if (_char == null)
                {
                    {
                        if (s_char == null)
                        {
                            s_char = new JsonValueInfo<char>(new CharConverter(), _options);
                        }

                        _char = s_char;
                    }
                }

                return _char;
            }
        }

        private JsonTypeInfo<DateTime>? _dateTime;
        private static JsonTypeInfo<DateTime>? s_dateTime;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<DateTime> DateTime
        {
            get
            {
                if (_dateTime == null)
                {
                    {
                        if (s_dateTime == null)
                        {
                            s_dateTime = new JsonValueInfo<DateTime>(new DateTimeConverter(), _options);
                        }

                        _dateTime = s_dateTime;
                    }
                }

                return _dateTime;
            }
        }

        private JsonTypeInfo<DateTimeOffset>? _dateTimeOffset;
        private static JsonTypeInfo<DateTimeOffset>? s_dateTimeOffset;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<DateTimeOffset> DateTimeOffset
        {
            get
            {
                if (_dateTimeOffset == null)
                {
                    {
                        if (s_dateTimeOffset == null)
                        {
                            s_dateTimeOffset = new JsonValueInfo<DateTimeOffset>(new DateTimeOffsetConverter(), _options);
                        }

                        _dateTimeOffset = s_dateTimeOffset;
                    }
                }

                return _dateTimeOffset;
            }
        }

        private JsonTypeInfo<decimal>? _decimal;
        private static JsonTypeInfo<decimal>? s_decimal;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<decimal> Decimal
        {
            get
            {
                if (_decimal == null)
                {
                    {
                        if (s_decimal == null)
                        {
                            s_decimal = new JsonValueInfo<decimal>(new DecimalConverter(), _options);
                        }

                        _decimal = s_decimal;
                    }
                }

                return _decimal;
            }
        }

        private JsonTypeInfo<double>? _double;
        private static JsonTypeInfo<double>? s_double;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<double> Double
        {
            get
            {
                if (_double == null)
                {
                    {
                        if (s_double == null)
                        {
                            s_double = new JsonValueInfo<double>(new DoubleConverter(), _options);
                        }

                        _double = s_double;
                    }
                }

                return _double;
            }
        }

        private JsonTypeInfo<Guid>? _guid;
        private static JsonTypeInfo<Guid>? s_guid;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<Guid> Guid
        {
            get
            {
                if (_guid == null)
                {
                    {
                        if (s_guid == null)
                        {
                            s_guid = new JsonValueInfo<Guid>(new GuidConverter(), _options);
                        }

                        _guid = s_guid;
                    }
                }

                return _guid;
            }
        }

        private JsonTypeInfo<short>? _int16;
        private static JsonTypeInfo<short>? s_int16;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<short> Int16
        {
            get
            {
                if (_int16 == null)
                {
                    {
                        if (s_int16 == null)
                        {
                            s_int16 = new JsonValueInfo<short>(new Int16Converter(), _options);
                        }

                        _int16 = s_int16;
                    }
                }

                return _int16;
            }
        }

        private JsonTypeInfo<int>? _int32;
        private static JsonTypeInfo<int>? s_int32;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<int> Int32
        {
            get
            {
                if (_int32 == null)
                {
                    {
                        if (s_int32 == null)
                        {
                            s_int32 = new JsonValueInfo<int>(new Int32Converter(), _options);
                        }

                        _int32 = s_int32;
                    }
                }

                return _int32;
            }
        }

        private JsonTypeInfo<long>? _int64;
        private static JsonTypeInfo<long>? s_int64;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<long> Int64
        {
            get
            {
                if (_int64 == null)
                {
                    {
                        if (s_int64 == null)
                        {
                            s_int64 = new JsonValueInfo<long>(new Int64Converter(), _options);
                        }

                        _int64 = s_int64;
                    }
                }

                return _int64;
            }
        }

        private JsonTypeInfo<float>? _single;
        private static JsonTypeInfo<float>? s_single;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<float> Single
        {
            get
            {
                if (_single == null)
                {
                    {
                        if (s_single == null)
                        {
                            s_single = new JsonValueInfo<float>(new SingleConverter(), _options);
                        }

                        _single = s_single;
                    }
                }

                return _single;
            }
        }

        private JsonTypeInfo<sbyte>? _sbyte;
        private static JsonTypeInfo<sbyte>? s_sbyte;
        /// <summary>
        /// todo
        /// </summary>
        [CLSCompliant(false)]
        public JsonTypeInfo<sbyte> SByte
        {
            get
            {
                if (_sbyte == null)
                {
                    {
                        if (s_sbyte == null)
                        {
                            s_sbyte = new JsonValueInfo<sbyte>(new SByteConverter(), _options);
                        }

                        _sbyte = s_sbyte;
                    }
                }

                return _sbyte;
            }
        }

        private JsonTypeInfo<string>? _string;
        private static JsonTypeInfo<string>? s_string;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<string> String
        {
            get
            {
                if (_string == null)
                {
                    {
                        if (s_string == null)
                        {
                            var valueInfo = new JsonValueInfo<string>(new StringConverter(), _options);
                            valueInfo.CompleteInitialization();
                            s_string = valueInfo;
                        }

                        _string = s_string;
                    }
                }

                return _string;
            }
        }

        private JsonTypeInfo<ushort>? _uint16;
        private static JsonTypeInfo<ushort>? s_uint16;
        /// <summary>
        /// todo
        /// </summary>
        [CLSCompliant(false)]
        public JsonTypeInfo<ushort> UInt16
        {
            get
            {
                if (_uint16 == null)
                {
                    {
                        if (s_uint16 == null)
                        {
                            s_uint16 = new JsonValueInfo<ushort>(new UInt16Converter(), _options);
                        }

                        _uint16 = s_uint16;
                    }
                }

                return _uint16;
            }
        }

        private JsonTypeInfo<uint>? _uint32;
        private static JsonTypeInfo<uint>? s_uint32;
        /// <summary>
        /// todo
        /// </summary>
        [CLSCompliant(false)]
        public JsonTypeInfo<uint> UInt32
        {
            get
            {
                if (_uint32 == null)
                {
                    {
                        if (s_uint32 == null)
                        {
                            s_uint32 = new JsonValueInfo<uint>(new UInt32Converter(), _options);
                        }

                        _uint32 = s_uint32;
                    }
                }

                return _uint32;
            }
        }

        private JsonTypeInfo<ulong>? _uint64;
        private static JsonTypeInfo<ulong>? s_uint64;
        /// <summary>
        /// todo
        /// </summary>
        [CLSCompliant(false)]
        public JsonTypeInfo<ulong> UInt64
        {
            get
            {
                if (_uint64 == null)
                {
                    {
                        if (s_uint64 == null)
                        {
                            s_uint64 = new JsonValueInfo<ulong>(new UInt64Converter(), _options);
                        }

                        _uint64 = s_uint64;
                    }
                }

                return _uint64;
            }
        }

        private JsonTypeInfo<Uri>? _uri;
        private static JsonTypeInfo<Uri>? s_uri;
        /// <summary>
        /// todo
        /// </summary>
        public JsonTypeInfo<Uri> Uri
        {
            get
            {
                if (_uri == null)
                {
                    {
                        if (s_uri == null)
                        {
                            s_uri = new JsonValueInfo<Uri>(new UriConverter(), _options);
                        }

                        _uri = s_uri;
                    }
                }

                return _uri;
            }
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <param name="type"></param>
        public virtual JsonClassInfo? GetJsonClassInfo(Type type) => throw new NotImplementedException();
    }
}
