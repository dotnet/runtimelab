// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Dynamo;

/// <summary>
/// Extensions to make writing code easier.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Write all code elements and recurse if needed.
    /// </summary>
    /// <param name="elem">a code element to write</param>
    /// <param name="writer">a code write</param>
    public static void WriteAll(this ICodeElement elem, ICodeWriter writer)
    {
        var memento = elem.BeginWrite(writer);
        elem.Write(writer, memento);
        if (elem is ICodeElementSet set)
        {
            foreach (var sub in set.Elements)
            {
                sub.WriteAll(writer);
            }
        }
        elem.EndWrite(writer, memento);
    }

    /// <summary>
    /// Fire an event in reverse order.
    /// </summary>
    /// <typeparam name="T">The event handler type constrained to event args</typeparam>
    /// <param name="handler">an event handler</param>
    /// <param name="sender">The event source</param>
    /// <param name="args">the event arguments</param>
    public static void FireInReverse<T>(this EventHandler<T> handler, object sender, EventArgs args) where T : EventArgs
    {
        var dels = handler.GetInvocationList();
        for (int i = dels.Length - 1; i >= 0; i--)
        {
            dels[i].DynamicInvoke(new object[] { sender, args });
        }
    }

    /// <summary>
    /// Interleave a separator between elements in a collection. This is useful for writing lists of elements
    /// such as array initializers, function arguments, etc.
    /// </summary>
    /// <typeparam name="T">The type of the elements</typeparam>
    /// <param name="contents">The contents to interleave</param>
    /// <param name="separator">The separator code element</param>
    /// <param name="includeSeparatorFirst">if true, inject the separator before the elements</param>
    /// <returns></returns>
    public static IEnumerable<T> Interleave<T>(this IEnumerable<T> contents, T separator, bool includeSeparatorFirst = false)
    {
        bool first = true;
        foreach (T t in contents)
        {
            if (!first || includeSeparatorFirst)
                yield return separator;
            first = false;
            yield return t;
        }
    }

    /// <summary>
    /// Interleave a separator between elements in a collection injecting a start and end element. This is useful
    /// for writing array initializers, dictionary initializers, etc.
    /// </summary>
    /// <typeparam name="T">The type of the elements</typeparam>
    /// <param name="contents">The contents to interleave</param>
    /// <param name="start">The start element</param>
    /// <param name="end">The end element</param>
    /// <param name="separator">The separator</param>
    /// <param name="includeSeparatorFirst">if true, inject the separator before the elements</param>
    /// <returns></returns>
    public static IEnumerable<T> BracketInterleave<T>(this IEnumerable<T> contents, T start, T end, T separator, bool includeSeparatorFirst = false)
    {
        yield return start;
        foreach (T t in contents.Interleave(separator, includeSeparatorFirst))
            yield return t;
        yield return end;
    }

    /// <summary>
    /// Attach a code element before another code element. This is useful for adding attributes or adding
    /// #if/#else/#endif constructs
    /// </summary>
    /// <typeparam name="T">The type to attach, constrained to ICodeElement</typeparam>
    /// <param name="attacher">The element to attach</param>
    /// <param name="attachTo">The element that will be attached to</param>
    /// <returns>the attacher</returns>
    public static T AttachBefore<T>(this T attacher, ICodeElement attachTo) where T : ICodeElement
    {
        attachTo.Begin += (s, eventArgs) =>
        {
            attacher.WriteAll(eventArgs.Writer);
        };
        return attacher;
    }

    /// <summary>
    /// Attach a code element after another code element. This is useful for adding #else/#endif constructs
    /// </summary>
    /// <typeparam name="T">The type of the element to attach constrained to ICodeElement</typeparam>
    /// <param name="attacher">The element to attach</param>
    /// <param name="attachTo">The element that will be attached to</param>
    /// <returns>The attacher</returns>
    public static T AttachAfter<T>(this T attacher, ICodeElement attachTo) where T : ICodeElement
    {
        attachTo.End += (s, eventArgs) =>
        {
            attacher.WriteAll(eventArgs.Writer);
        };
        return attacher;
    }
}


