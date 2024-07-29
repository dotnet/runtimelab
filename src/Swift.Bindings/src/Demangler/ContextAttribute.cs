// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace BindingsGeneration.Demangling;

/// <Summary>
/// Attribute used to mark an enum element as defining a context element
/// </Summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ContextAttribute : Attribute {
}
