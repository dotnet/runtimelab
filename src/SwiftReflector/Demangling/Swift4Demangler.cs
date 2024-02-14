// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using SwiftReflector.ExceptionTools;
using SwiftRuntimeLibrary;

namespace SwiftReflector.Demangling {
	public class Swift4Demangler {
		public const int kMaxRepeatCount = 2048;
		ulong offset;
		string originalIdentifier;
		Stack<Node> nodeStack = new Stack<Node> ();
		List<Node> substitutions = new List<Node> ();
		List<uint> pendingSubstitutions = new List<uint> ();
		List<string> words = new List<string> ();
		StringSlice slice;

		public Swift4Demangler (string swiftIdentifier, ulong offset = 0)
		{
			Exceptions.ThrowOnNull (swiftIdentifier, nameof (swiftIdentifier));
			if (!swiftIdentifier.StartsWith (Decomposer.kSwift4ID, StringComparison.Ordinal))
				throw new ArgumentOutOfRangeException (nameof (swiftIdentifier), $"Expecting '{swiftIdentifier}' to start with '{Decomposer.kSwift4ID}'");
			this.offset = offset;
			originalIdentifier = swiftIdentifier;
			slice = new StringSlice (originalIdentifier);
			slice.Advance (Decomposer.kSwift4ID.Length);
		}

		public TLDefinition Run()
		{
			Node topLevelNode = DemangleType ();
			if (topLevelNode != null && topLevelNode.IsAttribute() && nodeStack.Count >= 1) {
				var attribute = topLevelNode.ExtractAttribute ();
				var tld = Run ();
				if (tld != null && tld is TLFunction tlf) {
					tlf.Signature.Attributes.Add (attribute);
				}
				return tld;
			} else {
				Swift4NodeToTLDefinition converter = new Swift4NodeToTLDefinition (originalIdentifier, offset);
				return converter.Convert (topLevelNode);
			}
		}

		public string ExplodedView()
		{
			Node topLevelNode = DemangleType ();
			return topLevelNode != null ? topLevelNode.ToString () : null;
		}

		Node DemangleType()
		{
			if (!ParseAndPushNodes ())
				throw ErrorHelper.CreateError (ReflectorError.kDecomposeBase + 2, $"Error decomposing {slice.Original} at {slice.Position}");
			var node = PopNode ();
			if (node != null)
				return node;
			return new Node (NodeKind.Suffix, slice.Original);
		}

		bool ParseAndPushNodes()
		{
			while (!slice.IsAtEnd) {
				var node = DemangleOperator ();
				if (node == null)
					return false;
				PushNode (node);
			}
			return true;
		}

		bool NextIf (string str)
		{
			if (slice.StartsWith (str)) {
				slice.Advance (str.Length);
				return true;
			}
			return false;

		}

		char PeekChar ()
		{
			if (slice.IsAtEnd)
				return (char)0;
			return slice.Current;
		}

		char NextChar ()
		{
			return slice.Advance ();
		}

		bool NextIf (char c)
		{
			return slice.AdvanceIfEquals (c);
		}

		void PushBack ()
		{
			slice.Rewind ();
		}

		string ConsumeAll ()
		{
			return slice.ConsumeRemaining ();
		}

		void PushNode (Node node)
		{
			nodeStack.Push (node);
		}

		Node PopNode ()
		{
			if (nodeStack.Count == 0)
				return null;
			return nodeStack.Pop ();
		}

		Node PopNode (NodeKind kind)
		{
			if (nodeStack.Count == 0)
				return null;
			if (kind != nodeStack.Peek ().Kind)
				return null;
			return PopNode ();
		}

		Node PopNode (Predicate<NodeKind> pred)
		{
			if (nodeStack.Count == 0)
				return null;
			if (!pred (nodeStack.Peek ().Kind))
				return null;
			return PopNode ();
		}

		void AddSubstitution (Node nd)
		{
			if (nd != null)
				substitutions.Add (nd);
		}

		Node AddChild (Node parent, Node child)
		{
			if (parent == null || child == null)
				return null;
			parent.Children.Add (child);
			return parent;
		}

		Node CreateWithChild (NodeKind kind, Node child)
		{
			if (child == null)
				return null;
			var node = new Node (kind);
			node.Children.Add (child);
			return node;
		}

		Node CreateType (Node child)
		{
			return CreateWithChild (NodeKind.Type, child);
		}

		Node CreateWithChildren (NodeKind kind, params Node [] children)
		{
			foreach (var nd in children)
				if (nd == null)
					throw new ArgumentOutOfRangeException (nameof (children));
			var node = new Node (kind);
			node.Children.AddRange (children);
			return node;
		}

		Node CreateWithPoppedType(NodeKind kind)
		{
			return CreateWithChild (kind, PopNode (NodeKind.Type));
		}

		Node ChangeKind (Node node, NodeKind newKind)
		{
			Node newNode = null;
			if (node.HasText) {
				newNode = new Node (node.Kind, node.Text);
			} else if (node.HasIndex) {
				newNode = new Node (node.Kind, node.Index);
			} else {
				newNode = new Node (node.Kind);
			}
			newNode.Children.AddRange (node.Children);
			return newNode;
		}

		Node DemangleOperator ()
		{
			var c = NextChar ();
			switch (c) {
			case 'A': return DemangleMultiSubstitutions ();
			case 'B': return DemangleBuiltinType ();
			case 'C': return DemangleAnyGenericType (NodeKind.Class);
			case 'D': return CreateWithChild (NodeKind.TypeMangling, PopNode (NodeKind.Type));
			case 'E': return DemangleExtensionContext ();
			case 'F': return DemanglePlainFunction ();
			case 'G': return DemangleBoundGenericType ();
			case 'I': return DemangleImplFunctionType ();
			case 'K': return new Node (NodeKind.ThrowsAnnotation);
			case 'L': return DemangleLocalIdentifier ();
			case 'M': return DemangleMetatype ();
			case 'N': return CreateWithChild (NodeKind.TypeMetadata, PopNode (NodeKind.Type));
			case 'O': return DemangleAnyGenericType (NodeKind.Enum);
			case 'P': return DemangleAnyGenericType (NodeKind.Protocol);
			case 'Q': return DemangleArchetype ();
			case 'R': return DemangleGenericRequirement ();
			case 'S': return DemangleStandardSubstitution ();
			case 'T': return DemangleThunkOrSpecialization ();
			case 'V': return DemangleAnyGenericType (NodeKind.Structure);
			case 'W': return DemangleWitness ();
			case 'X': return DemangleSpecialType ();
			case 'Z': return CreateWithChild (NodeKind.Static, PopNode (Node.IsEntity));
			case 'a': return DemangleAnyGenericType (NodeKind.TypeAlias);
			case 'c': return PopFunctionType (NodeKind.FunctionType);
			case 'd': return new Node (NodeKind.VariadicMarker);
			case 'f': return DemangleFunctionEntity ();
			case 'i': return DemangleEntity (NodeKind.Subscript);
			case 'l': return DemangleGenericSignature (false);
			case 'm': return CreateType (CreateWithChild (NodeKind.Metatype, PopNode (NodeKind.Type)));
			case 'o': return DemangleOperatorIdentifier ();
			case 'p': return DemangleProtocolListType ();
			case 'q': return CreateType (DemangleGenericParamIndex ());
			case 'r': return DemangleGenericSignature (true);
			case 's': return new Node (NodeKind.Module, "Swift");
			case 't': return PopTuple ();
			case 'u': return DemangleGenericType ();
			case 'v': return DemangleEntity (NodeKind.Variable);
			case 'w': return DemangleValueWitness ();
			case 'x': return CreateType (GetDependentGenericParamType (0, 0));
			case 'y': return new Node (NodeKind.EmptyList);
			case 'z': return CreateType (CreateWithChild (NodeKind.InOut, PopTypeAndGetChild ()));
			case '_': return new Node (NodeKind.FirstElementMarker);
			case '.':
				PushBack ();
				return new Node (NodeKind.Suffix, ConsumeAll ());
			default:
				PushBack ();
				return DemangleIdentifier ();
			}
		}

		Node DemangleLocalIdentifier()
		{
			if (NextIf('L')) {
				var adiscriminator = PopNode (NodeKind.Identifier);
				var aname = PopNode (Node.IsDeclName);
				return CreateWithChildren (NodeKind.PrivateDeclName, adiscriminator, aname);
			}
			if (NextIf('l')) {
				var adiscriminator = PopNode (NodeKind.Identifier);
				return CreateWithChild (NodeKind.PrivateDeclName, adiscriminator);
			}
			var discriminator = DemangleIndexAsNode ();
			var name = PopNode (Node.IsDeclName);
			return CreateWithChildren (NodeKind.LocalDeclName, discriminator, name);
		}

		Node PopModule()
		{
			var ident = PopNode (NodeKind.Identifier);
			if (ident != null)
				return ChangeKind (ident, NodeKind.Module);
			return PopNode (NodeKind.Module);
		}

		Node PopContext()
		{
			var mod = PopModule ();
			if (mod != null)
				return mod;
			Node ty = PopNode (NodeKind.Type);
			if (ty != null) {
				if (ty.Children.Count != 1)
					return null;
				var child = ty.Children [0];
				if (!Node.IsContext (child.Kind))
					return null;
				return child;
			}
			return PopNode (Node.IsContext);
		}

		Node PopTypeAndGetChild ()
		{
			Node ty = PopNode (NodeKind.Type);
			if (ty == null || ty.Children.Count != 1)
				return null;
			return ty.Children [0];
		}

		Node PopTypeAndGetNominal()
		{
			var child = PopTypeAndGetChild ();
			if (child != null && Node.IsNominal (child.Kind))
				return child;
			return null;
		}

		Node DemangleBuiltinType()
		{
			Node ty = null;
			switch (NextChar ()) {
			case 'b':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.BridgeObject");
				break;
			case 'B':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.UnsafeValueBuffer");
				break;
			case 'f': {
					int size = DemangleIndex () - 1;
					if (size <= 0)
						return null;
					string name = $"Builtin.Float{size}";
					ty = new Node (NodeKind.BuiltinTypeName, name);
					break;
				}
			case 'i': {
					int size = DemangleIndex () - 1;
					if (size <= 0)
						return null;
					string name = $"Builtin.Int{size}";
					ty = new Node (NodeKind.BuiltinTypeName, name);
					break;
				}
			case 'v': {
					int elts = DemangleIndex () - 1;
					if (elts <= 0)
						return null;
					Node eltType = PopTypeAndGetChild ();
					if (eltType == null || eltType.Kind != NodeKind.BuiltinTypeName ||
					    !eltType.Text.StartsWith ("Builtin.", StringComparison.Ordinal))
						return null;
					string name = $"Builtin.Vec{elts}x{eltType.Text.Substring ("Builtin.".Length)}";
					ty = new Node (NodeKind.BuiltinTypeName, name);
					break;
				}
			case 'O':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.UnknownObject");
				break;
			case 'o':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.NativeObject");
				break;
			case 'p':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.RawPointer");
				break;
			case 'w':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.Word");
				break;
			default:
				return null;
			}
			return CreateType (ty);
		}

		Node DemangleAnyGenericType(NodeKind kind)
		{
			var name = PopNode (Node.IsDeclName);
			var ctx = PopContext ();
			var nty = CreateType (CreateWithChildren (kind, ctx, name));
			AddSubstitution (nty);
			return nty;
		}

		Node DemangleExtensionContext()
		{
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			var module = PopModule ();
			var type = PopTypeAndGetNominal ();
			var ext = CreateWithChildren (NodeKind.Extension, module, type);
			if (genSig != null)
				ext = AddChild (ext, genSig);
			return ext;
		}

		Node DemanglePlainFunction()
		{
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			var type = PopFunctionType (NodeKind.FunctionType);
			if (genSig != null)
				type = CreateType (CreateWithChildren (NodeKind.DependentGenericType, genSig, type));
			var name = PopNode (Node.IsDeclName);
			var ctx = PopContext ();
			return CreateWithChildren (NodeKind.Function, ctx, name, type);
		}

		Node PopFunctionType(NodeKind kind)
		{
			var funcType = new Node (kind);
			AddChild (funcType, PopNode (NodeKind.ThrowsAnnotation));
			funcType = AddChild (funcType, PopFunctionParams (NodeKind.ArgumentTuple));
			funcType = AddChild (funcType, PopFunctionParams (NodeKind.ReturnType));
			return CreateType (funcType);
		}

		Node PopFunctionParams(NodeKind kind)
		{
			Node parmsType = null;
			if (PopNode(NodeKind.EmptyList) != null) {
				parmsType = CreateType (new Node (NodeKind.Tuple));
			} else {
				parmsType = PopNode (NodeKind.Type);
			}
			return CreateWithChild (kind, parmsType);
		}

		Node PopTuple()
		{
			var root = new Node (NodeKind.Tuple);

			if (PopNode(NodeKind.EmptyList) == null) {
				bool firstElem = false;
				do {
					firstElem = PopNode (NodeKind.FirstElementMarker) != null;
					var tupleElmt = new Node (NodeKind.TupleElement);
					AddChild (tupleElmt, PopNode (NodeKind.VariadicMarker));
					Node ident = null;
					if ((ident = PopNode (NodeKind.Identifier)) != null) {
						tupleElmt.Children.Add (new Node (NodeKind.TupleElementName, ident.Text));
					}
					var ty = PopNode (NodeKind.Type);
					if (ty == null)
						return null;
					tupleElmt.Children.Add (ty);
					root.Children.Add (tupleElmt);
				} while (!firstElem);
				root.ReverseChildren ();
			}
			return CreateType (root);
		}


		Node DemangleMultiSubstitutions ()
		{
			var repeatCount = -1;
			while (true) {
				var c = NextChar ();
				if (c == 0) {
					return null;
				}
				if (Char.IsLower (c)) {
					Node nd = PushMultiSubstitutions (repeatCount, c - 'a');
					if (nd == null)
						return null;
					PushNode (nd);
					repeatCount = -1;
					continue;
				}
				if (Char.IsUpper (c)) {
					return PushMultiSubstitutions (repeatCount, c - 'A');
				}
				if (c == '_') {
					var index = repeatCount + 27;
					if (index >= substitutions.Count)
						return null;
					return substitutions [index];
				}
				PushBack ();
				repeatCount = DemangleNatural ();
				if (repeatCount < 0)
					return null;
			}
		}

		int DemangleNatural()
		{
			if (!Char.IsDigit (PeekChar ()))
				return -1000;
			int num = 0;
			while (true) {
				var c = PeekChar ();
				if (!Char.IsDigit (c))
					return num;
				int newNum = (10 * num) + (c - '0');
				if (newNum < num)
					return -1000;
				num = newNum;
				NextChar ();
			}
		}

		int DemangleIndex()
		{
			if (NextIf ('_'))
				return 0;
			int num = DemangleNatural ();
			if (num >= 0 && NextIf ('_'))
				return num + 1;
			return -1000;
		}

		Node DemangleIndexAsNode()
		{
			int idx = DemangleIndex ();
			if (idx >= 0)
				return new Node (NodeKind.Number, idx);
			return null;
		}

		Node PushMultiSubstitutions(int repeatCount, int index)
		{
			if (index >= substitutions.Count)
				return null;
			if (repeatCount > kMaxRepeatCount)
				return null;
			Node nd = substitutions [index];
			while (repeatCount-- > 1) {
				PushNode (nd);
			}
			return nd;
		}

		Node CreateSwiftType(NodeKind typeKind, string name) {
			return CreateType (CreateWithChildren (typeKind,
							     new Node (NodeKind.Module, "Swift"),
							     new Node (NodeKind.Identifier, name)));
		}

		Node DemangleStandardSubstitution()
		{
			char c = NextChar ();
			switch(c) {
			case 'o':
				return new Node (NodeKind.Module, "__ObjC");
			case 'C':
				return new Node (NodeKind.Module, "__C");
			case 'g': {
					var optionalTy = CreateType (CreateWithChildren (NodeKind.BoundGenericEnum,
										       CreateSwiftType (NodeKind.Enum, "Optional"),
										       CreateWithChild (NodeKind.TypeList, PopNode (NodeKind.Type))));
					AddSubstitution (optionalTy);
					return optionalTy;
				}
			default: {
					PushBack ();
					var repeatCount = DemangleNatural ();
					if (repeatCount > kMaxRepeatCount)
						return null;
					var nd = CreateStandardSubstitution (NextChar ());
					if (nd != null) {
						while (repeatCount-- > 1) {
							PushNode (nd);
						}
						return nd;
					}
					return null;
				}
			}
		}

		Node CreateStandardSubstitution(char subst)
		{
			var kind = NodeKind.Structure; // most common
			string name = null;
			switch (subst) {
			case 'a': name = "Array"; break;
			case 'b': name = "Bool"; break;
			case 'c': name = "UnicodeScalar"; break;
			case 'd': name = "Double"; break;
			case 'f': name = "Float"; break;
			case 'i': name = "Int"; break;
			case 'V': name = "UnsafeRawPointer"; break;
			case 'v': name = "UnsafeMutableRawPointer"; break;
			case 'P': name = "UnsafePointer"; break;
			case 'p': name = "UnsafeMutablePointer"; break;
			case 'R': name = "UnsafeBufferPointer"; break;
			case 'r': name = "UnsafeMutableBufferPointer"; break;
			case 'S': name = "String"; break;
			case 'u': name = "UInt"; break;
			case 'q': name = "Optional"; kind = NodeKind.Enum; break;
			case 'Q': name = "ImplicitlyUnwrappedOptional"; kind = NodeKind.Enum; break;
			default: return null;
			}
			return CreateSwiftType (kind, name);
		}

		Node DemangleIdentifier()
		{
			var hasWordSubsts = false;
			var isPunycoded = false;
			var c = PeekChar ();
			if (!Char.IsDigit (c))
				return null;
			if (c == '0') {
				NextChar ();
				if (PeekChar() == '0') {
					NextChar ();
					isPunycoded = true;
				} else {
					hasWordSubsts = true;
				}
			}
			var identifier = new StringBuilder ();
			do {
				while (hasWordSubsts && Char.IsLetter (PeekChar ())) {
					var cc = NextChar ();
					var wordIdx = 0;
					if (Char.IsLower (cc)) {
						wordIdx = cc - 'a';
					} else {
						wordIdx = cc - 'A';
						hasWordSubsts = false;
					}
					if (wordIdx >= words.Count)
						return null;
					string piece0 = words [wordIdx];
					identifier.Append (piece0);
				}
				if (NextIf ('0'))
					break;
				var numChars = DemangleNatural ();
				if (numChars <= 0)
					return null;
				if (isPunycoded)
					NextIf ('_');
				if (numChars > slice.Length)
					return null;
				string piece = slice.Substring (slice.Position, numChars);
				if (isPunycoded) {
					PunyCode puny = new PunyCode ();
					string punyCodedString = puny.Decode (piece);
					identifier.Append (punyCodedString);
				} else {
					identifier.Append (piece);
					var wordStartPos = -1;
					for (int idx = 0; idx <= piece.Length; idx++) {
						char ccc = idx < piece.Length ? piece [idx] : (char)0;
						if (wordStartPos >= 0 && IsWordEnd (ccc, piece [idx - 1])) {
							if (idx - wordStartPos >= 2 && words.Count < 26) {
								var word = piece.Substring (wordStartPos, idx - wordStartPos);
								words.Add (word);
							}
							wordStartPos = -1;
						}
						if (wordStartPos < 0 && IsWordStart(ccc)) {
							wordStartPos = idx;
						}
					}
				}
				slice.Advance (numChars);
			} while (hasWordSubsts);
			if (identifier.Length == 0)
				return null;
			var ident = new Node (NodeKind.Identifier, identifier.ToString());
			AddSubstitution (ident);
			return ident;
		}

		bool IsWordStart(char ch) {
			return !Char.IsDigit (ch) && ch != '_' && ch != (char)0;
		}

		bool IsWordEnd(char ch, char prevCh)
		{
			if (ch == '_' || ch == (char)0)
				return true;
			if (!Char.IsUpper (prevCh) && Char.IsUpper (ch))
				return true;
			return false;
		}
		Node DemangleOperatorIdentifier()
		{
			var ident = PopNode (NodeKind.Identifier);
			if (ident == null)
				return null;
			var op_char_table = "& @/= >    <*!|+?%-~   ^ .";
			StringBuilder opStr = new StringBuilder ();
			foreach (var c in ident.Text) {
				if (c > 0x7f) {
					opStr.Append (c);
					continue;
				}
				if (!Char.IsLower (c))
					return null;
				char o = op_char_table [c - 'a'];
				if (o == ' ')
					return null;
				opStr.Append (o);
			}
			switch (NextChar()) {
			case 'i': return new Node (NodeKind.InfixOperator, opStr.ToString ());
			case 'p': return new Node (NodeKind.PrefixOperator, opStr.ToString ());
			case 'P': return new Node (NodeKind.PostfixOperator, opStr.ToString ());
			default: return null;
			}
		}

		Node PopTypeList()
		{
			var root = new Node (NodeKind.TypeList);
			if (PopNode(NodeKind.EmptyList) == null) {
				bool firstElem = false;
				do {
					firstElem = PopNode (NodeKind.FirstElementMarker) != null;
					var ty = PopNode (NodeKind.Type);
					if (ty == null)
						return null;
					root.Children.Add (ty);
				} while (!firstElem);
				root.ReverseChildren ();
			}
			return root;
		}

		Node PopProtocol()
		{
			var name = PopNode (Node.IsDeclName);
			var ctx = PopContext ();
			var proto = CreateWithChildren (NodeKind.Protocol, ctx, name);
			return CreateType (proto);
		}

		Node DemangleBoundGenericType()
		{
			var typeListList = new List<Node> (4);
			while (true) {
				var tlist = new Node (NodeKind.TypeList);
				typeListList.Add (tlist);
				Node ty = null;
				while ((ty = PopNode(NodeKind.Type)) != null) {
					tlist.Children.Add (ty);
				}
				tlist.ReverseChildren ();
				if (PopNode (NodeKind.EmptyList) != null)
					break;
				if (PopNode (NodeKind.FirstElementMarker) == null)
					return null;
			}
			var nominal = PopTypeAndGetNominal ();
			var nty = CreateType (DemangleBoundGenericArgs (nominal, typeListList, 0));
			AddSubstitution (nty);
			return nty;
		}

		Node DemangleBoundGenericArgs(Node nominal, List<Node> typeLists, int typeListIdx)
		{
			if (nominal == null || nominal.Children.Count < 2)
				return null;

			if (typeListIdx >= typeLists.Count)
				return null;
			var args = typeLists [typeListIdx++];

			var context = nominal.Children [0];

			if (typeListIdx < typeLists.Count) {
				Node boundParent = null;
				if (context.Kind == NodeKind.Extension) {
					boundParent = DemangleBoundGenericArgs (context.Children [1], typeLists, typeListIdx);
					boundParent = CreateWithChildren (NodeKind.Extension, context.Children [0], boundParent);

					if (context.Children.Count == 3) {
						AddChild (boundParent, context.Children [2]);
					}

				} else {
					boundParent = DemangleBoundGenericArgs (context, typeLists, typeListIdx);
				}
				nominal = CreateWithChildren (nominal.Kind, boundParent, nominal.Children [1]);
				if (nominal == null)
					return null;
			}

			if (args.Children.Count == 0)
				return nominal;

			NodeKind kind = NodeKind.Type; // arbitrary
			switch (nominal.Kind) {
			case NodeKind.Class:
				kind = NodeKind.BoundGenericClass;
				break;
			case NodeKind.Structure:
				kind = NodeKind.BoundGenericStructure;
				break;
			case NodeKind.Enum:
				kind = NodeKind.BoundGenericEnum;
				break;
			default:
				return null;
			}
			return CreateWithChildren (kind, CreateType (nominal), args);
		}

		Node DemangleImplParamConvention()
		{
			string attr = null;
			switch (NextChar ()) {
			case 'i': attr = "@in"; break;
			case 'c': attr = "@in_constant"; break;
			case 'l': attr = "@inout"; break;
			case 'b': attr = "@inout_aliasable"; break;
			case 'n': attr = "@in_guaranteed"; break;
			case 'x': attr = "@owned"; break;
			case 'g': attr = "@guaranteed"; break;
			case 'e': attr = "@deallocating"; break;
			case 'y': attr = "@unowned"; break;
			default:
				PushBack ();
				return null;
			}
			return CreateWithChild (NodeKind.ImplParameter, new Node (NodeKind.ImplConvention, attr));
		}

		Node DemangleImplResultConvention(NodeKind convKind)
		{
			string attr = null;
			switch (NextChar ()) {
			case 'r': attr = "@out"; break;
			case 'o': attr = "@owned"; break;
			case 'd': attr = "@unowned"; break;
			case 'u': attr = "@unowned_inner_pointer"; break;
			case 'a': attr = "@autoreleased"; break;
			default:
				PushBack ();
				return null;
			}
			return CreateWithChild (convKind, new Node (NodeKind.ImplConvention, attr));
		}

		Node DemangleImplFunctionType()
		{
			var type = new Node (NodeKind.ImplFunctionType);
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			if (genSig != null && NextIf ('P'))
				genSig = ChangeKind (genSig, NodeKind.DependentPseudogenericSignature);

			string cAttr = null;
			switch (NextChar()) {
			case 'y': cAttr = "@callee_unowned"; break;
			case 'g': cAttr = "@callee_guaranteed"; break;
			case 'x': cAttr = "@callee_owned"; break;
			case 't': cAttr = "@convention(thin)"; break;
			default:
				return null;
			}
			type.Children.Add (new Node (NodeKind.ImplConvention, cAttr));

			string fAttr = null;
			switch (NextChar ()) {
			case 'B': fAttr = "@convention(block)"; break;
			case 'C': fAttr = "@convention(c)"; break;
			case 'M': fAttr = "@convention(method)"; break;
			case 'O': fAttr = "@convention(objc_method)"; break;
			case 'K': fAttr = "@convention(closure)"; break;
			case 'W': fAttr = "@convention(witness_method)"; break;
			default:
				PushBack ();
				break;
			}
			if (fAttr != null)
				type.Children.Add (new Node (NodeKind.ImplFunctionAttribute, fAttr));

			AddChild (type, genSig);

			int numTypesToAdd = 0;
			Node parameter = null;
			while ((parameter = DemangleImplParamConvention()) != null) {
				type = AddChild (type, parameter);
				numTypesToAdd++;
			}
			Node result = null;
			while ((result = DemangleImplResultConvention(NodeKind.ImplResult)) != null) {
				type = AddChild (type, result);
				numTypesToAdd++;
			}
			if (NextIf('z')) {
				var errorResult = DemangleImplResultConvention (NodeKind.ImplErrorResult);
				if (errorResult == null)
					return null;
				type = AddChild (type, errorResult);
				numTypesToAdd++;
			}
			if (!NextIf ('_'))
				return null;

			for (int idx = 0; idx < numTypesToAdd; idx++) {
				var convTy = PopNode (NodeKind.Type);
				if (convTy == null)
					return null;
				type.Children [type.Children.Count - idx - 1].Children.Add (convTy);
			}
			return CreateType (type);
		}

		Node DemangleMetatype()
		{
			switch (NextChar ()) {
			case 'f':
				return CreateWithPoppedType (NodeKind.FullTypeMetadata);
			case 'P':
				return CreateWithPoppedType (NodeKind.GenericTypeMetadataPattern);
			case 'a':
				return CreateWithPoppedType (NodeKind.TypeMetadataAccessFunction);
			case 'L':
				return CreateWithPoppedType (NodeKind.TypeMetadataLazyCache);
			case 'm':
				return CreateWithPoppedType (NodeKind.Metaclass);
			case 'n':
				return CreateWithPoppedType (NodeKind.NominalTypeDescriptor);
			case 'p':
				return CreateWithChild (NodeKind.ProtocolDescriptor, PopProtocol ());
			case 'B':
				return CreateWithChild (NodeKind.ReflectionMetadataBuiltinDescriptor, PopNode (NodeKind.Type));
			case 'F':
				return CreateWithChild (NodeKind.ReflectionMetadataFieldDescriptor, PopNode (NodeKind.Type));
			case 'A':
				return CreateWithChild (NodeKind.ReflectionMetadataAssocTypeDescriptor, PopProtocolConformance ());
			case 'C': {
					var ty = PopNode (NodeKind.Type);
					if (ty == null || !Node.IsNominal (ty.Children [0].Kind))
						return null;
					return CreateWithChild (NodeKind.ReflectionMetadataSuperclassDescriptor, ty.Children [0]);
				}
			default:
				return null;
			}
		}

		Node DemangleArchetype()
		{
			switch (NextChar ()) {
			case 'a': {
					var ident = PopNode (NodeKind.Identifier);
					var archeTy = PopTypeAndGetChild ();
					var assocTy = CreateType (CreateWithChildren (NodeKind.AssociatedTypeRef, archeTy, ident));
					AddSubstitution (assocTy);
					return assocTy;
				}
			case 'y': {
					var T = DemangleAssociatedTypeSimple (DemangleGenericParamIndex ());
					AddSubstitution (T);
					return T;
				}
			case 'z': {
					var T = DemangleAssociatedTypeSimple (GetDependentGenericParamType (0, 0));
					AddSubstitution (T);
					return T;
				}
			case 'Y': {
					var T = DemangleAssociatedTypeCompound (DemangleGenericParamIndex ());
					AddSubstitution (T);
					return T;
				}
			case 'Z': {
					var T = DemangleAssociatedTypeCompound (GetDependentGenericParamType (0, 0));
					AddSubstitution (T);
					return T;
				}

			default:
				return null;
			}
		}

		Node DemangleAssociatedTypeSimple(Node genericParamIndex) {
			var GPI = CreateType (genericParamIndex);
			var atName = PopAssocTypeName ();
			return CreateType (CreateWithChildren (NodeKind.DependentMemberType, GPI, atName));
		}

		Node DemangleAssociatedTypeCompound(Node genericParamIdx)
		{
			var assocTypeNames = new List<Node> (4);
			bool firstElem = false;
			do {
				firstElem = PopNode (NodeKind.FirstElementMarker) != null;
				var assocTyName = PopAssocTypeName ();
				if (assocTyName == null)
					return null;
				assocTypeNames.Add (assocTyName);
			} while (!firstElem);

			var baseClass = genericParamIdx;

			for (int index = assocTypeNames.Count - 1; index >= 0; index--) {
				var assocTy = assocTypeNames [index];
				var depTy = new Node (NodeKind.DependentMemberType);
				depTy = AddChild (depTy, CreateType (baseClass));
				baseClass = AddChild (depTy, assocTy);
			}
			return CreateType (baseClass);
		}



		Node PopAssocTypeName()
		{
			var proto = PopNode (NodeKind.Type);
			if (proto != null && proto.Children [0].Kind != NodeKind.Protocol)
				return null;

			var id = PopNode (NodeKind.Identifier);
			var assocTy = ChangeKind (id, NodeKind.DependentAssociatedTypeRef);
			AddChild (assocTy, proto);
			return assocTy;
		}

		Node GetDependentGenericParamType(int depth, int index)
		{
			if (depth < 0 || index < 0)
				return null;

			StringBuilder name = new StringBuilder ();
			int idxChar = index;
			do {
				name.Append ((char)('A' + (idxChar % 26)));
				idxChar /= 26;
			} while (idxChar != 0);
			if (depth != 0)
				name.Append (depth);
			var paramTy = new Node (NodeKind.DependentGenericParamType, name.ToString ());
			paramTy.Children.Add (new Node (NodeKind.Index, depth));
			paramTy.Children.Add (new Node (NodeKind.Index, index));
			return paramTy;
		}

		Node DemangleGenericParamIndex()
		{
			if (NextIf('d')) {
				var depth = DemangleIndex () + 1;
				var index = DemangleIndex ();
				return GetDependentGenericParamType (depth, index);
			}
			if (NextIf('z')) {
				return GetDependentGenericParamType (0, 0);
			}
			return GetDependentGenericParamType (0, DemangleIndex () + 1);
		}
		

		Node PopProtocolConformance()
		{
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			var module = PopModule ();
			var proto = PopProtocol ();
			var type = PopNode (NodeKind.Type);
			Node ident = null;
			if (type == null) {
				ident = PopNode (NodeKind.Identifier);
				type = PopNode (NodeKind.Type);
			}
			if (genSig != null) {
				type = CreateType (CreateWithChildren (NodeKind.DependentGenericType, genSig, type));
			}
			var conf = CreateWithChildren (NodeKind.ProtocolConformance, type, proto, module);
			AddChild (conf, ident);
			return conf;
		}

		Node DemangleThunkOrSpecialization()
		{
			char c = NextChar ();
			switch (c) {
			case 'c': return CreateWithChild (NodeKind.CurryThunk, PopNode (Node.IsEntity));
			case 'o': return new Node (NodeKind.ObjCAttribute);
			case 'O': return new Node (NodeKind.NonObjCAttribute);
			case 'D': return new Node (NodeKind.DynamicAttribute);
			case 'd': return new Node (NodeKind.DirectMethodReferenceAttribute);
			case 'a': return new Node (NodeKind.PartialApplyObjCForwarder);
			case 'A': return new Node (NodeKind.PartialApplyForwarder);
			case 'm': return new Node (NodeKind.MergedFunction);
			case 'V': {
					var baseClass = PopNode (Node.IsEntity);
					var derived = PopNode (Node.IsEntity);
					return CreateWithChildren (NodeKind.VTableThunk, derived, baseClass);
				}
			case 'W': {
					var entity = PopNode (Node.IsEntity);
					var conf = PopProtocolConformance ();
					return CreateWithChildren (NodeKind.ProtocolWitness, conf, entity);
				}
			case 'R':
			case 'r': {
					var thunk = new Node (c == 'R' ? NodeKind.ReabstractionThunkHelper : NodeKind.ReabstractionThunk);
					var genSig = PopNode (NodeKind.DependentGenericSignature);
					if (genSig != null) {
						AddChild (thunk, genSig);
					}
					var ty2 = PopNode (NodeKind.Type);
					thunk = AddChild (thunk, PopNode (NodeKind.Type));
					return AddChild (thunk, ty2);

				}
			case 'g': return DemangleGenericSpecialization (NodeKind.GenericSpecialization);
			case 'G': return DemangleGenericSpecialization (NodeKind.GenericSpecializationNotReAbstracted);
			case 'p': {
					var spec = DemangleSpecAttributes (NodeKind.GenericPartialSpecialization);
					var parameter = CreateWithChild (NodeKind.GenericSpecialization, PopNode (NodeKind.Type));
					return AddChild (spec, parameter);
				}
			case 'P': {
					var spec = DemangleSpecAttributes (NodeKind.GenericPartialSpecializationNotReAbstracted);
					var parameter = CreateWithChild (NodeKind.GenericSpecialization, PopNode (NodeKind.Type));
					return AddChild (spec, parameter);
				}
			case 'f': return DemangleFunctionSpecialization ();
			case 'K':
			case 'k': {
					var nodeKind = c == 'K' ? NodeKind.KeyPathGetterThunkHelper : NodeKind.KeyPathSetterThunkHelper;
					var types = new List<Node> ();
					var node = PopNode ();
					if (node == null || node.Kind != NodeKind.Type)
						return null;
					do {
						types.Add (node);
						node = PopNode ();
					} while (node != null && node.Kind == NodeKind.Type);
					Node result = null;
					if (node != null) {
						if (node.Kind == NodeKind.DependentGenericSignature) {
							var decl = PopNode ();
							result = CreateWithChildren (nodeKind, decl, node);
						} else {
							result = CreateWithChild (nodeKind, node);
						}
					} else {
						return null;
					}
					foreach (var i in types) {
						result.Children.Add (i);
					}
					return result;
				}
			case 'H':
			case 'h': {
					var nodeKind = c == 'H' ? NodeKind.KeyPathEqualsThunkHelper : NodeKind.KeyPathHashThunkHelper;
					Node genericSig = null;
					var types = new List<Node> ();
					var node = PopNode ();
					if (node != null) {
						if (node.Kind == NodeKind.DependentGenericSignature) {
							genericSig = node;
						} else if (node.Kind == NodeKind.Type) {
							types.Add (node);
						} else {
							return null;
						}
					} else {
						return null;
					}

					while ((node = PopNode ()) != null) {
						if (node.Kind != NodeKind.Type) {
							return null;
						}
						types.Add (node);
					}

					var result = new Node (nodeKind);
					foreach (var i in types) {
						result.Children.Add (i);
					}
					if (genericSig != null)
						result.Children.Add (genericSig);
					return result;
				}
			default:
				return null;

			}
		}

		Node DemangleGenericSpecialization(NodeKind specKind)
		{
			var spec = DemangleSpecAttributes (specKind);
			if (spec == null)
				return null;
			var typeList = PopTypeList ();
			if (typeList == null)
				return null;
			foreach (var ty in typeList.Children) {
				spec.Children.Add (CreateWithChild (NodeKind.GenericSpecializationParam, ty));
			}
			return spec;
		}

		Node DemangleFunctionSpecialization()
		{
			var spec = DemangleSpecAttributes (NodeKind.FunctionSignatureSpecialization, true);
			uint parmIdx = 0;
			while (spec != null && !NextIf('_')) {
				spec = AddChild (spec, DemangleFuncSpecParam (parmIdx));
				parmIdx++;
			}
			if (!NextIf ('n'))
				spec = AddChild (spec, DemangleFuncSpecParam (~(ulong)0));

			if (spec == null)
				return null;

			for (int idx = 0, num = spec.Children.Count; idx < num; idx++) {
				var param = spec.Children [num - idx - 1];
				if (param.Kind != NodeKind.FunctionSignatureSpecializationParam)
					continue;

				if (param.Children.Count == 0)
					continue;
				var kindNd = param.Children [0];
				var paramKind = (FunctionSigSpecializationParamKind)kindNd.Index;

				switch (paramKind) {
				case FunctionSigSpecializationParamKind.ConstantPropFunction:
				case FunctionSigSpecializationParamKind.ConstantPropGlobal:
				case FunctionSigSpecializationParamKind.ConstantPropString:
				case FunctionSigSpecializationParamKind.ClosureProp: {
						int fixedChildren = param.Children.Count;
						Node ty = null;
						while ((ty = PopNode(NodeKind.Type)) != null) {
							if (paramKind != FunctionSigSpecializationParamKind.ClosureProp)
								return null;
							param = AddChild (param, ty);
						}
						var name = PopNode (NodeKind.Identifier);
						if (name == null)
							return null;
						string text = name.Text;
						if (paramKind == FunctionSigSpecializationParamKind.ConstantPropString &&
						    text.Length > 0 && text[0] == '_') {
							text = text.Substring (1);
						}
						AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamPayload, text));
						param.ReverseChildren (fixedChildren);
						break;
					}
				default:
					break;
				}
			}
			return spec;
		}

		Node DemangleFuncSpecParam(ulong paramIdx)
		{
			var param = new Node (NodeKind.FunctionSignatureSpecializationParam, (long)paramIdx);
			switch (NextChar ()) {
			case 'n':
				return param;
			case 'c':
				return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
								(long)FunctionSigSpecializationParamKind.ClosureProp));
			case 'p':
				switch (NextChar ()) {
				case 'f':
					return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
									(long)FunctionSigSpecializationParamKind.ConstantPropFunction));
				case 'g':
					return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
									(long)FunctionSigSpecializationParamKind.ConstantPropGlobal));
				case 'i':
					return AddFuncSpecParamNumber (param, FunctionSigSpecializationParamKind.ConstantPropInteger);
				case 'd':
					return AddFuncSpecParamNumber (param, FunctionSigSpecializationParamKind.ConstantPropFloat);
				case 's': {
						string encoding = null;
						switch (NextChar ()) {
						case 'b': encoding = "u8"; break;
						case 'w': encoding = "u16"; break;
						case 'c': encoding = "objc"; break;
						default:
							return null;
						}
						AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
									 (long)FunctionSigSpecializationParamKind.ConstantPropString));
						return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamPayload, encoding));
					}
				default:
					return null;
				}
			case 'd': {
					uint value = (uint)FunctionSigSpecializationParamKind.Dead;
					if (NextIf ('G'))
						value |= (uint)FunctionSigSpecializationParamKind.OwnedToGuaranteed;

					if (NextIf ('X'))
						value |= (uint)FunctionSigSpecializationParamKind.SROA;
					return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind, value));
				}
			case 'g': {
					uint value = (uint)FunctionSigSpecializationParamKind.OwnedToGuaranteed;
					if (NextIf ('X'))
						value |= (uint)FunctionSigSpecializationParamKind.SROA;
					return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind, value));
				}
			case 'x':
				return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
								(long)FunctionSigSpecializationParamKind.SROA));
			case 'i':
				return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
								  (long)FunctionSigSpecializationParamKind.BoxToValue));
			case 's':
				return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
								  (long)FunctionSigSpecializationParamKind.BoxToStack));
			default:
				return null;
			}
		}

		Node AddFuncSpecParamNumber(Node param, FunctionSigSpecializationParamKind kind)
		{
			param.Children.Add (new Node (NodeKind.FunctionSignatureSpecializationParamKind, (long)kind));
			StringBuilder str = new StringBuilder ();
			while (Char.IsDigit(PeekChar())) {
				str.Append (NextChar ());
			}
			if (str.Length == 0)
				return null;
			return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamPayload, str.ToString()));
		}

		Node DemangleSpecAttributes(NodeKind specKind, bool demangleUniqueID = false)
		{
			bool isFragile = NextIf ('q');

			var passID = (int)NextChar () - '0';
			if (passID < 0 || passID > 9)
				return null;

			var idx = -1;
			if (demangleUniqueID)
				idx = DemangleNatural ();

			Node specNd = null;
			if (idx >= 0) {
				specNd = new Node (specKind, idx);
			} else {
				specNd = new Node (specKind);
			}
			if (isFragile)
				specNd.Children.Add (new Node (NodeKind.SpecializationIsFragile));
			specNd.Children.Add (new Node (NodeKind.SpecializationPassID, passID));
			return specNd;
		}

		Node DemangleWitness() 
		{
			switch (NextChar ()) {
			case 'V':
				return CreateWithChild (NodeKind.ValueWitnessTable, PopNode (NodeKind.Type));
			case 'v': {
					uint directness = 0;
					switch (NextChar ()) {
					case 'd': directness = (uint)Directness.Direct; break;
					case 'i': directness = (uint)Directness.Indirect; break;
					default: return null;
					}
					return CreateWithChildren (NodeKind.FieldOffset, new Node (NodeKind.Directness, (long)directness),
								  PopNode (Node.IsEntity));
				}
			case 'P':
				return CreateWithChild (NodeKind.ProtocolWitnessTable, PopProtocolConformance ());
			case 'G':
				return CreateWithChild (NodeKind.GenericProtocolWitnessTable, PopProtocolConformance ());
			case 'I':
				return CreateWithChild (NodeKind.GenericProtocolWitnessTableInstantiationFunction, PopProtocolConformance ());
			case 'l': {
					var conf = PopProtocolConformance ();
					var type = PopNode (NodeKind.Type);
					return CreateWithChildren (NodeKind.LazyProtocolWitnessTableAccessor, type, conf);
				}
			case 'L': {
					var conf = PopProtocolConformance ();
					var type = PopNode (NodeKind.Type);
					return CreateWithChildren (NodeKind.LazyProtocolWitnessTableCacheVariable, type, conf);
				}
			case 'a':
				return CreateWithChild (NodeKind.ProtocolWitnessTableAccessor, PopProtocolConformance ());
			case 't': {
					var name = PopNode (Node.IsDeclName);
					var conf = PopProtocolConformance ();
					return CreateWithChildren (NodeKind.AssociatedTypeMetadataAccessor, conf, name);
				}
			case 'T': {
					var protoTy = PopNode (NodeKind.Type);
					var name = PopNode (Node.IsDeclName);
					var conf = PopProtocolConformance ();
					return CreateWithChildren (NodeKind.AssociatedTypeMetadataAccessor, conf, name, protoTy);
				}
			case 'y': {
					return CreateWithChild (NodeKind.OutlinedCopy, PopNode (NodeKind.Type));
				}
			case 'e': {
					return CreateWithChild (NodeKind.OutlinedConsume, PopNode (NodeKind.Type));
				}
			case 'r': {
					return CreateWithChild (NodeKind.OutlinedRetain, PopNode (NodeKind.Type));
				}
			case 's': {
					return CreateWithChild (NodeKind.OutlinedRelease, PopNode (NodeKind.Type));
				}
			default:
				return null;
			}
		}

		Node DemangleSpecialType()
		{
			var specialChar = NextChar ();
			switch (specialChar) {
			case 'f':
				return PopFunctionType (NodeKind.ThinFunctionType);
			case 'K':
				return PopFunctionType (NodeKind.AutoClosureType);
			case 'U':
				return PopFunctionType (NodeKind.UncurriedFunctionType);
			case 'B':
				return PopFunctionType (NodeKind.ObjCBlock);
			case 'C':
				return PopFunctionType (NodeKind.CFunctionPointer);
			case 'o':
				return CreateType (CreateWithChild (NodeKind.Unowned, PopNode (NodeKind.Type)));
			case 'u':
				return CreateType (CreateWithChild (NodeKind.Unmanaged, PopNode (NodeKind.Type)));
			case 'w':
				return CreateType (CreateWithChild (NodeKind.Weak, PopNode (NodeKind.Type)));
			case 'b':
				return CreateType (CreateWithChild (NodeKind.SILBoxType, PopNode (NodeKind.Type)));
			case 'D':
				return CreateType (CreateWithChild (NodeKind.DynamicSelf, PopNode (NodeKind.Type)));
			case 'M': {
					var MTR = DemangleMetatypeRepresentation ();
					var type = PopNode (NodeKind.Type);
					return CreateType (CreateWithChildren (NodeKind.Metatype, MTR, type));
				}
			case 'm': {
					var MTR = DemangleMetatypeRepresentation ();
					var type = PopNode (NodeKind.Type);
					return CreateType (CreateWithChildren (NodeKind.ExistentialMetatype, MTR, type));
				}
			case 'p':
				return CreateType (CreateWithChild (NodeKind.ExistentialMetatype, PopNode (NodeKind.Type)));
			case 'c': {
					var superClass = PopNode (NodeKind.Type);
					var protocols = DemangleProtocolList ();
					return CreateType (CreateWithChildren (NodeKind.ProtocolListWithClass, protocols, superClass));
				}
			case 'l': {
					var protocols = DemangleProtocolList ();
					return CreateType (CreateWithChild (NodeKind.ProtocolListWithAnyObject, protocols));
				}
			case 'X':
			case 'x': {
					Node signature = null, genericArgs = null;
					if (specialChar == 'X') {
						signature = PopNode (NodeKind.DependentGenericSignature);
						if (signature == null)
							return null;
						genericArgs = PopTypeList ();
						if (genericArgs == null)
							return null;
					}

					var fieldTypes = PopTypeList ();
					if (fieldTypes == null)
						return null;

					var layout = new Node (NodeKind.SILBoxLayout);
					for (int i = 0; i < fieldTypes.Children.Count; i++) {
						var fieldType = fieldTypes.Children [i];
						var isMutable = false;
						if (fieldType.Children [0].Kind == NodeKind.InOut) {
							isMutable = true;
							fieldType = CreateType (fieldType.Children [0].Children [0]);
						}
						var field = new Node (isMutable ? NodeKind.SILBoxMutableField : NodeKind.SILBoxImmutableField);
						field.Children.Add (fieldType);
						layout.Children.Add (field);
					}
					var boxTy = new Node (NodeKind.SILBoxTypeWithLayout);
					boxTy.Children.Add (layout);
					if (signature != null) {
						boxTy.Children.Add (signature);
						boxTy.Children.Add (genericArgs);
					}
					return CreateType (boxTy);
				}
			case 'e':
				return CreateType (new Node (NodeKind.ErrorType));
			default:
				return null;
			}
		}

		Node DemangleMetatypeRepresentation()
		{
			switch (NextChar ()) {
			case 't':
				return new Node (NodeKind.MetatypeRepresentation, "@thin");
			case 'T':
				return new Node (NodeKind.MetatypeRepresentation, "@thick");
			case 'o':
				return new Node (NodeKind.MetatypeRepresentation, "@objc_metatype");
			default:
				return null;
			}
		}

		Node DemangleFunctionEntity()
		{
			var kind = NodeKind.EmptyList;
			var args = FunctionEntityArgs.None;
			switch (NextChar ()) {
			case 'D': args = FunctionEntityArgs.None; kind = NodeKind.Deallocator; break;
			case 'd': args = FunctionEntityArgs.None; kind = NodeKind.Destructor; break;
			case 'E': args = FunctionEntityArgs.None; kind = NodeKind.IVarDestroyer; break;
			case 'e': args = FunctionEntityArgs.None; kind = NodeKind.IVarInitializer; break;
			case 'i': args = FunctionEntityArgs.None; kind = NodeKind.Initializer; break;
			case 'C': args = FunctionEntityArgs.TypeAndMaybePrivateName; kind = NodeKind.Allocator; break;
			case 'c': args = FunctionEntityArgs.TypeAndMaybePrivateName; kind = NodeKind.Constructor; break;
			case 'g': args = FunctionEntityArgs.TypeAndName; kind = NodeKind.Getter; break;
			case 'G': args = FunctionEntityArgs.TypeAndName; kind = NodeKind.GlobalGetter; break;
			case 's': args = FunctionEntityArgs.TypeAndName; kind = NodeKind.Setter; break;
			case 'm': args = FunctionEntityArgs.TypeAndName; kind = NodeKind.MaterializeForSet; break;
			case 'w': args = FunctionEntityArgs.TypeAndName; kind = NodeKind.WillSet; break;
			case 'W': args = FunctionEntityArgs.TypeAndName; kind = NodeKind.DidSet; break;
			case 'a': {
					args = FunctionEntityArgs.TypeAndName;
					switch (NextChar ()) {
					case 'O': kind = NodeKind.OwningMutableAddressor; break;
					case 'o': kind = NodeKind.NativeOwningMutableAddressor; break;
					case 'P': kind = NodeKind.NativePinningMutableAddressor; break;
					case 'u': kind = NodeKind.UnsafeMutableAddressor; break;
					default: return null;
					}
				}
				break;
			case 'l': {
					args = FunctionEntityArgs.TypeAndName;
					switch (NextChar ()) {
					case 'O': kind = NodeKind.OwningAddressor; break;
					case 'o': kind = NodeKind.NativeOwningAddressor; break;
					case 'p': kind = NodeKind.NativePinningAddressor; break;
					case 'u': kind = NodeKind.UnsafeAddressor; break;
					default:
						return null;
					}
				}
				break;
			case 'U': args = FunctionEntityArgs.TypeAndIndex; kind = NodeKind.ExplicitClosure; break;
			case 'u': args = FunctionEntityArgs.TypeAndIndex; kind = NodeKind.ImplicitClosure; break;
			case 'A': args = FunctionEntityArgs.Index; kind = NodeKind.DefaultArgumentInitializer; break;
			case 'p': return DemangleEntity (NodeKind.GenericTypeParamDecl);
			default:
				return null;

			}

			Node child1 = null, child2 = null;
			switch (args) {
			case FunctionEntityArgs.None:
				break;
			case FunctionEntityArgs.Type:
				child1 = PopNode (NodeKind.Type);
				break;
			case FunctionEntityArgs.TypeAndName:
				child2 = PopNode (NodeKind.Type);
				child1 = PopNode (Node.IsDeclName);
				break;
			case FunctionEntityArgs.TypeAndMaybePrivateName:
				child1 = PopNode (NodeKind.PrivateDeclName);
				child2 = PopNode (NodeKind.Type);
				break;
			case FunctionEntityArgs.TypeAndIndex:
				child1 = DemangleIndexAsNode ();
				child2 = PopNode (NodeKind.Type);
				break;
			case FunctionEntityArgs.Index:
				child1 = DemangleIndexAsNode ();
				break;
			}
			var entity = CreateWithChild (kind, PopContext ());
			switch (args) {
			case FunctionEntityArgs.None:
				break;
			case FunctionEntityArgs.Type:
			case FunctionEntityArgs.Index:
				entity = AddChild (entity, child1);
				break;
			case FunctionEntityArgs.TypeAndMaybePrivateName:
				if (child1 != null)
					entity = AddChild (entity, child1);
				entity = AddChild (entity, child2);
				break;
			case FunctionEntityArgs.TypeAndName:
			case FunctionEntityArgs.TypeAndIndex:
				entity = AddChild (entity, child1);
				entity = AddChild (entity, child2);
				break;
			}
			return entity;
		}

		Node DemangleEntity(NodeKind kind)
		{
			var type = PopNode (NodeKind.Type);
			var name = PopNode (Node.IsDeclName);
			var context = PopContext ();
			return CreateWithChildren (kind, context, name, type);
		}

		Node DemangleProtocolList()
		{
			var typeList = new Node (NodeKind.TypeList);
			var protoList = CreateWithChild (NodeKind.ProtocolList, typeList);
			if (PopNode(NodeKind.EmptyList) == null) {
				bool firstElem = false;
				do {
					firstElem = PopNode (NodeKind.FirstElementMarker) != null;
					var proto = PopProtocol ();
					if (proto == null)
						return null;
					typeList.Children.Add (proto);
				} while (!firstElem);
				typeList.ReverseChildren ();
			}
			return protoList;
		}

		Node DemangleProtocolListType()
		{
			var protoList = DemangleProtocolList ();
			return CreateType (protoList);
		}

		Node DemangleGenericSignature(bool hasParamCounts)
		{
			var sig = new Node (NodeKind.DependentGenericSignature);
			if (hasParamCounts) {
				while (!NextIf('l')) {
					var count = 0;
					if (!NextIf ('z'))
						count = DemangleIndex () + 1;
					if (count < 0)
						return null;
					sig.Children.Add (new Node (NodeKind.DependentGenericParamCount, count));
				}
			} else {
				sig.Children.Add (new Node (NodeKind.DependentGenericParamCount, 1L));
			}
			if (sig.Children.Count == 0)
				return null;
			var numCounts = sig.Children.Count;
			Node req = null;
			while ((req = PopNode(Node.IsRequirement)) != null) {
				sig.Children.Add (req);
			}
			sig.ReverseChildren (numCounts);
			return sig;
		}

		Node DemangleGenericRequirement()
		{
			GenericTypeKind typeKind = GenericTypeKind.Assoc;
			GenericConstraintKind constraintKind = GenericConstraintKind.BaseClass;

			switch (NextChar ()) {
			case 'c': constraintKind = GenericConstraintKind.BaseClass; typeKind = GenericTypeKind.Assoc; break;
			case 'C': constraintKind = GenericConstraintKind.BaseClass; typeKind = GenericTypeKind.CompoundAssoc; break;
			case 'b': constraintKind = GenericConstraintKind.BaseClass; typeKind = GenericTypeKind.Generic; break;
			case 'B': constraintKind = GenericConstraintKind.BaseClass; typeKind = GenericTypeKind.Substitution; break;
			case 't': constraintKind = GenericConstraintKind.SameType; typeKind = GenericTypeKind.Assoc; break;
			case 'T': constraintKind = GenericConstraintKind.SameType; typeKind = GenericTypeKind.CompoundAssoc; break;
			case 's': constraintKind = GenericConstraintKind.SameType; typeKind = GenericTypeKind.Generic; break;
			case 'S': constraintKind = GenericConstraintKind.SameType; typeKind = GenericTypeKind.Substitution; break;
			case 'm': constraintKind = GenericConstraintKind.Layout; typeKind = GenericTypeKind.Assoc; break;
			case 'M': constraintKind = GenericConstraintKind.Layout; typeKind = GenericTypeKind.CompoundAssoc; break;
			case 'l': constraintKind = GenericConstraintKind.Layout; typeKind = GenericTypeKind.Generic; break;
			case 'L': constraintKind = GenericConstraintKind.Layout; typeKind = GenericTypeKind.Substitution; break;
			case 'p': constraintKind = GenericConstraintKind.Protocol; typeKind = GenericTypeKind.Assoc; break;
			case 'P': constraintKind = GenericConstraintKind.Protocol; typeKind = GenericTypeKind.CompoundAssoc; break;
			case 'Q': constraintKind = GenericConstraintKind.Protocol; typeKind = GenericTypeKind.Substitution; break;
			default: constraintKind = GenericConstraintKind.Protocol; typeKind = GenericTypeKind.Generic; PushBack (); break;
			}

			Node constrTy = null;
			switch (typeKind) {
			case GenericTypeKind.Generic:
				constrTy = CreateType (DemangleGenericParamIndex ());
				break;
			case GenericTypeKind.Assoc:
				constrTy = DemangleAssociatedTypeSimple (DemangleGenericParamIndex ());
				AddSubstitution (constrTy);
				break;
			case GenericTypeKind.CompoundAssoc:
				constrTy = DemangleAssociatedTypeCompound (DemangleGenericParamIndex ());
				AddSubstitution (constrTy);
				break;
			case GenericTypeKind.Substitution:
				constrTy = PopNode (NodeKind.Type);
				break;
			}

			switch (constraintKind) {
			case GenericConstraintKind.Protocol:
				return CreateWithChildren (NodeKind.DependentGenericConformanceRequirement, constrTy, PopProtocol ());
			case GenericConstraintKind.BaseClass:
				return CreateWithChildren (NodeKind.DependentGenericConformanceRequirement, constrTy, PopNode (NodeKind.Type));
			case GenericConstraintKind.SameType:
				return CreateWithChildren (NodeKind.DependentGenericSameTypeRequirement, constrTy, PopNode (NodeKind.Type));
			case GenericConstraintKind.Layout: {
					var c = NextChar ();
					Node size = null;
					Node alignment = null;
					string name = null;
					if (c == 'U') {
						name = "U";
					} else if (c == 'R') {
						name = "R";
					} else if (c == 'N') {
						name = "N";
					} else if (c == 'C') {
						name = "C";
					} else if (c == 'D') {
						name = "D";
					} else if (c == 'T') {
						name = "T";
					} else if (c == 'E') {
						size = DemangleIndexAsNode ();
						if (size == null)
							return null;
						name = "E";
					} else if (c == 'e') {
						size = DemangleIndexAsNode ();
						if (size == null)
							return null;
						name = "e";
					} else if (c == 'M') {
						size = DemangleIndexAsNode ();
						if (size == null)
							return null;
						alignment = DemangleIndexAsNode ();
						name = "M";
					} else if (c == 'm') {
						size = DemangleIndexAsNode ();
						if (size == null)
							return null;
						name = "m";
					} else {
						return null;
					}

					var nameNode = new Node (NodeKind.Identifier, name);
					var layoutRequirement = CreateWithChildren (NodeKind.DependentGenericLayoutRequirement, constrTy, nameNode);
					if (size != null)
						AddChild (layoutRequirement, size);
					if (alignment != null)
						AddChild (layoutRequirement, alignment);
					return layoutRequirement;
				}
			}
			return null;
		}

		Node DemangleGenericType()
		{
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			var ty = PopNode (NodeKind.Type);
			return CreateType (CreateWithChildren (NodeKind.DependentGenericType, genSig, ty));
		}

		static int DecodeValueWitnessKind(string codeStr)
		{
			switch (codeStr) {
			case "al": return (int)ValueWitnessKind.AllocateBuffer;
			case "ca": return (int)ValueWitnessKind.AssignWithCopy;
			case "ta": return (int)ValueWitnessKind.AssignWithTake;
			case "de": return (int)ValueWitnessKind.DeallocateBuffer;
			case "xx": return (int)ValueWitnessKind.Destroy;
			case "XX": return (int)ValueWitnessKind.DestroyBuffer;
			case "Xx": return (int)ValueWitnessKind.DestroyArray;
			case "CP": return (int)ValueWitnessKind.InitializeBufferWithCopyOfBuffer;
			case "Cp": return (int)ValueWitnessKind.InitializeBufferWithCopy;
			case "cp": return (int)ValueWitnessKind.InitializeWithCopy;
			case "Tk": return (int)ValueWitnessKind.InitializeBufferWithTake;
			case "tk": return (int)ValueWitnessKind.InitializeWithTake;
			case "pr": return (int)ValueWitnessKind.ProjectBuffer;
			case "TK": return (int)ValueWitnessKind.InitializeBufferWithTakeOfBuffer;
			case "Cc": return (int)ValueWitnessKind.InitializeArrayWithCopy;
			case "Tt": return (int)ValueWitnessKind.InitializeArrayWithTakeFrontToBack;
			case "tT": return (int)ValueWitnessKind.InitializeArrayWithTakeBackToFront;
			case "xs": return (int)ValueWitnessKind.StoreExtraInhabitant;
			case "xg": return (int)ValueWitnessKind.GetExtraInhabitantIndex;
			case "ug": return (int)ValueWitnessKind.GetEnumTag;
			case "up": return (int)ValueWitnessKind.DestructiveProjectEnumData;
			case "ui": return (int)ValueWitnessKind.DestructiveInjectEnumTag;
			default:
				return -1;
			}
		}

		Node DemangleValueWitness()
		{
			char [] code = new char [2];
			code [0] = NextChar ();
			code [1] = NextChar ();
			var kind = DecodeValueWitnessKind (new string (code, 0, 2));
			if (kind < 0)
				return null;
			var vw = new Node (NodeKind.ValueWitness, (long)kind);
			return AddChild (vw, PopNode (NodeKind.Type));
		}
	}

}
