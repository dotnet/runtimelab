// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

// This is a port of the Apple Swift 5 demangler
namespace BindingsGeneration.Demangling {
	public class Swift5Demangler {
		const int kMaxRepeatCount = 2048;
		ulong offset;
		string originalIdentifier;
		Stack<Node> nodeStack = new Stack<Node> ();
		List<Node> substitutions = new List<Node> ();
		string text;
		List<string> words = new List<string> ();
		StringSlice slice;
		bool isOldFunctionTypeMangling = false;

		public Func<SymbolicReferenceKind, Directness, int, byte [], Node> SymbolicReferenceResolver { get; set; }

		static string [] prefixes = {
		    /*Swift 4*/   "_T0",
		    /*Swift 4.x*/ "$S", "_$S",
		    /*Swift 5+*/  "$s", "_$s"
    		};

		Swift5Demangler ()
		{
		}


		public Swift5Demangler (string mangledName, ulong offset = 0)
		{
			originalIdentifier = mangledName;
			this.offset = offset;
			slice = new StringSlice (originalIdentifier);
			slice.Advance (GetManglingPrefixLength (originalIdentifier));
		}

		public IReduction Run ()
		{
			Node topLevelNode = DemangleType (null);
			if (topLevelNode is not null && topLevelNode.IsAttribute() && nodeStack.Count >= 1) {
				var attribute = ExtractAttribute (topLevelNode);
				var nextReduction = Run ();
				if (nextReduction is not null && nextReduction is TypeSpecReduction ts) {
					ts.TypeSpec.Attributes.Add (attribute);
				}
				return nextReduction;
			} else if (topLevelNode is not null) {
				var reducer = new Swift5Reducer (originalIdentifier);
				return reducer.Convert (topLevelNode);
			} else {
				return new ReductionError () {Symbol = originalIdentifier,  Message = $"Unable to demangle {originalIdentifier}" };
			}
		}

		static TypeSpecAttribute ExtractAttribute (Node node)
		{
			switch (node.Kind) {
				case NodeKind.ObjCAttribute: return new TypeSpecAttribute ("ObjC");
				case NodeKind.DynamicAttribute: return new TypeSpecAttribute ("Dynamic");
				case NodeKind.NonObjCAttribute: return new TypeSpecAttribute ("NonObjC");
				case NodeKind.ImplFunctionAttribute: return new TypeSpecAttribute ("ImplFunction");
				case NodeKind.DirectMethodReferenceAttribute: return new TypeSpecAttribute ("DirectMethodReference");
				default:
					throw new NotSupportedException ($"{node.Kind} is not a supported attribute.");
			}
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

		Node CreateWithPoppedType (NodeKind kind)
		{
			return CreateWithChild (kind, PopNode (NodeKind.Type));
		}


		// beginning of swift 5 demangler port

		static bool IsDeclName (NodeKind kind)
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

		static bool IsContext (NodeKind node)
		{
			var type = typeof (NodeKind);
			var memInfo = type.GetMember (node.ToString ());
			return memInfo [0].GetCustomAttributes (typeof (ContextAttribute), false).Length > 0;
		}

		static bool IsAnyGeneric (NodeKind kind)
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

		static bool IsEntity (NodeKind kind)
		{
			if (kind == NodeKind.Type)
				return true;
			return IsContext (kind);
		}

		static bool IsRequirement (NodeKind kind)
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

		static bool IsFunctionAttr (NodeKind kind)
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

		int GetManglingPrefixLength (string mangledName)
		{
			if (string.IsNullOrEmpty (mangledName))
				return 0;
			foreach (var prefix in prefixes) {
				if (mangledName.StartsWith (prefix, StringComparison.Ordinal))
					return prefix.Length;
			}
			return 0;
		}

		bool IsSwiftSymbol (string mangledName)
		{
			if (IsOldFunctionTypeMangling (mangledName))
				return true;
			return GetManglingPrefixLength (mangledName) != 0;
		}

		bool IsObjCSymbol (string mangledName)
		{
			var nameWithoutPrefix = DropSwiftManglingPrefix (mangledName);
			return nameWithoutPrefix.StartsWith ("So", StringComparison.Ordinal) || nameWithoutPrefix.StartsWith ("Sc", StringComparison.Ordinal);
		}

		bool IsOldFunctionTypeMangling (string mangledName)
		{
			return mangledName.StartsWith ("_T", StringComparison.Ordinal);
		}

		string DropSwiftManglingPrefix (string mangledName)
		{
			return mangledName.Substring (GetManglingPrefixLength (mangledName));
		}

		static bool IsAliasNode (Node node)
		{
			switch (node.Kind) {
			case NodeKind.Type:
				return IsAliasNode (node.Children [0]);
			case NodeKind.TypeAlias:
				return true;
			default:
				return false;
			}
		}

		static bool IsAlias (string mangledName)
		{
			return IsAliasNode (new Swift5Demangler ().DemangleType (mangledName));
		}

		static bool IsClassNode (Node node)
		{
			switch (node.Kind) {
			case NodeKind.Type:
				return IsClassNode (node.Children [0]);
			case NodeKind.Class:
			case NodeKind.BoundGenericClass:
				return true;
			default:
				return false;
			}
		}

		static bool IsClass (string mangledName)
		{
			return IsClassNode (new Swift5Demangler ().DemangleType (mangledName));
		}

		static bool IsEnumNode (Node node)
		{
			switch (node.Kind) {
			case NodeKind.Type:
				return IsEnumNode (node.Children [0]);
			case NodeKind.Enum:
			case NodeKind.BoundGenericEnum:
				return true;
			default:
				return false;
			}
		}

		static bool IsEnum (string mangledName)
		{
			return IsEnumNode (new Swift5Demangler ().DemangleType (mangledName));
		}

		static bool IsProtocolNode (Node node)
		{
			switch (node.Kind) {
			case NodeKind.Type:
				return IsProtocolNode (node.Children [0]);
			case NodeKind.Protocol:
			case NodeKind.ProtocolSymbolicReference:
				return true;
			default:
				return false;
			}
		}

		static bool IsProtocol (string mangledName)
		{
			return IsProtocolNode (new Swift5Demangler ().DemangleType (mangledName));
		}

		static bool IsStructNode (Node node)
		{
			switch (node.Kind) {
			case NodeKind.Type:
				return IsStructNode (node.Children [0]);
			case NodeKind.Structure:
			case NodeKind.BoundGenericStructure:
				return true;
			default:
				return false;
			}
		}

		static bool IsStruct (string mangledName)
		{
			return IsStructNode (new Swift5Demangler ().DemangleType (mangledName));
		}




		void Clear ()
		{
			nodeStack.Clear ();
			substitutions.Clear ();
		}

		void Init (string mangledName)
		{
			nodeStack = new Stack<Node> ();
			substitutions = new List<Node> ();
			words = new List<string> ();
			if (mangledName != null) {
				text = mangledName;
				slice = new StringSlice (mangledName);
			}
		}

		public Node DemangleSymbol (string mangledName)
		{
			Init (mangledName);

			if (NextIf ("_Tt"))
				return DemangleObjCTypeName ();

			var prefixLength = GetManglingPrefixLength (mangledName);
			if (prefixLength == 0)
				return null;
			isOldFunctionTypeMangling = IsOldFunctionTypeMangling (mangledName);
			slice.Advance (prefixLength);

			if (!ParseAndPushNodes ())
				return null;

			var topLevel = new Node (NodeKind.Global);
			var parent = topLevel;
			Node funcAttr = null;
			while ((funcAttr = PopNode (IsFunctionAttr)) != null) {
				parent.AddChild (funcAttr);
				if (funcAttr.Kind == NodeKind.PartialApplyForwarder || funcAttr.Kind == NodeKind.PartialApplyObjCForwarder)
					parent = funcAttr;
				foreach (var nd in nodeStack) {
					switch (nd.Kind) {
					case NodeKind.Type:
						parent.AddChild (nd.Children [0]);
						break;
					default:
						parent.AddChild (nd);
						break;
					}
				}
			}
			if (topLevel.Children.Count == 0)
				return null;

			return topLevel;
		}

		Node DemangleType (string mangledName)
		{
			Init (mangledName);

			ParseAndPushNodes ();

			var result = PopNode ();
			if (result != null)
				return result;

			return new Node (NodeKind.Suffix, text);
		}

		bool ParseAndPushNodes ()
		{
			var idx = 0;
			while (!slice.IsAtEnd) {
				var node = DemangleOperator ();
				if (node == null)
					return false;
				PushNode (node);
				idx++;
			}
			return true;
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

		Node ChangeKind (Node node, NodeKind newKind)
		{
			Node newNode = null;
			if (node.HasText) {
				newNode = new Node (newKind, node.Text);
			} else if (node.HasIndex) {
				newNode = new Node (newKind, node.Index);
			} else {
				newNode = new Node (newKind);
			}
			newNode.Children.AddRange (node.Children);
			return newNode;
		}

		Node DemangleTypeMangling ()
		{
			var type = PopNode (NodeKind.Type);
			var labelList = PopFunctionParamLabels (type);
			var typeMangling = new Node (NodeKind.TypeMangling);

			AddChild (typeMangling, labelList);
			typeMangling = AddChild (typeMangling, type);
			return typeMangling;
		}

		Node DemangleSymbolicReference (int rawKind)
		{
			var data = new byte [4];
			for (int i = 0; i < 4; i++) {
				data [i] = (byte)NextChar ();
			}
			var value = BitConverter.ToInt32 (data, 0);

			SymbolicReferenceKind kind;
			Directness direct;
			switch (rawKind) {
			case 1:
				kind = SymbolicReferenceKind.Context;
				direct = Directness.Direct;
				break;
			case 2:
				kind = SymbolicReferenceKind.Context;
				direct = Directness.Indirect;
				break;
			default:
				return null;
			}

			Node resolved = null;
			if (SymbolicReferenceResolver != null) {
				resolved = SymbolicReferenceResolver (kind, direct, value, data);
			}
			if (resolved == null)
				return null;

			if (kind == SymbolicReferenceKind.Context)
				AddSubstitution (resolved);
			return resolved;
		}

		Node DemangleOperator ()
		{
			var c = NextChar ();
			switch (c) {
			case '\x01':
			case '\x02':
			case '\x03':
			case '\x04':
				return DemangleSymbolicReference (c);
			case 'A': return DemangleMultiSubstitutions ();
			case 'B': return DemangleBuiltinType ();
			case 'C': return DemangleAnyGenericType (NodeKind.Class);
			case 'D': return DemangleTypeMangling ();
			case 'E': return DemangleExtensionContext ();
			case 'F': return DemanglePlainFunction ();
			case 'G': return DemangleBoundGenericType ();
			case 'H':
				var c2 = NextChar ();
				switch (c2) {
				case 'A': return DemangleDependentProtocolConformanceAssociated ();
				case 'C': return DemangleConcreteProtocolConformance ();
				case 'D': return DemangleDependentProtocolConformanceRoot ();
				case 'I': return DemangleDependentProtocolConformanceInherited ();
				case 'P':
					return CreateWithChild (NodeKind.ProtocolConformanceRefInTypeModule, PopProtocol ());
				case 'p':
					return CreateWithChild (NodeKind.ProtocolConformanceRefInProtocolModule, PopProtocol ());
				default:
					PushBack ();
					PushBack ();
					return DemangleIdentifier ();
				}
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
			case 'Z': return CreateWithChild (NodeKind.Static, PopNode (IsEntity));
			case 'a': return DemangleAnyGenericType (NodeKind.TypeAlias);
			case 'c': return PopFunctionType (NodeKind.FunctionType);
			case 'd': return new Node (NodeKind.VariadicMarker);
			case 'f': return DemangleFunctionEntity ();
			case 'g': return DemangleRetroactiveConformance ();
			case 'h': return CreateType (CreateWithChild (NodeKind.Shared, PopTypeAndGetChild ()));
			case 'i': return DemangleSubscript ();
			case 'l': return DemangleGenericSignature (false);
			case 'm': return CreateType (CreateWithChild (NodeKind.Metatype, PopNode (NodeKind.Type)));
			case 'n': return CreateType (CreateWithChild (NodeKind.Owned, PopTypeAndGetChild ()));
			case 'o': return DemangleOperatorIdentifier ();
			case 'p': return DemangleProtocolListType ();
			case 'q': return CreateType (DemangleGenericParamIndex ());
			case 'r': return DemangleGenericSignature (true);
			case 's': return new Node (NodeKind.Module, "Swift");
			case 't': return PopTuple ();
			case 'u': return DemangleGenericType ();
			case 'v': return DemangleVariable ();
			case 'w': return DemangleValueWitness ();
			case 'x': return CreateType (GetDependentGenericParamType (0, 0));
			case 'y': return new Node (NodeKind.EmptyList);
			case 'z': return CreateType (CreateWithChild (NodeKind.InOut, PopTypeAndGetChild ()));
			case '_': return new Node (NodeKind.FirstElementMarker);
			case '.':
				PushBack ();
				return new Node (NodeKind.Suffix, slice.ConsumeRemaining ());
			default:
				PushBack ();
				return DemangleIdentifier ();
			}
		}

		int DemangleNatural ()
		{
			if (!Char.IsDigit (slice.Current))
				return -1000;
			var num = 0;
			while (true) {
				var c = slice.Current;
				if (!Char.IsDigit (c))
					return num;
				var newNum = (10 * num) + (c - '0');
				if (newNum < num)
					return -1000;
				num = newNum;
				NextChar ();
			}
		}

		int DemangleIndex ()
		{
			if (NextIf ('_'))
				return 0;
			var num = DemangleNatural ();
			if (num >= 0 && NextIf ('_'))
				return num + 1;
			return -1000;
		}


		Node DemangleIndexAsNode ()
		{
			var idx = DemangleIndex ();
			if (idx >= 0)
				return new Node (NodeKind.Number, idx);
			return null;
		}

		Node DemangleMultiSubstitutions ()
		{
			var repeatCount = 1;
			while (true) {
				if (slice.IsAtEnd)
					return null;
				var c = NextChar ();
				if (Char.IsLower (c)) {
					var nd = PushMultiSubstitutions (repeatCount, c - 'a');
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
					var idx = repeatCount + 27;
					if (idx >= substitutions.Count)
						return null;
					return substitutions [idx];
				}
				PushBack ();
				repeatCount = DemangleNatural ();
				if (repeatCount < 0)
					return null;
			}
		}

		Node PushMultiSubstitutions (int repeatCount, int substIdx)
		{
			if (substIdx >= substitutions.Count)
				return null;
			if (repeatCount > kMaxRepeatCount)
				return null;
			var nd = substitutions [substIdx];
			while (repeatCount-- > 1) {
				PushNode (nd);
			}
			return nd;
		}

		Node CreateSwiftType (NodeKind typeKind, string name)
		{
			return CreateType (CreateWithChildren (typeKind, new Node (NodeKind.Module, "Swift"), new Node (NodeKind.Identifier, name)));
		}

		Node DemangleStandardSubstitution ()
		{
			var c = NextChar ();
			switch (c) {
			case 'o':
				return new Node (NodeKind.Module, "__C");
			case 'C':
				return new Node (NodeKind.Module, "__C_Synthesized");
			case 'g': {
					var optionalTy = CreateType (CreateWithChildren (NodeKind.BoundGenericEnum,
										CreateSwiftType (NodeKind.Enum, "Optional"),
										CreateWithChild (NodeKind.TypeList, PopNode (NodeKind.Type))));
					AddSubstitution (optionalTy);
					return optionalTy;
				}
			default:
				PushBack ();
				var repeatCount = DemangleNatural ();
				if (repeatCount > kMaxRepeatCount)
					return null;
				Node nd;
				if ((nd = CreateStandardSubstitution (NextChar ())) != null) {
					while (repeatCount-- > 1) {
						PushNode (nd);
					}
					return nd;
				}
				return null;
			}
		}

		Node CreateStandardSubstitution (char subst)
		{
			var kind = NodeKind.Structure;
			string name = null;
			switch (subst) {
			case 'A': name = "AutoreleasingUnsafeMutablePointer"; break;
			case 'a': name = "Array"; break;
			case 'b': name = "Bool"; break;
			case 'c': name = "UnicodeScalar"; break;
			case 'D': name = "Dictionary"; break;
			case 'd': name = "Double"; break;
			case 'f': name = "Float"; break;
			case 'h': name = "Set"; break;
			case 'I': name = "DefaultIndicies"; break;
			case 'i': name = "Int"; break;
			case 'J': name = "Character"; break;
			case 'N': name = "ClosedRange"; break;
			case 'n': name = "Range"; break;
			case 'O': name = "ObjectIdentifier"; break;
			case 'P': name = "UnsafePointer"; break;
			case 'p': name = "UnsafeMutablePointer"; break;
			case 'R': name = "UnsafeBufferPointer"; break;
			case 'r': name = "UnsafeMutableBufferPointer"; break;
			case 'S': name = "String"; break;
			case 's': name = "Substring"; break;
			case 'u': name = "UInt"; break;
			case 'V': name = "UnsafeRawPointer"; break;
			case 'v': name = "UnsafeMutableRawPointer"; break;
			case 'W': name = "UnsafeRawBufferPointer"; break;
			case 'w': name = "UnsafeMutableRawBufferPointer"; break;

			case 'q': name = "Optional"; kind = NodeKind.Enum; break;

			case 'B': name = "BinaryFloatingPoint"; kind = NodeKind.Protocol; break;
			case 'E': name = "Encodable"; kind = NodeKind.Protocol; break;
			case 'e': name = "Decodable"; kind = NodeKind.Protocol; break;
			case 'F': name = "FloatingPoint"; kind = NodeKind.Protocol; break;
			case 'G': name = "RandomNumberGenerator"; kind = NodeKind.Protocol; break;
			case 'H': name = "Hashable"; kind = NodeKind.Protocol; break;
			case 'j': name = "Numeric"; kind = NodeKind.Protocol; break;
			case 'K': name = "BidirectionalCollection"; kind = NodeKind.Protocol; break;
			case 'k': name = "RandomAccessCollection"; kind = NodeKind.Protocol; break;
			case 'L': name = "Comparable"; kind = NodeKind.Protocol; break;
			case 'l': name = "Collection"; kind = NodeKind.Protocol; break;
			case 'M': name = "MutableCollection"; kind = NodeKind.Protocol; break;
			case 'm': name = "RangeReplaceableCollection"; kind = NodeKind.Protocol; break;
			case 'Q': name = "Equatable"; kind = NodeKind.Protocol; break;
			case 'T': name = "Sequence"; kind = NodeKind.Protocol; break;
			case 't': name = "IteratorProtocol"; kind = NodeKind.Protocol; break;
			case 'U': name = "UnsignedInteger"; kind = NodeKind.Protocol; break;
			case 'X': name = "RangeExpression"; kind = NodeKind.Protocol; break;
			case 'x': name = "Strideable"; kind = NodeKind.Protocol; break;
			case 'Y': name = "RawRepresentable"; kind = NodeKind.Protocol; break;
			case 'y': name = "StringProtocol"; kind = NodeKind.Protocol; break;
			case 'Z': name = "SignedInteger"; kind = NodeKind.Protocol; break;
			case 'z': name = "BinaryInteger"; kind = NodeKind.Protocol; break;
			default:
				return null;
			}

			return CreateSwiftType (kind, name);
		}

		Node DemangleIdentifier ()
		{
			var hasWordSubsts = false;
			var isPunycoded = false;
			var c = PeekChar ();
			if (!Char.IsDigit (c))
				return null;
			if (c == '0') {
				NextChar ();
				if (PeekChar () == '0') {
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
						if (wordStartPos < 0 && IsWordStart (ccc)) {
							wordStartPos = idx;
						}
					}
				}
				slice.Advance (numChars);
			} while (hasWordSubsts);
			if (identifier.Length == 0)
				return null;
			var ident = new Node (NodeKind.Identifier, identifier.ToString ());
			AddSubstitution (ident);
			return ident;
		}

		bool IsWordStart (char ch)
		{
			return !Char.IsDigit (ch) && ch != '_' && ch != (char)0;
		}

		bool IsWordEnd (char ch, char prevCh)
		{
			if (ch == '_' || ch == (char)0)
				return true;
			if (!Char.IsUpper (prevCh) && Char.IsUpper (ch))
				return true;
			return false;
		}


		Node DemangleOperatorIdentifier ()
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
			switch (NextChar ()) {
			case 'i': return new Node (NodeKind.InfixOperator, opStr.ToString ());
			case 'p': return new Node (NodeKind.PrefixOperator, opStr.ToString ());
			case 'P': return new Node (NodeKind.PostfixOperator, opStr.ToString ());
			default: return null;
			}
		}


		Node DemangleLocalIdentifier ()
		{
			if (NextIf ('L')) {
				var discriminator = PopNode (NodeKind.Identifier);
				var name = PopNode (IsDeclName);
				return CreateWithChildren (NodeKind.PrivateDeclName, discriminator, name);
			}
			if (NextIf ('l')) {
				var discriminator = PopNode (NodeKind.Identifier);
				return CreateWithChild (NodeKind.PrivateDeclName, discriminator);
			}
			if ((PeekChar () >= 'a' && PeekChar () <= 'j') ||
					(PeekChar () >= 'A' && PeekChar () <= 'J')) {
				var relatedEntityKind = NextChar ();
				var name = PopNode ();
				var result = new Node (NodeKind.RelatedEntityDeclName, relatedEntityKind.ToString ());
				return AddChild (result, name);
			}
			var discriminatorx = DemangleIndexAsNode ();
			var namex = PopNode (IsDeclName);
			return CreateWithChildren (NodeKind.LocalDeclName, discriminatorx, namex);
		}

		Node PopModule ()
		{
			var ident = PopNode (NodeKind.Identifier);
			if (ident != null)
				return ChangeKind (ident, NodeKind.Module);
			return PopNode (NodeKind.Module);
		}

		Node PopContext ()
		{
			var mod = PopModule ();
			if (mod != null)
				return mod;

			var ty = PopNode (NodeKind.Type);
			if (ty != null) {
				if (ty.Children.Count != 1)
					return null;
				var child = ty.Children [0];
				if (!IsContext (child.Kind))
					return null;
				return child;
			}
			return PopNode (IsContext);
		}

		Node PopTypeAndGetChild ()
		{
			var ty = PopNode (NodeKind.Type);
			if (ty == null || ty.Children.Count != 1)
				return null;
			return ty.Children [0];
		}


		Node PopTypeAndGetAnyGeneric ()
		{
			var child = PopTypeAndGetChild ();
			if (child != null && IsAnyGeneric (child.Kind))
				return child;
			return null;
		}

		Node DemangleBuiltinType ()
		{
			Node ty = null;
			const int maxTypeSize = 4096;
			switch (NextChar ()) {
			case 'b':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.BridgeObject");
				break;
			case 'B':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.UnsafeValueBuffer");
				break;
			case 'f': {
					var size = DemangleIndex () - 1;
					if (size <= 0 || size > maxTypeSize)
						return null;
					var floatName = $"Builtin.FPIEEE{size}";
					ty = new Node (NodeKind.BuiltinTypeName, floatName);
					break;
				}
			case 'i': {
					var size = DemangleIndex () - 1;
					if (size < 0 || size > maxTypeSize)
						return null;
					var intName = $"Builtin.Int{size}";
					ty = new Node (NodeKind.BuiltinTypeName, intName);
					break;
				}
			case 'I':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.IntLiteral");
				break;
			case 'v':
				var elts = DemangleIndex () - 1;
				if (elts <= 0 || elts > maxTypeSize)
					return null;
				var eltType = PopTypeAndGetChild ();
				if (eltType == null || eltType.Kind != NodeKind.BuiltinTypeName ||
						!eltType.Text.StartsWith ("Builtin.", StringComparison.Ordinal))
					return null;
				var name = $"Builtin.Vec{elts}x{eltType.Text.Substring ("Builtin.".Length)}";
				ty = new Node (NodeKind.BuiltinTypeName, name);
				break;
			case 'O':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.UnknownObject");
				break;
			case 'o':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.NativeObject");
				break;
			case 'p':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.RawPointer");
				break;
			case 't':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.SILToken");
				break;
			case 'w':
				ty = new Node (NodeKind.BuiltinTypeName, "Builtin.Word");
				break;
			default:
				return null;
			}
			return CreateType (ty);
		}

		Node DemangleAnyGenericType (NodeKind kind)
		{
			var name = PopNode (IsDeclName);
			var ctx = PopContext ();
			var nty = CreateType (CreateWithChildren (kind, ctx, name));
			AddSubstitution (nty);
			return nty;
		}

		Node DemangleExtensionContext ()
		{
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			var module = PopModule ();
			var type = PopTypeAndGetAnyGeneric ();
			var ext = CreateWithChildren (NodeKind.Extension, module, type);
			if (genSig != null)
				ext = AddChild (ext, genSig);
			return ext;
		}

		Node DemanglePlainFunction ()
		{
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			var type = PopFunctionType (NodeKind.FunctionType);
			var labelList = PopFunctionParamLabels (type);

			if (genSig != null) {
				type = CreateType (CreateWithChildren (NodeKind.DependentGenericType, genSig, type));
			}

			var name = PopNode (IsDeclName);
			var ctx = PopContext ();

			if (labelList != null)
				return CreateWithChildren (NodeKind.Function, ctx, name, labelList, type);
			return CreateWithChildren (NodeKind.Function, ctx, name, type);
		}


		Node PopFunctionType (NodeKind kind)
		{
			var funcType = new Node (kind);
			AddChild (funcType, PopNode (NodeKind.ThrowsAnnotation));
			funcType = AddChild (funcType, PopFunctionParams (NodeKind.ArgumentTuple));
			funcType = AddChild (funcType, PopFunctionParams (NodeKind.ReturnType));
			return CreateType (funcType);
		}

		Node PopFunctionParams (NodeKind kind)
		{
			Node paramsType = null;
			if (PopNode (NodeKind.EmptyList) != null) {
				paramsType = CreateType (new Node (NodeKind.Tuple));
			} else {
				paramsType = PopNode (NodeKind.Type);
			}
			Node node = null;

			if (paramsType != null && kind == NodeKind.ArgumentTuple) {
				var @params = paramsType.Children [0];
				var numParams = @params.Kind == NodeKind.Tuple ? @params.Children.Count : 1;
				node = new Node (kind, numParams);
			} else {
				node = new Node (kind);
			}

			return AddChild (node, paramsType);
		}

		Node PopFunctionParamLabels (Node type)
		{
			if (!isOldFunctionTypeMangling && PopNode (NodeKind.EmptyList) != null)
				return new Node (NodeKind.LabelList);

			if (type == null || type.Kind != NodeKind.Type)
				return null;

			var funcType = type.Children [0];
			if (funcType.Kind == NodeKind.DependentGenericType)
				funcType = funcType.Children [1].Children [0];

			if (funcType.Kind != NodeKind.FunctionType && funcType.Kind != NodeKind.NoEscapeFunctionType)
				return null;

			var parameterType = funcType.Children [0];
			if (parameterType.Kind == NodeKind.ThrowsAnnotation)
				parameterType = funcType.Children [1];


			if (parameterType.Index == 0)
				return null;

			Func<Node, NodeKind, KeyValuePair<Node, int>> getChildIf = (node, filterBy) => {
				for (int i = 0, n = node.Children.Count; i != n; ++i) {
					var child = node.Children [i];
					if (child.Kind == filterBy)
						return new KeyValuePair<Node, int> (child, i);
				}
				return new KeyValuePair<Node, int> (null, 0);
			};

			Func<Node, int, Node> getLabel = (@params, idx) => {
				if (isOldFunctionTypeMangling) {
					var param = @params.Children [idx];
					var label = getChildIf (param, NodeKind.TupleElementName);

					if (label.Key != null) {
						param.RemoveChildAt (label.Value);
						return new Node (NodeKind.Identifier, label.Key.Text);
					}
					return new Node (NodeKind.FirstElementMarker);
				}
				return PopNode ();
			};

			var labelList = new Node (NodeKind.LabelList);
			var tuple = parameterType.Children [0].Children [0];

			if (isOldFunctionTypeMangling && (tuple != null || tuple.Kind != NodeKind.Tuple))
				return labelList;

			var hasLabels = false;
			for (int i = 0; i != parameterType.Index; ++i) {
				var label = getLabel (tuple, i);
				if (label == null)
					return null;

				if (label.Kind != NodeKind.Identifier && label.Kind != NodeKind.FirstElementMarker)
					return null;

				labelList.AddChild (label);
				hasLabels |= label.Kind != NodeKind.FirstElementMarker;
			}

			if (!hasLabels)
				return new Node (NodeKind.LabelList);

			if (!isOldFunctionTypeMangling)
				labelList.ReverseChildren ();

			return labelList;
		}

		Node PopTuple ()
		{
			var root = new Node (NodeKind.Tuple);

			if (PopNode (NodeKind.EmptyList) == null) {
				var firstElem = false;
				do {
					firstElem = (PopNode (NodeKind.FirstElementMarker)) != null;
					var tupleElmt = new Node (NodeKind.TupleElement);
					AddChild (tupleElmt, PopNode (NodeKind.VariadicMarker));
					Node ident;
					if ((ident = PopNode (NodeKind.Identifier)) != null) {
						tupleElmt.AddChild (new Node (NodeKind.TupleElementName, ident.Text));
					}
					var ty = PopNode (NodeKind.Type);
					if (ty == null)
						return null;
					tupleElmt.AddChild (ty);
					root.AddChild (tupleElmt);
				} while (!firstElem);

				root.ReverseChildren ();
			}
			return CreateType (root);
		}

		Node PopTypeList ()
		{
			var root = new Node (NodeKind.TypeList);

			if (PopNode (NodeKind.EmptyList) == null) {
				var firstElem = false;
				do {
					firstElem = (PopNode (NodeKind.FirstElementMarker) != null);
					var ty = PopNode (NodeKind.Type);
					if (ty == null)
						return ty;
					root.AddChild (ty);
				} while (!firstElem);
				root.ReverseChildren ();
			}
			return root;
		}

		Node PopProtocol ()
		{
			Node type;
			if ((type = PopNode (NodeKind.Type)) != null) {
				if (type.Children.Count < 1)
					return null;

				if (!IsProtocolNode (type))
					return null;
				return type;
			}

			Node symbolicRef;
			if ((symbolicRef = PopNode (NodeKind.ProtocolSymbolicReference)) != null) {
				return symbolicRef;
			}

			var name = PopNode (IsDeclName);
			var ctx = PopContext ();
			var proto = CreateWithChildren (NodeKind.Protocol, ctx, name);
			return CreateType (proto);
		}

		Node PopAnyProtocolConformanceList ()
		{
			var conformanceList = new Node (NodeKind.AnyProtocolConformanceList);
			if (PopNode (NodeKind.EmptyList) == null) {
				var firstElem = false;
				do {
					firstElem = (PopNode (NodeKind.FirstElementMarker) != null);
					var anyConformance = PopAnyProtocolConformance ();
					if (anyConformance == null)
						return null;
					conformanceList.AddChild (anyConformance);
				} while (!firstElem);
				conformanceList.ReverseChildren ();
			}
			return conformanceList;
		}

		Node PopAnyProtocolConformance ()
		{
			return PopNode ((kind) => {
				switch (kind) {
				case NodeKind.ConcreteProtocolConformance:
				case NodeKind.DependentProtocolConformanceRoot:
				case NodeKind.DependentProtocolConformanceInherited:
				case NodeKind.DependentProtocolConformanceAssociated:
					return true;
				default:
					return false;
				}
			});
		}

		Node DemangleRetroactiveProtocolConformanceRef ()
		{
			var module = PopModule ();
			var proto = PopProtocol ();
			var protocolConformanceRef = CreateWithChildren (NodeKind.ProtocolConformanceRefInOtherModule, proto, module);
			return protocolConformanceRef;
		}

		Node DemangleConcreteProtocolConformance ()
		{
			var conditionalConformanceList = PopAnyProtocolConformanceList ();

			var conformanceRef = PopNode (NodeKind.ProtocolConformanceRefInTypeModule);
			if (conformanceRef == null) {
				conformanceRef = PopNode (NodeKind.ProtocolConformanceRefInProtocolModule);
			}
			if (conformanceRef == null)
				conformanceRef = DemangleRetroactiveProtocolConformanceRef ();

			var type = PopNode (NodeKind.Type);
			return CreateWithChildren (NodeKind.ConcreteProtocolConformance,
						  type, conformanceRef, conditionalConformanceList);
		}


		Node DemangleProtocolConformance ()
		{
			var conditionalConformanceList = PopAnyProtocolConformanceList ();

			var conformanceRef = PopNode (NodeKind.ProtocolConformanceRefInTypeModule);
			if (conformanceRef == null) {
				conformanceRef = PopNode (NodeKind.ProtocolConformanceRefInProtocolModule);
			}

			if (conformanceRef == null)
				conformanceRef = DemangleRetroactiveProtocolConformanceRef ();

			var type = PopNode (NodeKind.Type);
			return CreateWithChildren (NodeKind.ConcreteProtocolConformance, type, conformanceRef, conditionalConformanceList);
		}

		Node PopDependentProtocolConformance ()
		{
			return PopNode ((kind) => {
				switch (kind) {
				case NodeKind.DependentProtocolConformanceRoot:
				case NodeKind.DependentProtocolConformanceInherited:
				case NodeKind.DependentProtocolConformanceAssociated:
					return true;
				default:
					return false;
				}
			});
		}

		Node DemangleDependentProtocolConformanceRoot ()
		{
			var index = DemangleIndex ();
			var conformance =
				index > 0 ? new Node (NodeKind.DependentProtocolConformanceRoot, index - 1)
					: new Node (NodeKind.DependentProtocolConformanceRoot);
			Node protocol = null;
			if ((protocol = PopProtocol ()) != null)
				conformance.AddChild (protocol);
			else
				return null;

			Node dependentType;
			if ((dependentType = PopNode (NodeKind.Type)) != null)
				conformance.AddChild (dependentType);
			else
				return null;

			return conformance;
		}

		Node DemangleDependentProtocolConformanceInherited ()
		{
			var index = DemangleIndex ();
			var conformance =
				index > 0 ? new Node (NodeKind.DependentProtocolConformanceInherited, index - 1)
					: new Node (NodeKind.DependentProtocolConformanceInherited);
			Node protocol;
			if ((protocol = PopProtocol ()) != null)
				conformance.AddChild (protocol);
			else
				return null;

			Node nested;

			if ((nested = PopDependentProtocolConformance ()) != null)
				conformance.AddChild (nested);
			else
				return null;

			conformance.ReverseChildren ();
			return conformance;
		}

		Node PopDependentAssociatedConformance ()
		{
			var protocol = PopProtocol ();
			var dependentType = PopNode (NodeKind.Type);
			return CreateWithChildren (NodeKind.DependentAssociatedConformance, dependentType, protocol);
		}

		Node DemangleDependentProtocolConformanceAssociated ()
		{
			var index = DemangleIndex ();
			var conformance = index > 0 ? new Node (NodeKind.DependentProtocolConformanceRoot, index - 1)
						: new Node (NodeKind.DependentProtocolConformanceRoot);

			Node associatedConformance;
			if ((associatedConformance = PopDependentAssociatedConformance ()) != null)
				conformance.AddChild (associatedConformance);
			else
				return null;

			Node nested;
			if ((nested = PopDependentProtocolConformance ()) != null)
				conformance.AddChild (nested);
			else
				return null;

			conformance.ReverseChildren ();

			return conformance;
		}

		Node DemangleRetroactiveConformance ()
		{
			var index = DemangleIndex ();
			if (index < 0)
				return null;

			var conformance = PopAnyProtocolConformance ();
			if (conformance == null)
				return null;

			var retroactiveConformance = new Node (NodeKind.RetroactiveConformance, index);
			retroactiveConformance.AddChild (conformance);
			return retroactiveConformance;
		}

		Node DemangleBoundGenericType ()
		{
			Node retroactiveConformances = null;
			Node retroactiveConformance;
			while ((retroactiveConformance = PopNode (NodeKind.RetroactiveConformance)) != null) {
				if (retroactiveConformances == null)
					retroactiveConformances = new Node (NodeKind.TypeList);
				retroactiveConformances.AddChild (retroactiveConformance);
			}
			if (retroactiveConformances != null)
				retroactiveConformances.ReverseChildren ();

			var typeListList = new List<Node> ();
			for (; ; ) {
				var tlist = new Node (NodeKind.TypeList);
				typeListList.Add (tlist);
				Node ty;
				while ((ty = PopNode (NodeKind.Type)) != null) {
					tlist.AddChild (ty);
				}
				tlist.ReverseChildren ();
				if (PopNode (NodeKind.EmptyList) != null)
					break;
				if (PopNode (NodeKind.FirstElementMarker) == null)
					return null;
			}
			var nominal = PopTypeAndGetAnyGeneric ();
			var boundNode = DemangleBoundGenericArgs (nominal, typeListList, 0);
			AddChild (boundNode, retroactiveConformances);
			var nty = CreateType (boundNode);
			AddSubstitution (nty);
			return nty;
		}

		Node DemangleBoundGenericArgs (Node nominal, List<Node> typeLists, int typeListIdx)
		{
			if (nominal == null)
				return null;

			if (typeListIdx >= typeLists.Count)
				return null;

			if (nominal.Kind == NodeKind.TypeSymbolicReference || nominal.Kind == NodeKind.ProtocolSymbolicReference) {
				var remainingTypeList = new Node (NodeKind.TypeList);
				for (int i = typeLists.Count - 1; i >= typeListIdx && i < typeLists.Count; --i) {
					var list = typeLists [i];
					foreach (var child in list.Children) {
						remainingTypeList.AddChild (child);
					}
				}
				return CreateWithChildren (NodeKind.BoundGenericOtherNominalType, CreateType (nominal), remainingTypeList);
			}

			if (nominal.Children.Count == 0)
				return null;
			var context = nominal.Children [0];

			var consumesGenericArgs = true;
			switch (nominal.Kind) {
			case NodeKind.Variable:
			case NodeKind.ExplicitClosure:
			case NodeKind.Subscript:
				consumesGenericArgs = false;
				break;
			default:
				break;
			}

			var args = typeLists [typeListIdx];
			if (consumesGenericArgs)
				++typeListIdx;

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

				var newNominal = CreateWithChild (nominal.Kind, boundParent);
				if (newNominal == null)
					return null;

				for (int idx = 1; idx < nominal.Children.Count; ++idx) {
					AddChild (newNominal, nominal.Children [idx]);
				}
				nominal = newNominal;
			}
			if (!consumesGenericArgs)
				return nominal;

			if (args.Children.Count == 0)
				return nominal;

			NodeKind kind;
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
			case NodeKind.Protocol:
				kind = NodeKind.BoundGenericProtocol;
				break;
			case NodeKind.OtherNominalType:
				kind = NodeKind.BoundGenericOtherNominalType;
				break;
			case NodeKind.TypeAlias:
				kind = NodeKind.BoundGenericTypeAlias;
				break;
			case NodeKind.Function:
			case NodeKind.Constructor:
				return CreateWithChildren (NodeKind.BoundGenericFunction, nominal, args);
			default:
				return null;
			}
			return CreateWithChildren (kind, CreateType (nominal), args);
		}

		Node DemangleImpleParamConvention ()
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

		Node DemangleImplResultConvention (NodeKind convKind)
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

		Node DemangleImplFunctionType ()
		{
			var type = new Node (NodeKind.ImplFunctionType);

			var genSig = PopNode (NodeKind.DependentGenericSignature);
			if (genSig != null && NextIf ('P'))
				genSig = ChangeKind (genSig, NodeKind.DependentPseudogenericSignature);

			if (NextIf ('e'))
				type.AddChild (new Node (NodeKind.ImplEscaping));

			string cattr = null;
			switch (NextChar ()) {
			case 'y': cattr = "@callee_unowned"; break;
			case 'g': cattr = "@callee_guaranteed"; break;
			case 'x': cattr = "@callee_owned"; break;
			case 't': cattr = "@convention(thin)"; break;
			default:
				return null;
			}
			type.AddChild (new Node (NodeKind.ImplConvention, cattr));

			string fattr = null;
			switch (NextChar ()) {
			case 'B': fattr = "@convention(block)"; break;
			case 'C': fattr = "@convention(c)"; break;
			case 'M': fattr = "@convention(method)"; break;
			case 'O': fattr = "@convention(objc_method)"; break;
			case 'K': fattr = "@convention(closure)"; break;
			case 'W': fattr = "@convention(witness_method)"; break;
			default:
				PushBack ();
				break;
			}
			if (fattr != null)
				type.AddChild (new Node (NodeKind.ImplFunctionAttribute, fattr));

			AddChild (type, genSig);

			var numTypesToAdd = 0;
			Node param;
			while ((param = DemangleImpleParamConvention ()) != null) {
				type = AddChild (type, param);
				numTypesToAdd++;
			}
			Node result;
			while ((result = DemangleImplResultConvention (NodeKind.ImplResult)) != null) {
				type = AddChild (type, result);
				numTypesToAdd++;
			}
			if (NextIf ('z')) {
				var errorResult = DemangleImplResultConvention (NodeKind.ImplErrorResult);
				if (errorResult == null)
					return null;
				type = AddChild (type, errorResult);
				numTypesToAdd++;
			}
			if (!NextIf ('_'))
				return null;

			for (int idx = 0; idx < numTypesToAdd; ++idx) {
				var convTy = PopNode (NodeKind.Type);
				if (convTy == null)
					return null;
				type.Children [type.Children.Count - idx - 1].AddChild (convTy);
			}

			return CreateType (type);
		}

		Node DemangleMetatype ()
		{
			switch (NextChar ()) {
			case 'c':
				return CreateWithChild (NodeKind.ProtocolConformanceDescriptor, PopProtocolConformance ());
			case 'f':
				return CreateWithPoppedType (NodeKind.FullTypeMetadata);
			case 'P':
				return CreateWithPoppedType (NodeKind.GenericTypeMetadataPattern);
			case 'a':
				return CreateWithPoppedType (NodeKind.TypeMetadataAccessFunction);
			case 'I':
				return CreateWithPoppedType (NodeKind.TypeMetadataInstantiationCache);
			case 'i':
				return CreateWithPoppedType (NodeKind.TypeMetadataInstantiationFunction);
			case 'r':
				return CreateWithPoppedType (NodeKind.TypeMetadataCompletionFunction);
			case 'l':
				return CreateWithPoppedType (
						      NodeKind.TypeMetadataSingletonInitializationCache);
			case 'L':
				return CreateWithPoppedType (NodeKind.TypeMetadataLazyCache);
			case 'm':
				return CreateWithPoppedType (NodeKind.Metaclass);
			case 'n':
				return CreateWithPoppedType (NodeKind.NominalTypeDescriptor);
			case 'o':
				return CreateWithPoppedType (NodeKind.ClassMetadataBaseOffset);
			case 'p':
				return CreateWithChild (NodeKind.ProtocolDescriptor, PopProtocol ());
			case 'S':
				return CreateWithChild (NodeKind.ProtocolSelfConformanceDescriptor,
						       PopProtocol ());
			case 'u':
				return CreateWithPoppedType (NodeKind.MethodLookupFunction);
			case 'U':
				return CreateWithPoppedType (NodeKind.ObjCMetadataUpdateFunction);
			case 'B':
				return CreateWithChild (NodeKind.ReflectionMetadataBuiltinDescriptor,
						       PopNode (NodeKind.Type));
			case 'F':
				return CreateWithChild (NodeKind.ReflectionMetadataFieldDescriptor,
						       PopNode (NodeKind.Type));
			case 'A':
				return CreateWithChild (NodeKind.ReflectionMetadataAssocTypeDescriptor,
						       PopProtocolConformance ());
			case 'C': {
					Node Ty = PopNode (NodeKind.Type);
					if (Ty == null || !IsAnyGeneric (Ty.Children [0].Kind))
						return null;
					return CreateWithChild (NodeKind.ReflectionMetadataSuperclassDescriptor,
							       Ty.Children [0]);
				}
			case 'V':
				return CreateWithChild (NodeKind.PropertyDescriptor,
						       PopNode (IsEntity));
			case 'X':
				return DemanglePrivateContextDescriptor ();
			default:
				return null;
			}
		}

		Node DemanglePrivateContextDescriptor ()
		{
			switch (NextChar ()) {
			case 'E': {
					var extension = PopContext ();
					if (extension == null)
						return null;
					return CreateWithChild (NodeKind.ExtensionDescriptor, extension);
				}
			case 'M': {
					var module = PopModule ();
					if (module == null)
						return null;
					return CreateWithChild (NodeKind.ModuleDescriptor, module);
				}
			case 'Y': {
					var discriminator = PopNode ();
					if (discriminator == null)
						return null;
					var context = PopContext ();
					if (context == null)
						return null;

					var node = new Node (NodeKind.AnonymousDescriptor);
					node.AddChild (context);
					node.AddChild (discriminator);
					return node;
				}
			case 'X': {
					var context = PopContext ();
					if (context == null)
						return null;
					return CreateWithChild (NodeKind.AnonymousDescriptor, context);
				}
			case 'A': {
					var path = PopAssocTypePath ();
					if (path == null)
						return null;
					var @base = PopNode (NodeKind.Type);
					if (@base == null)
						return null;
					return CreateWithChildren (NodeKind.AssociatedTypeGenericParamRef,
								  @base, path);
				}
			default:
				return null;
			}
		}

		Node DemangleArchetype ()
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
					var t = DemangleAssociatedTypeSimple (DemangleGenericParamIndex ());
					AddSubstitution (t);
					return t;
				}
			case 'z': {
					var t = DemangleAssociatedTypeSimple (GetDependentGenericParamType (0, 0));
					AddSubstitution (t);
					return t;
				}
			case 'Y': {
					var t = DemangleAssociatedTypeCompound (DemangleGenericParamIndex ());
					AddSubstitution (t);
					return t;
				}
			case 'Z': {
					var t = DemangleAssociatedTypeCompound (GetDependentGenericParamType (0, 0));
					AddSubstitution (t);
					return t;
				}
			default:
				return null;
			}
		}

		Node DemangleAssociatedTypeSimple (Node genericParamIdx)
		{
			var GPI = CreateType (genericParamIdx);
			var ATName = PopAssocTypeName ();
			return CreateType (CreateWithChildren (NodeKind.DependentMemberType, GPI, ATName));
		}

		Node DemangleAssociatedTypeCompound (Node genericParamIdx)
		{
			var assocTyNames = new List<Node> ();
			bool firstElem = false;
			do {
				firstElem = (PopNode (NodeKind.FirstElementMarker) != null);
				var assocTyName = PopAssocTypeName ();
				if (assocTyName == null)
					return null;
				assocTyNames.Add (assocTyName);
			} while (!firstElem);

			var @base = genericParamIdx;


			for (int i = assocTyNames.Count - 1; i >= 0; --i) {
				var assocTy = assocTyNames [i];
				var depTy = new Node (NodeKind.DependentMemberType);
				depTy = AddChild (depTy, CreateType (@base));
				@base = AddChild (depTy, assocTy);
			}
			return CreateType (@base);
		}

		Node PopAssocTypeName ()
		{
			var proto = PopNode (NodeKind.Type);
			if (proto != null && !IsProtocolNode (proto))
				return null;

			// If we haven't seen a protocol, check for a symbolic reference.
			if (proto == null)
				proto = PopNode (NodeKind.ProtocolSymbolicReference);

			var id = PopNode (NodeKind.Identifier);
			var assocTy = ChangeKind (id, NodeKind.DependentAssociatedTypeRef);
			AddChild (assocTy, proto);
			return assocTy;
		}

		Node PopAssocTypePath ()
		{
			var assocTypePath = new Node (NodeKind.AssocTypePath);
			bool firstElem = false;
			do {
				firstElem = (PopNode (NodeKind.FirstElementMarker) != null);
				var assocTy = PopAssocTypeName ();
				if (assocTy == null)
					return null;
				assocTypePath.AddChild (assocTy);
			} while (!firstElem);
			assocTypePath.ReverseChildren ();
			return assocTypePath;
		}

		Node GetDependentGenericParamType (int depth, int index)
		{
			if (depth < 0 || index < 0)
				return null;

			StringBuilder name = new StringBuilder ();
			int idxChar = index;
			do {
				name.Append ((char)('A' + (idxChar % 26)));
				idxChar /= 26;
			} while (idxChar > 0);
			if (depth != 0)
				name.Append (depth);

			var paramTy = new Node (NodeKind.DependentGenericParamType, name.ToString ());
			paramTy.AddChild (new Node (NodeKind.Index, depth));
			paramTy.AddChild (new Node (NodeKind.Index, index));
			return paramTy;
		}

		Node DemangleGenericParamIndex ()
		{
			if (NextIf ('d')) {
				int depth = DemangleIndex () + 1;
				int index = DemangleIndex ();
				return GetDependentGenericParamType (depth, index);
			}
			if (NextIf ('z')) {
				return GetDependentGenericParamType (0, 0);
			}
			return GetDependentGenericParamType (0, DemangleIndex () + 1);
		}

		Node PopProtocolConformance ()
		{
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			var module = PopModule ();
			var proto = PopProtocol ();
			var type = PopNode (NodeKind.Type);
			Node Ident = null;
			if (type == null) {
				// Property behavior conformance
				Ident = PopNode (NodeKind.Identifier);
				type = PopNode (NodeKind.Type);
			}
			if (genSig != null) {
				type = CreateType (CreateWithChildren (NodeKind.DependentGenericType, genSig, type));
			}
			var Conf = CreateWithChildren (NodeKind.ProtocolConformance, type, proto, module);
			AddChild (Conf, Ident);
			return Conf;
		}

		Node DemangleThunkOrSpecialization ()
		{
			var c = NextChar ();
			switch (c) {
			case 'c': return CreateWithChild (NodeKind.CurryThunk, PopNode (IsEntity));
			case 'j': return CreateWithChild (NodeKind.DispatchThunk, PopNode (IsEntity));
			case 'q': return CreateWithChild (NodeKind.MethodDescriptor, PopNode (IsEntity));
			case 'o': return new Node (NodeKind.ObjCAttribute);
			case 'O': return new Node (NodeKind.NonObjCAttribute);
			case 'D': return new Node (NodeKind.DynamicAttribute);
			case 'd': return new Node (NodeKind.DirectMethodReferenceAttribute);
			case 'a': return new Node (NodeKind.PartialApplyObjCForwarder);
			case 'A': return new Node (NodeKind.PartialApplyForwarder);
			case 'm': return new Node (NodeKind.MergedFunction);
			case 'X': return new Node (NodeKind.DynamicallyReplaceableFunctionVar);
			case 'x': return new Node (NodeKind.DynamicallyReplaceableFunctionKey);
			case 'I': return new Node (NodeKind.DynamicallyReplaceableFunctionImpl);
			case 'C': {
					var type = PopNode (NodeKind.Type);
					return CreateWithChild (NodeKind.CoroutineContinuationPrototype, type);
				}
			case 'V': {
					var @base = PopNode (IsEntity);
					var derived = PopNode (IsEntity);
					return CreateWithChildren (NodeKind.VTableThunk, derived, @base);
				}
			case 'W': {
					var entity = PopNode (IsEntity);
					var conf = PopProtocolConformance ();
					return CreateWithChildren (NodeKind.ProtocolWitness, conf, entity);
				}
			case 'S':
				return CreateWithChild (NodeKind.ProtocolSelfConformanceWitness,
						       PopNode (IsEntity));
			case 'R':
			case 'r': {
					var thunk = new Node (c == 'R' ?
									  NodeKind.ReabstractionThunkHelper :
									  NodeKind.ReabstractionThunk);
					Node genSig;
					if ((genSig = PopNode (NodeKind.DependentGenericSignature)) != null)
						AddChild (thunk, genSig);
					var Ty2 = PopNode (NodeKind.Type);
					thunk = AddChild (thunk, PopNode (NodeKind.Type));
					return AddChild (thunk, Ty2);
				}
			case 'g':
				return DemangleGenericSpecialization (NodeKind.GenericSpecialization);
			case 'G':
				return DemangleGenericSpecialization (NodeKind.GenericSpecializationNotReAbstracted);
			case 'i':
				return DemangleGenericSpecialization (NodeKind.InlinedGenericFunction);
			case 'p': {
					var spec = DemangleSpecAttributes (NodeKind.GenericPartialSpecialization);
					var param = CreateWithChild (NodeKind.GenericSpecializationParam, PopNode (NodeKind.Type));
					return AddChild (spec, param);
				}
			case 'P': {
					var spec = DemangleSpecAttributes (NodeKind.GenericPartialSpecializationNotReAbstracted);
					var param = CreateWithChild (NodeKind.GenericSpecializationParam, PopNode (NodeKind.Type));
					return AddChild (spec, param);
				}
			case 'f':
				return DemangleFunctionSpecialization ();
			case 'K':
			case 'k': {
					var nodeKind = c == 'K' ? NodeKind.KeyPathGetterThunkHelper
								 : NodeKind.KeyPathSetterThunkHelper;
					var types = new List<Node> ();
					var node = PopNode ();
					if (node == null || node.Kind != NodeKind.Type)
						return null;
					do {
						types.Add (node);
						node = PopNode ();
					} while (node != null && node.Kind == NodeKind.Type);

					Node result;
					if (node != null) {
						if (node.Kind == NodeKind.DependentGenericSignature) {
							var decl = PopNode ();
							if (decl == null)
								return null;
							result = CreateWithChildren (nodeKind, decl, /*sig*/ node);
						} else {
							result = CreateWithChild (nodeKind, /*decl*/ node);
						}
					} else {
						return null;
					}
					foreach (var i in types) {
						result.AddChild (i);
					}
					return result;
				}
			case 'l': {
					var assocTypeName = PopAssocTypeName ();
					if (assocTypeName == null)
						return null;

					return CreateWithChild (NodeKind.AssociatedTypeDescriptor,
							       assocTypeName);
				}
			case 'L':
				return CreateWithChild (NodeKind.ProtocolRequirementsBaseDescriptor,
						       PopProtocol ());
			case 'M':
				return CreateWithChild (NodeKind.DefaultAssociatedTypeMetadataAccessor,
						       PopAssocTypeName ());

			case 'n': {
					var requirementTy = PopProtocol ();
					var conformingType = PopAssocTypePath ();
					var protoTy = PopNode (NodeKind.Type);
					return CreateWithChildren (NodeKind.AssociatedConformanceDescriptor,
								  protoTy, conformingType, requirementTy);
				}

			case 'N': {
					var requirementTy = PopProtocol ();
					var assocTypePath = PopAssocTypePath ();
					var protoTy = PopNode (NodeKind.Type);
					return CreateWithChildren (NodeKind.DefaultAssociatedConformanceAccessor,
							      protoTy, assocTypePath, requirementTy);
				}

			case 'b': {
					var requirementTy = PopProtocol ();
					var protoTy = PopNode (NodeKind.Type);
					return CreateWithChildren (NodeKind.BaseConformanceDescriptor,
								  protoTy, requirementTy);
				}

			case 'H':
			case 'h': {
					var nodeKind = c == 'H' ? NodeKind.KeyPathEqualsThunkHelper
								 : NodeKind.KeyPathHashThunkHelper;
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

					Node node1;
					while ((node1 = PopNode ()) != null) {
						if (node1.Kind != NodeKind.Type) {
							return null;
						}
						types.Add (node);
					}

					var result = new Node (nodeKind);
					foreach (var i in types) {
						result.AddChild (i);
					}
					if (genericSig != null)
						result.AddChild (genericSig);
					return result;
				}
			case 'v': {
					int idx = DemangleIndex ();
					if (idx < 0)
						return null;
					return new Node (NodeKind.OutlinedVariable, idx);
				}
			case 'e': {
					string @params = DemangleBridgedMethodParams ();
					if (string.IsNullOrEmpty (@params))
						return null;
					return new Node (NodeKind.OutlinedBridgedMethod, @params);
				}
			default:
				return null;
			}
		}

		string DemangleBridgedMethodParams ()
		{
			if (NextIf ('_'))
				return "";

			StringBuilder Str = new StringBuilder ();

			var kind = NextChar ();
			switch (kind) {
			default:
				return "";
			case 'p':
			case 'a':
			case 'm':
				Str.Append (kind);
				break;
			}

			while (!NextIf ('_')) {
				var c = NextChar ();
				if (c != 0 && c != 'n' && c != 'b')
					return "";
				Str.Append (c);
			}
			return Str.ToString ();
		}

		Node DemangleGenericSpecialization (NodeKind SpecKind)
		{
			var Spec = DemangleSpecAttributes (SpecKind);
			if (Spec == null)
				return null;
			var TyList = PopTypeList ();
			if (TyList == null)
				return null;
			foreach (var Ty in TyList.Children) {
				Spec.AddChild (CreateWithChild (NodeKind.GenericSpecializationParam, Ty));
			}
			return Spec;
		}


		Node DemangleFunctionSpecialization ()
		{
			var spec = DemangleSpecAttributes (NodeKind.FunctionSignatureSpecialization);
			ulong paramIdx = 0;
			while (spec != null && !NextIf ('_')) {
				spec = AddChild (spec, DemangleFuncSpecParam (paramIdx));
				paramIdx++;
			}
			if (!NextIf ('n'))
				spec = AddChild (spec, DemangleFuncSpecParam ((~(ulong)0)));

			if (spec == null)
				return null;

			// Add the required parameters in reverse order.
			for (int idx = 0, num = spec.Children.Count; idx < num; ++idx) {
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
						var fixedChildren = param.Children.Count;
						Node ty;
						while ((ty = PopNode (NodeKind.Type)) != null) {
							if (paramKind != FunctionSigSpecializationParamKind.ClosureProp)
								return null;
							param = AddChild (param, ty);
						}
						var name = PopNode (NodeKind.Identifier);
						if (name == null)
							return null;
						string nameText = name.Text;
						if (paramKind == FunctionSigSpecializationParamKind.ConstantPropString && !String.IsNullOrEmpty (nameText)
						    		&& nameText [0] == '_') {
							// A '_' escapes a leading digit or '_' of a string constant.
							nameText = nameText.Substring (1);
						}
						AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamPayload, nameText));
						param.ReverseChildren (fixedChildren);
						break;
					}
				default:
					break;
				}
			}
			return spec;
		}

		Node DemangleFuncSpecParam (ulong paramIdx)
		{
			var param = new Node (NodeKind.FunctionSignatureSpecializationParam, (long)paramIdx);
			switch (NextChar ()) {
			case 'n':
				return param;
			case 'c':
				// Consumes an identifier and multiple type parameters.
				// The parameters will be added later.
				return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
				 		 (long)FunctionSigSpecializationParamKind.ClosureProp));
			case 'p': {
					switch (NextChar ()) {
					case 'f':
						// Consumes an identifier parameter, which will be added later.
						return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
							       (long)(FunctionSigSpecializationParamKind.ConstantPropFunction)));
					case 'g':
						// Consumes an identifier parameter, which will be added later.
						return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
										(long)FunctionSigSpecializationParamKind.ConstantPropGlobal));
					case 'i':
						return AddFuncSpecParamNumber (param, FunctionSigSpecializationParamKind.ConstantPropInteger);
					case 'd':
						return AddFuncSpecParamNumber (param, FunctionSigSpecializationParamKind.ConstantPropFloat);
					case 's': {
							// Consumes an identifier parameter (the string constant),
							// which will be added later.
							string encoding = null;
							switch (NextChar ()) {
							case 'b': encoding = "u8"; break;
							case 'w': encoding = "u16"; break;
							case 'c': encoding = "objc"; break;
							default: return null;
							}
							AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
								   		 (long)FunctionSigSpecializationParamKind.ConstantPropString));
							return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamPayload, encoding));
						}
					default:
						return null;
					}
				}
			case 'e': {
					uint value = (uint)FunctionSigSpecializationParamKind.ExistentialToGeneric;
					if (NextIf ('D'))
						value |= (uint)FunctionSigSpecializationParamKind.Dead;
					if (NextIf ('G'))
						value |= (uint)FunctionSigSpecializationParamKind.OwnedToGuaranteed;
					if (NextIf ('O'))
						value |= (uint)FunctionSigSpecializationParamKind.GuaranteedToOwned;
					if (NextIf ('X'))
						value |= (uint)FunctionSigSpecializationParamKind.SROA;
					return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind, value));
				}
			case 'd': {
					uint value = (uint)FunctionSigSpecializationParamKind.Dead;
					if (NextIf ('G'))
						value |= (uint)FunctionSigSpecializationParamKind.OwnedToGuaranteed;
					if (NextIf ('O'))
						value |= (uint)FunctionSigSpecializationParamKind.GuaranteedToOwned;
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
			case 'o': {
					uint value = (uint)FunctionSigSpecializationParamKind.GuaranteedToOwned;
					if (NextIf ('X'))
						value |= (uint)FunctionSigSpecializationParamKind.SROA;
					return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind, value));
				}
			case 'x':
				return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
						(uint)FunctionSigSpecializationParamKind.SROA));
			case 'i':
				return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
					  	(uint)FunctionSigSpecializationParamKind.BoxToValue));
			case 's':
				return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamKind,
						(uint)FunctionSigSpecializationParamKind.BoxToStack));
			default:
				return null;
			}
		}

		Node AddFuncSpecParamNumber (Node param, FunctionSigSpecializationParamKind kind)
		{
			param.AddChild (new Node (NodeKind.FunctionSignatureSpecializationParamKind, (uint)kind));
			var str = new StringBuilder ();
			while (Char.IsDigit (PeekChar ())) {
				str.Append (NextChar ());
			}
			if (str.Length == 0)
				return null;
			return AddChild (param, new Node (NodeKind.FunctionSignatureSpecializationParamPayload, str.ToString ()));
		}

		Node DemangleSpecAttributes (NodeKind SpecKind)
		{
			bool isFragile = NextIf ('q');

			int passID = (int)NextChar () - '0';
			if (passID < 0 || passID > 9)
				return null;

			var specNd = new Node (SpecKind);
			if (isFragile)
				specNd.AddChild (new Node (NodeKind.SpecializationIsFragile));

			specNd.AddChild (new Node (NodeKind.SpecializationPassID, passID));
			return specNd;
		}

		Node DemangleWitness ()
		{
			switch (NextChar ()) {
			case 'C':
				return CreateWithChild (NodeKind.EnumCase, PopNode (IsEntity));
			case 'V':
				return CreateWithChild (NodeKind.ValueWitnessTable, PopNode (NodeKind.Type));
			case 'v': {
					uint directness;
					switch (NextChar ()) {
					case 'd': directness = (uint)Directness.Direct; break;
					case 'i': directness = (uint)Directness.Indirect; break;
					default: return null;
					}
					return CreateWithChildren (NodeKind.FieldOffset, new Node (NodeKind.Directness, directness),
							  PopNode (IsEntity));
				}
			case 'S':
				return CreateWithChild (NodeKind.ProtocolSelfConformanceWitnessTable, PopProtocol ());
			case 'P':
				return CreateWithChild (NodeKind.ProtocolWitnessTable, PopProtocolConformance ());
			case 'p':
				return CreateWithChild (NodeKind.ProtocolWitnessTablePattern, PopProtocolConformance ());
			case 'G':
				return CreateWithChild (NodeKind.GenericProtocolWitnessTable, PopProtocolConformance ());
			case 'I':
				return CreateWithChild (NodeKind.GenericProtocolWitnessTableInstantiationFunction, PopProtocolConformance ());

			case 'r':
				return CreateWithChild (NodeKind.ResilientProtocolWitnessTable, PopProtocolConformance ());

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
					var name = PopNode (IsDeclName);
					var conf = PopProtocolConformance ();
					return CreateWithChildren (NodeKind.AssociatedTypeMetadataAccessor, conf, name);
				}
			case 'T': {
					var protoTy = PopNode (NodeKind.Type);
					var conformingType = PopAssocTypePath ();
					var conf = PopProtocolConformance ();
					return CreateWithChildren (NodeKind.AssociatedTypeWitnessTableAccessor, conf, conformingType, protoTy);
				}
			case 'b': {
					var protoTy = PopNode (NodeKind.Type);
					var conf = PopProtocolConformance ();
					return CreateWithChildren (NodeKind.BaseWitnessTableAccessor, conf, protoTy);
				}
			case 'O': {
					switch (NextChar ()) {
					case 'y': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedCopy, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedCopy, PopNode (NodeKind.Type));
						}
					case 'e': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedConsume, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedConsume, PopNode (NodeKind.Type));
						}
					case 'r': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedRetain, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedRetain, PopNode (NodeKind.Type));
						}
					case 's': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedRelease, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedRelease, PopNode (NodeKind.Type));
						}
					case 'b': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedInitializeWithTake, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedInitializeWithTake, PopNode (NodeKind.Type));
						}
					case 'c': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedInitializeWithCopy, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedInitializeWithCopy, PopNode (NodeKind.Type));
						}
					case 'd': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedAssignWithTake, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedAssignWithTake, PopNode (NodeKind.Type));
						}
					case 'f': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedAssignWithCopy, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedAssignWithCopy, PopNode (NodeKind.Type));
						}
					case 'h': {
							Node sig;
							if ((sig = PopNode (NodeKind.DependentGenericSignature)) != null)
								return CreateWithChildren (NodeKind.OutlinedDestroy, PopNode (NodeKind.Type), sig);
							return CreateWithChild (NodeKind.OutlinedDestroy, PopNode (NodeKind.Type));
						}
					default:
						return null;
					}
				}
			default:
				return null;
			}
		}

		Node DemangleSpecialType ()
		{
			char specialChar;
			switch (specialChar = NextChar ()) {
			case 'E':
				return PopFunctionType (NodeKind.NoEscapeFunctionType);
			case 'A':
				return PopFunctionType (NodeKind.EscapingAutoClosureType);
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
					var Superclass = PopNode (NodeKind.Type);
					var Protocols = DemangleProtocolList ();
					return CreateType (CreateWithChildren (NodeKind.ProtocolListWithClass, Protocols, Superclass));
				}
			case 'l': {
					var Protocols = DemangleProtocolList ();
					return CreateType (CreateWithChild (NodeKind.ProtocolListWithAnyObject, Protocols));
				}
			case 'X':
			case 'x': {
					// SIL box types.
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
					// Build layout.
					var layout = new Node (NodeKind.SILBoxLayout);
					for (int i = 0, e = fieldTypes.Children.Count; i < e; ++i) {
						var fieldType = fieldTypes.Children [i];
						bool isMutable = false;
						// 'inout' typelist mangling is used to represent mutable fields.
						if (fieldType.Children [0].Kind == NodeKind.InOut) {
							isMutable = true;
							fieldType = CreateType (fieldType.Children [0].Children [0]);
						}
						var field = new Node (isMutable
										 ? NodeKind.SILBoxMutableField
										 : NodeKind.SILBoxImmutableField);
						field.AddChild (fieldType);
						layout.AddChild (field);
					}
					var boxTy = new Node (NodeKind.SILBoxTypeWithLayout);
					boxTy.AddChild (layout);
					if (signature != null) {
						boxTy.AddChild (signature);
						boxTy.AddChild (genericArgs);
					}
					return CreateType (boxTy);
				}
			case 'Y':
				return DemangleAnyGenericType (NodeKind.OtherNominalType);
			case 'Z': {
					var types = PopTypeList ();
					var name = PopNode (NodeKind.Identifier);
					var parent = PopContext ();
					var anon = new Node (NodeKind.AnonymousContext);
					anon = AddChild (anon, name);
					anon = AddChild (anon, parent);
					anon = AddChild (anon, types);
					return anon;
				}
			case 'e':
				return CreateType (new Node (NodeKind.ErrorType));
			default:
				return null;
			}
		}

		Node DemangleMetatypeRepresentation ()
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

		Node DemangleAccessor (Node childNode)
		{
			NodeKind kind;
			switch (NextChar ()) {
			case 'm': kind = NodeKind.MaterializeForSet; break;
			case 's': kind = NodeKind.Setter; break;
			case 'g': kind = NodeKind.Getter; break;
			case 'G': kind = NodeKind.GlobalGetter; break;
			case 'w': kind = NodeKind.WillSet; break;
			case 'W': kind = NodeKind.DidSet; break;
			case 'r': kind = NodeKind.ReadAccessor; break;
			case 'M': kind = NodeKind.ModifyAccessor; break;
			case 'a':
				switch (NextChar ()) {
				case 'O': kind = NodeKind.OwningMutableAddressor; break;
				case 'o': kind = NodeKind.NativeOwningMutableAddressor; break;
				case 'P': kind = NodeKind.NativePinningMutableAddressor; break;
				case 'u': kind = NodeKind.UnsafeMutableAddressor; break;
				default: return null;
				}
				break;
			case 'l':
				switch (NextChar ()) {
				case 'O': kind = NodeKind.OwningAddressor; break;
				case 'o': kind = NodeKind.NativeOwningAddressor; break;
				case 'p': kind = NodeKind.NativePinningAddressor; break;
				case 'u': kind = NodeKind.UnsafeAddressor; break;
				default: return null;
				}
				break;
			case 'p': // Pseudo-accessor referring to the variable/subscript itself
				return childNode;
			default: return null;
			}
			var entity = CreateWithChild (kind, childNode);
			return entity;
		}

		enum Args {
			None,
			TypeAndMaybePrivateName,
			TypeAndIndex,
			Index
		}

		Node DemangleFunctionEntity ()
		{
			var args = Args.None;
			NodeKind Kind = NodeKind.EmptyList;
			switch (NextChar ()) {
			case 'D': args = Args.None; Kind = NodeKind.Deallocator; break;
			case 'd': args = Args.None; Kind = NodeKind.Destructor; break;
			case 'E': args = Args.None; Kind = NodeKind.IVarDestroyer; break;
			case 'e': args = Args.None; Kind = NodeKind.IVarInitializer; break;
			case 'i': args = Args.None; Kind = NodeKind.Initializer; break;
			case 'C':
				args = Args.TypeAndMaybePrivateName; Kind = NodeKind.Allocator; break;
			case 'c':
				args = Args.TypeAndMaybePrivateName; Kind = NodeKind.Constructor; break;
			case 'U': args = Args.TypeAndIndex; Kind = NodeKind.ExplicitClosure; break;
			case 'u': args = Args.TypeAndIndex; Kind = NodeKind.ImplicitClosure; break;
			case 'A': args = Args.Index; Kind = NodeKind.DefaultArgumentInitializer; break;
			case 'p': return DemangleEntity (NodeKind.GenericTypeParamDecl);
			default: return null;
			}

			Node nameOrIndex = null, paramType = null, labelList = null;
			switch (args) {
			case Args.None:
				break;
			case Args.TypeAndMaybePrivateName:
				nameOrIndex = PopNode (NodeKind.PrivateDeclName);
				paramType = PopNode (NodeKind.Type);
				labelList = PopFunctionParamLabels (paramType);
				break;
			case Args.TypeAndIndex:
				nameOrIndex = DemangleIndexAsNode ();
				paramType = PopNode (NodeKind.Type);
				break;
			case Args.Index:
				nameOrIndex = DemangleIndexAsNode ();
				break;
			}
			var entity = CreateWithChild (Kind, PopContext ());
			switch (args) {
			case Args.None:
				break;
			case Args.Index:
				entity = AddChild (entity, nameOrIndex);
				break;
			case Args.TypeAndMaybePrivateName:
				AddChild (entity, labelList);
				entity = AddChild (entity, paramType);
				AddChild (entity, nameOrIndex);
				break;
			case Args.TypeAndIndex:
				entity = AddChild (entity, nameOrIndex);
				entity = AddChild (entity, paramType);
				break;
			}
			return entity;
		}

		Node DemangleEntity (NodeKind Kind)
		{
			var type = PopNode (NodeKind.Type);
			var labelList = PopFunctionParamLabels (type);
			var name = PopNode (IsDeclName);
			var context = PopContext ();
			return labelList != null ? CreateWithChildren (Kind, context, name, labelList, type)
					 : CreateWithChildren (Kind, context, name, type);
		}

		Node DemangleVariable ()
		{
			var variable = DemangleEntity (NodeKind.Variable);
			return DemangleAccessor (variable);
		}

		Node DemangleSubscript ()
		{
			var privateName = PopNode (NodeKind.PrivateDeclName);
			var type = PopNode (NodeKind.Type);
			var labelList = PopFunctionParamLabels (type);
			var context = PopContext ();

			var subscript = new Node (NodeKind.Subscript);
			subscript = AddChild (subscript, context);
			AddChild (subscript, labelList);
			subscript = AddChild (subscript, type);
			AddChild (subscript, privateName);

			return DemangleAccessor (subscript);
		}

		Node DemangleProtocolList ()
		{
			var typeList = new Node (NodeKind.TypeList);
			var protoList = CreateWithChild (NodeKind.ProtocolList, typeList);
			if (PopNode (NodeKind.EmptyList) == null) {
				bool firstElem = false;
				do {
					firstElem = (PopNode (NodeKind.FirstElementMarker) != null);
					var proto = PopProtocol ();
					if (proto == null)
						return null;
					typeList.AddChild (proto);
				} while (!firstElem);

				typeList.ReverseChildren ();
			}
			return protoList;
		}

		Node DemangleProtocolListType ()
		{
			var protoList = DemangleProtocolList ();
			return CreateType (protoList);
		}

		Node DemangleGenericSignature (bool hasParamCounts)
		{
			var Sig = new Node (NodeKind.DependentGenericSignature);
			if (hasParamCounts) {
				while (!NextIf ('l')) {
					int count = 0;
					if (!NextIf ('z'))
						count = DemangleIndex () + 1;
					if (count < 0)
						return null;
					Sig.AddChild (new Node (NodeKind.DependentGenericParamCount, count));
				}
			} else {
				Sig.AddChild (new Node (NodeKind.DependentGenericParamCount, 1));
			}
			var NumCounts = Sig.Children.Count;
			Node Req;
			while ((Req = PopNode (IsRequirement)) != null) {
				Sig.AddChild (Req);
			}
			Sig.ReverseChildren (NumCounts);
			return Sig;
		}

		enum TypeKind {
			Generic,
			Assoc,
			CompoundAssoc,
			Substitution
		}

		enum ConstraintKind {
			Protocol,
			BaseClass,
			SameType,
			Layout
		}

		Node DemangleGenericRequirement ()
		{
			TypeKind typeKind;
			ConstraintKind constraintKind;

			switch (NextChar ()) {
			case 'c': constraintKind = ConstraintKind.BaseClass; typeKind = TypeKind.Assoc; break;
			case 'C': constraintKind = ConstraintKind.BaseClass; typeKind = TypeKind.CompoundAssoc; break;
			case 'b': constraintKind = ConstraintKind.BaseClass; typeKind = TypeKind.Generic; break;
			case 'B': constraintKind = ConstraintKind.BaseClass; typeKind = TypeKind.Substitution; break;
			case 't': constraintKind = ConstraintKind.SameType; typeKind = TypeKind.Assoc; break;
			case 'T': constraintKind = ConstraintKind.SameType; typeKind = TypeKind.CompoundAssoc; break;
			case 's': constraintKind = ConstraintKind.SameType; typeKind = TypeKind.Generic; break;
			case 'S': constraintKind = ConstraintKind.SameType; typeKind = TypeKind.Substitution; break;
			case 'm': constraintKind = ConstraintKind.Layout; typeKind = TypeKind.Assoc; break;
			case 'M': constraintKind = ConstraintKind.Layout; typeKind = TypeKind.CompoundAssoc; break;
			case 'l': constraintKind = ConstraintKind.Layout; typeKind = TypeKind.Generic; break;
			case 'L': constraintKind = ConstraintKind.Layout; typeKind = TypeKind.Substitution; break;
			case 'p': constraintKind = ConstraintKind.Protocol; typeKind = TypeKind.Assoc; break;
			case 'P': constraintKind = ConstraintKind.Protocol; typeKind = TypeKind.CompoundAssoc; break;
			case 'Q': constraintKind = ConstraintKind.Protocol; typeKind = TypeKind.Substitution; break;
			default: constraintKind = ConstraintKind.Protocol; typeKind = TypeKind.Generic; PushBack (); break;
			}

			Node ConstrTy = null;

			switch (typeKind) {
			case TypeKind.Generic:
				ConstrTy = CreateType (DemangleGenericParamIndex ());
				break;
			case TypeKind.Assoc:
				ConstrTy = DemangleAssociatedTypeSimple (DemangleGenericParamIndex ());
				AddSubstitution (ConstrTy);
				break;
			case TypeKind.CompoundAssoc:
				ConstrTy = DemangleAssociatedTypeCompound (DemangleGenericParamIndex ());
				AddSubstitution (ConstrTy);
				break;
			case TypeKind.Substitution:
				ConstrTy = PopNode (NodeKind.Type);
				break;
			}

			switch (constraintKind) {
			case ConstraintKind.Protocol:
				return CreateWithChildren (NodeKind.DependentGenericConformanceRequirement, ConstrTy, PopProtocol ());
			case ConstraintKind.BaseClass:
				return CreateWithChildren (NodeKind.DependentGenericConformanceRequirement, ConstrTy, PopNode (NodeKind.Type));
			case ConstraintKind.SameType:
				return CreateWithChildren (NodeKind.DependentGenericSameTypeRequirement, ConstrTy, PopNode (NodeKind.Type));
			case ConstraintKind.Layout: {
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
						alignment = DemangleIndexAsNode ();
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
						// Unknown layout constraint.
						return null;
					}

					var NameNode = new Node (NodeKind.Identifier, name);
					var LayoutRequirement = CreateWithChildren (NodeKind.DependentGenericLayoutRequirement, ConstrTy, NameNode);
					if (size != null)
						AddChild (LayoutRequirement, size);
					if (alignment != null)
						AddChild (LayoutRequirement, alignment);
					return LayoutRequirement;
				}
			}
			return null;
		}

		Node DemangleGenericType ()
		{
			var genSig = PopNode (NodeKind.DependentGenericSignature);
			var ty = PopNode (NodeKind.Type);
			return CreateType (CreateWithChildren (NodeKind.DependentGenericType, genSig, ty));
		}

		int DecodeValueWitnessKind (string codeStr)
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
			case "et": return (int)ValueWitnessKind.GetEnumTagSinglePayload;
			case "st": return (int)ValueWitnessKind.StoreEnumTagSinglePayload;
			default:
				return -1;
			}
		}

		Node DemangleValueWitness ()
		{
			char [] code = new char [2];
			code [0] = NextChar ();
			code [1] = NextChar ();
			int kind = DecodeValueWitnessKind (new string (code));
			if (kind < 0)
				return null;
			var vw = new Node (NodeKind.ValueWitness, (uint)kind);
			return AddChild (vw, PopNode (NodeKind.Type));
		}

		Node DemangleObjCTypeName ()
		{
			var ty = new Node (NodeKind.Type);
			var global = AddChild (new Node (NodeKind.Global), AddChild (new Node (NodeKind.TypeMangling), ty));
			Node nominal = null;
			bool isProto = false;
			if (NextIf ('C')) {
				nominal = new Node (NodeKind.Class);
				AddChild (ty, nominal);
			} else if (NextIf ('P')) {
				isProto = true;
				nominal = new Node (NodeKind.Protocol);
				AddChild (ty, AddChild (new Node (NodeKind.ProtocolList),
						AddChild (new Node (NodeKind.TypeList),
						  AddChild (new Node (NodeKind.Type), nominal))));
			} else {
				return null;
			}

			if (NextIf ('s')) {
				nominal.AddChild (new Node (NodeKind.Module, "Swift"));
			} else {
				var Module = DemangleIdentifier ();
				if (Module == null)
					return null;
				nominal.AddChild (ChangeKind (Module, NodeKind.Module));
			}

			var ident = DemangleIdentifier ();
			if (ident == null)
				return null;
			nominal.AddChild (ident);

			if (isProto && !NextIf ('_'))
				return null;

			if (!slice.IsAtEnd)
				return null;

			return global;
		}

	}
}
