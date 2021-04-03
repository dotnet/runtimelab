// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class KnownCollectionTypeInfos<T>
    {
        private static JsonCollectionTypeInfo<T[]>? s_array;
        /// <summary>
        /// todo
        /// </summary>
        // TODO: Should this return JsonCollectionTypeInfo<T>?
        public static JsonCollectionTypeInfo<T[]> GetArray(JsonTypeInfo elementInfo, JsonSerializerContext context)
        {
            if (s_array == null)
            {
                s_array = new JsonCollectionTypeInfo<T[]>(CreateList, new ArrayConverter<T[], T>(), elementInfo, context._options);
            }

            return s_array;
        }

        private static JsonCollectionTypeInfo<List<T>>? s_list;
        /// <summary>
        /// todo
        /// </summary>
        public static JsonCollectionTypeInfo<List<T>> GetList(JsonTypeInfo elementInfo, JsonSerializerContext context)
        {
            if (s_list == null)
            {
                s_list = new JsonCollectionTypeInfo<List<T>>(CreateList, new ListOfTConverter<List<T>, T>(), elementInfo, context._options);
            }

            return s_list;
        }

        private static JsonCollectionTypeInfo<IEnumerable<T>>? s_ienumerable;
        /// <summary>
        /// todo
        /// </summary>
        public static JsonCollectionTypeInfo<IEnumerable<T>> GetIEnumerable(JsonTypeInfo elementInfo, JsonSerializerContext context)
        {
            if (s_ienumerable == null)
            {
                s_ienumerable = new JsonCollectionTypeInfo<IEnumerable<T>>(CreateList, new IEnumerableOfTConverter<IEnumerable<T>, T>(), elementInfo, context._options);
            }

            return s_ienumerable;
        }

        private static JsonCollectionTypeInfo<IList<T>>? s_ilist;
        /// <summary>
        /// todo
        /// </summary>
        public static JsonCollectionTypeInfo<IList<T>> GetIList(JsonTypeInfo elementInfo, JsonSerializerContext context)
        {
            if (s_ilist == null)
            {
                s_ilist = new JsonCollectionTypeInfo<IList<T>>(CreateList, new IListOfTConverter<IList<T>, T>(), elementInfo, context._options);
            }

            return s_ilist;
        }

        private static List<T> CreateList()
        {
            return new List<T>();
        }

        // todo: duplicate the above code for each supported collection type (IEnumerable, IEnumerable<T>, array, etc)
    }
}
