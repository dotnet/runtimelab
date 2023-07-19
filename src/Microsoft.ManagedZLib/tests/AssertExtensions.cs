// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit.Sdk;

namespace Microsoft.ManagedZLib.Tests;

public static class AssertExtensions
{
    public static void Equal<T>(T[] expected, T[] actual) where T : IEquatable<T>
    {
        // Use the SequenceEqual to compare the arrays for better performance. The default Assert.Equal method compares
        // the arrays by boxing each element that is very slow for large arrays.
        if (!expected.AsSpan().SequenceEqual(actual.AsSpan()))
        {
            string expectedString = string.Join(", ", expected);
            string actualString = string.Join(", ", actual);
            throw new AssertActualExpectedException(expectedString, actualString, null);
        }
    }
}
