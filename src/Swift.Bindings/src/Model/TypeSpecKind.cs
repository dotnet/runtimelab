// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace BindingsGeneration;


/// <summary>
/// Describes the kind of the TypeSpec 
/// </summary>
public enum TypeSpecKind
{

	/// <summary>
	/// A nominal type, e.g. A.B.C 
	/// </summary>
    Named = 0,

	/// <summary>
	/// a tuple type, e.g. (T1, T2, T3)
	/// </summary>
    Tuple,

	/// <summary>
	/// a closure type, e.g. async (a: Int, b: Bool) -> String
	/// </summary>
    Closure,

	/// <summary>
	/// a protocol list type, e.g. P1 &amp; P2 &amp; P3 
	/// </summary>
    ProtocolList,
}