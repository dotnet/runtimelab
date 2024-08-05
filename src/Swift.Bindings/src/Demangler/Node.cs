// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace BindingsGeneration.Demangling;

/// <Summary>
/// A reduction node used in the process of demangling.
/// It represents a variety of different node types each of which fall into three
/// categories - empty payload, index (long) payload or string payload.
/// </Summary>
public class Node {
	/// <Summary>
	/// Construct a Node from the given kind and payload kind
	/// </Summary>
	Node (NodeKind kind, PayloadKind payload)
	{
		Kind = kind;
		PayloadKind = payload;
		Children = new List<Node> ();
	}

	/// <Summary>
	/// Construct a Node from the given kind and set the payload kind to None
	/// </Summary>
	public Node (NodeKind kind)
		: this (kind, PayloadKind.None)
	{
	}

	/// <Summary>
	/// Construct a Node from the given kind with the given text, setting the payload kind to Text
	/// </Summary>
	public Node (NodeKind kind, string text)
		: this (kind, PayloadKind.Text)
	{
		stringPayload = text;
	}

	/// <Summary>
	/// Construct a node from the given kind with the given index, setting the payload kind to Index
	/// </Summary>
	public Node (NodeKind kind, long index)
		: this (kind, PayloadKind.Index)
	{
		indexPayload = index;
	}

	string? stringPayload = null;
	long indexPayload = 0;

	/// <Summary>
	/// Get a List of the child Nodes of this node
	/// </Summary>
	public List<Node> Children { get; private set; }

	/// <Summary>
	/// Get the NodeKind
	/// </Summary>
	public NodeKind Kind { get; private set; }
	public PayloadKind PayloadKind { get; private set; }

	/// <Summary>
	/// Returns true if and only if the PayloadKind is Text
	/// </Summary>
	[MemberNotNullWhen(true, nameof(stringPayload))]
	public bool HasText { get { return PayloadKind == PayloadKind.Text; } }
	public string Text {
		get {
			if (!HasText)
				throw new InvalidOperationException ($"Expected a text payload, but this has a {PayloadKind} payload");
			return stringPayload;
		}
	}

	/// <Summary>
	/// Returns true if and only if the PayloadKind is Index
	/// </Summary>
	public bool HasIndex { get { return PayloadKind == PayloadKind.Index; } }
	public long Index {
		get {
			if (!HasIndex)
				throw new InvalidOperationException ($"Expected an index payload, but this has a {PayloadKind} payload");
			return indexPayload;
		}
	}

	/// <Summary>
	/// Add a child node
	/// </Summary>
	public void AddChild (Node child)
	{
		Children.Add (child);
	}

	/// <Summary>
	/// Remove a child node at the given position
	/// </Summary>
	public void RemoveChildAt(int pos)
	{
		Children.RemoveAt (pos);
	}

	/// <Summary>
	/// Reverse the list of children from the given starting index, defaulting to 0
	/// </Summary>
	public void ReverseChildren (int startingAt = 0)
	{
		var last = Children.Count - 1;
		if (startingAt < 0 || startingAt > Children.Count)
			throw new ArgumentOutOfRangeException (nameof (startingAt));
		while (startingAt < last) {
			Node temp = Children [startingAt];
			Children [startingAt] = Children [last];
			Children [last] = temp;
			startingAt++;
			last--;
		}
	}


	/// <Summary>
	/// Return true if the node is a declaration name
	/// </Summary>
	public static bool IsDeclName (NodeKind kind)
	{
		switch (kind) {
		case NodeKind.Identifier:
		case NodeKind.LocalDeclName:
		case NodeKind.PrivateDeclName:
		case NodeKind.RelatedEntityDeclName:
		case NodeKind.PrefixOperator:
		case NodeKind.PostfixOperator:
		case NodeKind.InfixOperator:
		case NodeKind.TypeSymbolicReference:
		case NodeKind.ProtocolSymbolicReference:
			return true;
		default:
			return false;
		}
	}

	/// <Summary>
	/// Return true if and only if the given node kind is a context node
	/// </Summary>
	public static bool IsContext (NodeKind kind)
	{
		var type = typeof (NodeKind);
		var memberInfo = type.GetMember (kind.ToString ());
		var attrs = memberInfo [0].GetCustomAttributes (typeof (ContextAttribute), false);
		return attrs != null && attrs.Length == 1;
	}

	/// <Summary>
	/// Return true if the node could be a generic type
	/// </Summary>
	public static bool IsAnyGeneric (NodeKind kind)
	{
		switch (kind) {
		case NodeKind.Structure:
		case NodeKind.Class:
		case NodeKind.Enum:
		case NodeKind.Protocol:
		case NodeKind.ProtocolSymbolicReference:
		case NodeKind.OtherNominalType:
		case NodeKind.TypeAlias:
		case NodeKind.TypeSymbolicReference:
			return true;
		default:
			return false;
		}
	}

	/// <Summary>
	/// Return true if and only if the node is a nominal type
	/// </Summary>
	public static bool IsNominal (NodeKind kind)
	{
		switch (kind) {
		case NodeKind.Structure:
		case NodeKind.Class:
		case NodeKind.Enum:
		case NodeKind.Protocol:
			return true;
		default:
			return false;
		}
	}

	/// <Summary>
	/// Return true if and only if the node is an entity
	/// </Summary>
	public static bool IsEntity (NodeKind kind)
	{
		if (kind == NodeKind.Type)
			return true;
		return IsContext (kind);
	}

	/// <Summary>
	/// Return true if and only if the node is a requirement
	/// </Summary>
	public static bool IsRequirement (NodeKind kind)
	{
		switch (kind) {
		case NodeKind.DependentGenericSameTypeRequirement:
		case NodeKind.DependentGenericLayoutRequirement:
		case NodeKind.DependentGenericConformanceRequirement:
			return true;
		default:
			return false;
		}
	}

	/// <Summary>
	/// Return true if and only if the node is an attribute applied to a function
	/// </Summary>
	public static bool IsFunctionAttribute (NodeKind kind)
	{
		switch (kind) {
		case NodeKind.FunctionSignatureSpecialization:
		case NodeKind.GenericSpecialization:
		case NodeKind.InlinedGenericFunction:
		case NodeKind.GenericSpecializationNotReAbstracted:
		case NodeKind.GenericPartialSpecialization:
		case NodeKind.GenericPartialSpecializationNotReAbstracted:
		case NodeKind.ObjCAttribute:
		case NodeKind.NonObjCAttribute:
		case NodeKind.DynamicAttribute:
		case NodeKind.DirectMethodReferenceAttribute:
		case NodeKind.VTableAttribute:
		case NodeKind.PartialApplyForwarder:
		case NodeKind.PartialApplyObjCForwarder:
		case NodeKind.OutlinedVariable:
		case NodeKind.OutlinedBridgedMethod:
		case NodeKind.MergedFunction:
		case NodeKind.DynamicallyReplaceableFunctionImpl:
		case NodeKind.DynamicallyReplaceableFunctionKey:
		case NodeKind.DynamicallyReplaceableFunctionVar:
			return true;
		default:
			return false;
		}
	}

	public override string ToString ()
	{
		var sb = new StringBuilder ();
		ToString (0, sb);
		return sb.ToString ();
	}

	void ToString (int indent, StringBuilder sb)
	{
		for (int i = 0; i < indent; i++) {
			sb.Append (' ');
		}
		sb.Append ("->").Append (Kind.ToString ());
		switch (PayloadKind) {
		case PayloadKind.None:
			sb.Append (Environment.NewLine);
			break;
		case PayloadKind.Index:
			sb.Append ($" ({Index})\n");
			break;
		case PayloadKind.Text:
			sb.Append ($" (\"{Text}\")\n");
			break;
		}
		foreach (var node in Children) {
			node.ToString (indent + 2, sb);
		}
	}

	/// <Summary>
	/// Return true if and only if the node is an attribute
	/// </Summary>
	public bool IsAttribute ()
	{
		switch (Kind) {
		case NodeKind.ObjCAttribute:
		case NodeKind.DynamicAttribute:
		case NodeKind.NonObjCAttribute:
		case NodeKind.ImplFunctionAttribute:
		case NodeKind.DirectMethodReferenceAttribute:
			return true;
		default:
			return false;
		}
	}
}
