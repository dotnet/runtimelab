// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Threading.Tasks;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class KnownCollectionTypeInfos<T>
    {
        private static JsonTypeInfo<T[]>? s_array;
        /// <summary>
        /// todo
        /// </summary>
        public static JsonTypeInfo<T[]> GetArray(JsonClassInfo elementInfo, JsonSerializerContext context)
        {
            // todo: support obtaining existing converter
            //if (context._options.HasCustomConverters)
            //{
            //JsonConverter<T[]> converter = (JsonConverter<T[]>)context._options.GetConverter(typeof(T[]));
            //return new JsonCollectionTypeInfo<T[]>(CreateList, converter, elementInfo, context._options);
            //}

            if (s_array == null)
            {
                s_array = new JsonCollectionTypeInfo<T[]>(CreateList, new ArrayConverter<T[], T>(), elementInfo, context._options);
            }

            return s_array;
        }

        private static JsonTypeInfo<List<T>>? s_list;
        /// <summary>
        /// todo
        /// </summary>
        public static JsonTypeInfo<List<T>> GetList(JsonClassInfo elementInfo, JsonSerializerContext context)
        {
            // todo: support obtaining existing converter
            //if (context._options.HasCustomConverters)
            //{
                //JsonConverter<List<T>> converter = (JsonConverter<List<T>>)context._options.GetConverter(typeof(List<T>));
                //return new JsonCollectionTypeInfo<List<T>>(CreateList, converter, elementInfo, context._options);
            //}

            if (s_list == null)
            {
                s_list = new JsonCollectionTypeInfo<List<T>>(CreateList, new ListOfTConverter<List<T>, T>(), elementInfo, context._options);
            }

            return s_list;
        }

        private static JsonTypeInfo<IEnumerable<T>>? s_ienumerable;
        /// <summary>
        /// todo
        /// </summary>
        public static JsonTypeInfo<IEnumerable<T>> GetIEnumerable(JsonClassInfo elementInfo, JsonSerializerContext context)
        {
            // todo: support obtaining existing converter
            //if (context._options.HasCustomConverters)
            //{
            //JsonConverter<IEnumerable<T>> converter = (JsonConverter<IEnumerable<T>>)context._options.GetConverter(typeof(IEnumerable<T>));
            //return new JsonCollectionTypeInfo<IEnumerable<T>>(CreateList, converter, elementInfo, context._options);
            //}

            if (s_ienumerable == null)
            {
                s_ienumerable = new JsonCollectionTypeInfo<IEnumerable<T>>(CreateList, new IEnumerableOfTConverter<IEnumerable<T>, T>(), elementInfo, context._options);
            }

            return s_ienumerable;
        }

        private static JsonTypeInfo<Queue<T>>? s_queue;
        /// <summary>
        /// todo
        /// </summary>
        public static JsonTypeInfo<Queue<T>> GetQueue(JsonClassInfo elementInfo, JsonSerializerContext context)
        {
            // todo: support obtaining existing converter
            //if (context._options.HasCustomConverters)
            //{
            //JsonConverter<Queue<T>> converter = (JsonConverter<Queue<T>>)context._options.GetConverter(typeof(Queue<T>));
            //return new JsonCollectionTypeInfo<Queue<T>>(CreateQueue, converter, elementInfo, context._options);
            //}

            if (s_queue == null)
            {
                s_queue = new JsonCollectionTypeInfo<Queue<T>>(CreateQueue, new QueueOfTConverter<Queue<T>, T>(), elementInfo, context._options);
            }

            return s_queue;
        }

        private static JsonTypeInfo<Stack<T>>? s_stack;
        /// <summary>
        /// todo
        /// </summary>
        public static JsonTypeInfo<Stack<T>> GetStack(JsonClassInfo elementInfo, JsonSerializerContext context)
        {
            // todo: support obtaining existing converter
            //if (context._options.HasCustomConverters)
            //{
            //JsonConverter<Stack<T>> converter = (JsonConverter<Stack<T>>)context._options.GetConverter(typeof(Stack<T>));
            //return new JsonCollectionTypeInfo<Stack<T>>(CreateStack, converter, elementInfo, context._options);
            //}

            if (s_stack == null)
            {
                s_stack = new JsonCollectionTypeInfo<Stack<T>>(CreateStack, new StackOfTConverter<Stack<T>, T>(), elementInfo, context._options);
            }

            return s_stack;
        }

        private static List<T> CreateList()
        {
            return new List<T>();
        }

        private static Queue<T> CreateQueue()
        {
            return new Queue<T>();
        }

        private static Stack<T> CreateStack()
        {
            return new Stack<T>();
        }

        // todo: duplicate the above code for each supported collection type (IEnumerable, IEnumerable<T>, array, etc)
    }
}
