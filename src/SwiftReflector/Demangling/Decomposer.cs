// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftReflector.ExceptionTools;
using System.Text;

namespace SwiftReflector.Demangling {
	public class Decomposer {
		const string kSwiftID = "__T";
		public const string kSwift4ID = "__T0";
		public const string kSwift4xID = "_$s";
		public const string kSwift5ID = "_$S";
		public static SwiftName kSwiftAllocatingConstructorName = new SwiftName (".ctor", false);
		public static SwiftName kSwiftNonAllocatingConstructorName = new SwiftName (".nctor", false);
		public static SwiftName kSwiftClassConstructorName = new SwiftName (".cctor", false);
		public static SwiftName kSwiftDeallocatingDestructorName = new SwiftName (".dtor", false);
		public static SwiftName kSwiftNonDeallocatingDestructorName = new SwiftName (".ndtor", false);
		public static SwiftName kSwiftWitnessTableName = new SwiftName (".wtable", false);
		public static SwiftName kSwiftValueWitnessTableName = new SwiftName (".vwtable", false);
		public static SwiftName kSwiftProtocolWitnessTableName = new SwiftName (".pwtable", false);
		public static SwiftName kSwiftProtocolWitnessTableAccessorName = new SwiftName (".pwtablea", false);

		List<SwiftType> subs = new List<SwiftType> ();

		List<SwiftClassType> classes = new List<SwiftClassType> ();
		int position;
		StringSlice slice;
		string originalID;
		SwiftName thisModule;
		bool isOldVersion;
		ulong offset;


		Decomposer (string swiftIdentifier, bool isOldVersion, ulong offset)
		{
			if (swiftIdentifier == null)
				throw new ArgumentNullException (nameof(swiftIdentifier));
			if (swiftIdentifier.Length == 0)
				throw new ArgumentException ("swiftname is empty", nameof(swiftIdentifier));
			originalID = swiftIdentifier;
			this.isOldVersion = isOldVersion;
			this.offset = offset;
		}

		int MarkOperatorPosition ()
		{
			position = slice.Position;
			return position;
		}

		void Fail (string expected, string butGot = null)
		{
			if (butGot == null) {
				butGot = slice.IsAtEnd ? "nothing at end of string" : slice.Current.ToString ();
			}
			var message = $"Error decomposing {slice.Original} at position {slice.Position}: expected {expected}, but got {butGot}";
			throw ErrorHelper.CreateError (ReflectorError.kDecomposeBase + 1, message);
		}

		void Initialize ()
		{
			thisModule = null;
			classes.Clear ();
			slice = new StringSlice (originalID);
			MarkOperatorPosition ();
		}

		public static TLDefinition Decompose (string swiftIdentifier, bool isOldVersion, ulong offset = 0)
		{
			if (swiftIdentifier.StartsWith (kSwift4xID, StringComparison.Ordinal)||
				swiftIdentifier.StartsWith (kSwift5ID, StringComparison.Ordinal)) {
				var demangler5 = new Swift5Demangler (swiftIdentifier, offset);
				return demangler5.Run ();
			} else {
				Decomposer decomp = new Decomposer (swiftIdentifier, isOldVersion, offset);
				return decomp.Run ();
			}
		}

		public TLDefinition Run ()
		{
			Initialize ();

			RemoveSwiftID ();

			char c = slice.Advance ();
			switch (c) {
			case 'Z':
				if (slice.AdvanceIfEquals ('F')) {
					return ToFunction (true, false);
				} else if (slice.AdvanceIfEquals ('v')) {
					return ToVariable (true);
				} else {
					Fail ("a static function (F)");
				}
				break;
			case 'F': 
				return ToFunction (false, false);
			case 'M':
				return ToMetadata ();
			case 'W': 
				return ToWitnessTable ();
			case 'v':
				return ToVariable (false);
			case 'T':
				return ToThunk ();
			case 'I':
				return ToInitializer ();
			default:
				Fail ("one of Z, F, M, v, T, I or W", c.ToString ());
				break;
			}
			return null;
		}

		void RemoveSwiftID ()
		{
			if (!slice.StartsWith (kSwiftID))
				Fail ("Swift symbol (__T)", slice.Original.Substring (0, Math.Min (3, slice.Original.Length)));
			slice.Advance (kSwiftID.Length);
		}

		SwiftName GetModule ()
		{
			return slice.ExtractSwiftName ();
		}

		public static MemberNesting? ToMaybeMemberNesting (char c, bool throwOnFail)
		{
			switch (c) {
			case 'C':
				return MemberNesting.Class;
			case 'V':
				return MemberNesting.Struct;
			case 'O':
				return MemberNesting.Enum;
			case 'P':
				return MemberNesting.Protocol;
			default:
				if (throwOnFail)
					throw new ArgumentOutOfRangeException (nameof(c));
				return null;
			}
		}

		MemberNesting ToMemberNesting (char c)
		{
			MemberNesting? nesting = ToMaybeMemberNesting (c, false);
			if (!nesting.HasValue) {
				Fail ("either a struct, class, enum, or protocol nesting marker (C, V, O, or P)");
			}
			return nesting.Value;
		}

		MemberType ToMemberType (char c)
		{
			switch (c) {
			case 'C':
				return MemberType.Allocator;
			case 'c':
				return MemberType.Constructor;
			case 'D':
				return MemberType.Deallocator;
			case 'd':
				return MemberType.Destructor;
			case 'f':
				return MemberType.UncurriedFunction;
			case 'F':
				return MemberType.Function;
			case 'g':
				return MemberType.Getter;
			case 's':
				return MemberType.Setter;
			default:
				Fail ("a member type marker (one of C, c, D, d, F, f, g, or s");
				return MemberType.Allocator; // never hit, thanks C#, you're the best
			}
		}

		CoreBuiltInType ToCoreScalar (char c)
		{
			switch (c) {
			case 'b':
				return CoreBuiltInType.Bool;
			case 'd':
				return CoreBuiltInType.Double;
			case 'f':
				return CoreBuiltInType.Float;
			case 'i':
				return CoreBuiltInType.Int;
			case 'u':
				return CoreBuiltInType.UInt;
			default:
				Fail ("a core type marker (one of b, d, f, i, or u)");
				return CoreBuiltInType.Bool; // never hit, thanks C#, you're the best
			}
		}

		IList<MemberNesting> GetNesting ()
		{
			Stack<MemberNesting> st = new Stack<MemberNesting> ();
			while (!Char.IsDigit (slice.Current)) {
				MarkOperatorPosition ();
				st.Push (ToMemberNesting (slice.Advance ()));
			}
			return st.ToList ();
		}

		IList<MemberNesting> GetNestingAlt ()
		{
			Stack<MemberNesting> st = new Stack<MemberNesting> ();
			while (!slice.StartsWith ('S') && !slice.StartsWith ('s') && !Char.IsDigit (slice.Current)) {
				MarkOperatorPosition ();
				st.Push (ToMemberNesting (slice.Advance ()));
			}
			return st.ToList ();
		}

		IList<SwiftName> GetNestingNames (int expected)
		{
			List<SwiftName> names = new List<SwiftName> (expected);
			for (int i = 0; i < expected; i++) {
				MarkOperatorPosition ();
				SwiftName sw = slice.ExtractSwiftName ();
				if (sw == null)
					Fail (String.Format ("{0} nested class names", expected));
				names.Add (sw);
			}
			return names;
		}

		SwiftClassName ReadProtocolName ()
		{
			MarkOperatorPosition ();
			// module?
			IList<MemberNesting> nesting = new List<MemberNesting> { MemberNesting.Protocol };

			SwiftType st = ReadContext ();
			SwiftClassType ct = st as SwiftClassType;
			if (ct == null) {
				SwiftModuleNameType mod = st as SwiftModuleNameType;
				if (mod == null)
					Fail ("SwiftModuleNameType", st.GetType ().Name);
				ct = new SwiftClassType (new SwiftClassName (mod.Name, new List<MemberNesting> (), new List<SwiftName> ()), false, null);
			}


			IList<SwiftName> nestingNames = GetNestingNames (nesting.Count);
			return new SwiftClassName (ct.ClassName.Module, nesting, nestingNames);
		}

		SwiftClassName CombineContextAndName (SwiftType context, SwiftName terminus, MemberNesting nesting, OperatorType oper)
		{
			if (context is SwiftModuleNameType) {
				return new SwiftClassName (context.Name, new List<MemberNesting> { nesting },
				                           new List<SwiftName> { terminus });
			}
			if (context is SwiftClassType) {
				SwiftClassType ct = context as SwiftClassType;
				List<MemberNesting> newnesting = new List<MemberNesting> ();
				newnesting.AddRange (ct.ClassName.Nesting);
				newnesting.Add (nesting);
				List<SwiftName> newnames = new List<SwiftName> ();
				newnames.AddRange (ct.ClassName.NestingNames);
				newnames.Add (terminus);
				return new SwiftClassName (ct.ClassName.Module, newnesting, newnames, oper);
			}
			Fail ("a SwiftModuleNameType or a SwiftClassType", context.GetType ().Name);
			return null; // never reached - thanks C#
		}

		SwiftClassName ReadClassStructEnumNameAlt ()
		{
			if (!slice.StartsWith ('C') && !slice.StartsWith ('V') && !slice.StartsWith ('O'))
				Fail ("class, struct, or enum markers (C, V, or O)");
			MarkOperatorPosition ();

			MemberNesting nesting = ToMemberNesting (slice.Advance ());
			SwiftType context = ReadContext ();
			OperatorType oper = OperatorType.None;
			SwiftName name = slice.ExtractSwiftNameMaybeOperator (out oper);
			SwiftClassName className = CombineContextAndName (context, name, nesting, oper);
			return className;
		}

		public SwiftClassType ReadClassStructOrEnum (SwiftName name, bool isReference)
		{
			if (!slice.StartsWith ('C') && !slice.StartsWith ('V') && !slice.StartsWith ('O'))
				Fail ("class, struct, or enum markers (C, V, or O)");
			MarkOperatorPosition ();
			SwiftClassName cn = ReadClassStructEnumNameAlt ();
			SwiftClassType ct = new SwiftClassType (cn, isReference, name);
			subs.Add (ct);
			return ct;
		}


		SwiftType ReadContext ()
		{
			if (slice.Current == 'S') {
				return DemangleSubstitution (null, false);
			}
			if (slice.AdvanceIfEquals ('s')) {
				return new SwiftModuleNameType (new SwiftName ("Swift", false), false);
			}
			if (slice.StartsWith ('C') || slice.StartsWith ('V') || slice.StartsWith ('O')) {
				return ReadClassStructOrEnum (null, false);
			}
			SwiftName module = GetModule ();
			SwiftModuleNameType modName = new SwiftModuleNameType (module, false);
			subs.Add (modName);
			return modName;
		}


		TLDefinition ToMetadata ()
		{
			MarkOperatorPosition ();
			// current version doesn't use 'd', but is implicit


			char c = (slice.Current == 'C' || slice.Current == 'V' || slice.Current == 'O') ? 'd' : slice.Advance ();
			SwiftClassName className = c != 'p' ? ReadClassStructEnumNameAlt () : ReadProtocolName ();
			if (className.Module != null && thisModule == null)
				thisModule = className.Module;

			SwiftClassType cl = new SwiftClassType (className, false);
			subs.Add (cl);

			switch (c) {
			case 'a':
				return new TLFunction (slice.Original, thisModule, kSwiftClassConstructorName, cl,
					new SwiftClassConstructorType (new SwiftMetaClassType (cl, false), false), offset);
			case 'L':
				return new TLLazyCacheVariable (slice.Original, thisModule, cl, offset);
			case 'd':
				return new TLDirectMetadata (slice.Original, thisModule, cl, offset);
			case 'm':
				return new TLMetaclass (slice.Original, thisModule, cl, offset);
			case 'n':
				return new TLNominalTypeDescriptor (slice.Original, thisModule, cl, offset);
			case 'p':
				return new TLProtocolTypeDescriptor (slice.Original, thisModule, cl, offset);
			case 'P':
				return new TLGenericMetadataPattern (slice.Original, thisModule, cl, offset);
			default:
				Fail ("a metaclass descriptor (a, L, d, m, or n, p)", c.ToString ());
				return null;
			}
		}

		TLVariable ToVariable (bool isStatic)
		{
			MarkOperatorPosition ();
			SwiftType st = ReadContext ();
			SwiftClassType cl = st as SwiftClassType;
			if (cl == null) {
				SwiftModuleNameType mod = st as SwiftModuleNameType;
				if (mod == null)
					Fail ("a SwiftModuleType", st.GetType ().Name);
				cl = new SwiftClassType (new SwiftClassName (mod.Name, new List<MemberNesting> (),
				                                             new List<SwiftName> ()), false);
			}
			SwiftName module = cl.ClassName.Module;
			if (cl.ClassName.Nesting.Count == 0)
				cl = null;
			SwiftName varName = slice.ExtractSwiftName ();
			SwiftType ofType = ReadType (false);
			return new TLVariable (slice.Original, module, cl, varName, ofType, isStatic, offset);
		}

		TLThunk ToThunk ()
		{
			MarkOperatorPosition ();
			ThunkType thunkType = ToThunkType ();
			SwiftClassName className = ReadClassStructEnumNameAlt ();
			if (className.Module != null && thisModule == null)
				thisModule = className.Module;

			SwiftClassType cl = new SwiftClassType (className, false);
			return new TLThunk (thunkType, slice.Original, thisModule, cl, offset);
		}

		ThunkType ToThunkType ()
		{
			char c = slice.Advance ();
			switch (c) {
			case 'R':
				return ThunkType.ReabstractionHelper;
			case 'r':
				return ThunkType.Reabstraction;
			case 'W':
				return ThunkType.ProtocolWitness;
			default:
				Fail ("a thunk type marker, one of R, r, or W", c.ToString ());
				return ThunkType.ProtocolWitness; // can't happen
			}
		}

		TLDefinition ToInitializer ()
		{
			MarkOperatorPosition ();
			if (slice.AdvanceIfEquals ('v')) {
				return ToInitializer (InitializerType.Variable);
			} else {
				Fail ("expected a variable initializer marker (v)");
				return null; // stupid compile
			}
		}

		TLDefinition ToInitializer (InitializerType type)
		{
			SwiftType readType = ReadContext ();
			SwiftClassType owner = readType as SwiftClassType;
			if (owner == null)
				Fail ("SwiftClassType", readType.GetType ().Name);
			thisModule = owner.ClassName.Module;

			SwiftName varName = slice.ExtractSwiftName ();
			SwiftType varType = ReadType (false);
			return new TLFunction (slice.Original, thisModule,
			                       varName, owner, 
			                       new SwiftInitializerType (InitializerType.Variable, varType, owner, varName),
			                       offset);
		}


		TLDefinition ToWitnessTable ()
		{
			MarkOperatorPosition ();
			if (slice.StartsWith ('v'))
				return ToFieldOffset ();
			if (slice.StartsWith ('V'))
				return ToValueWitnessTable ();
			if (slice.StartsWith ('P'))
				return ToProtocolWitnessTable ();
			if (slice.StartsWith ('a'))
				return ToProtocolWitnessTableAccessor ();
			if (!slice.AdvanceIfEquals ('o'))
				Fail ("expected witness table offset marker (o)");
			if (!slice.AdvanceIfEquals ('F'))
				Fail ("expected a function marker (F) after witness table offset");

			SwiftClassName className = ReadClassStructEnumNameAlt ();
			if (className.Module != null && thisModule == null)
				thisModule = className.Module;

			SwiftClassType cl = new SwiftClassType (className, false);
			subs.Add (cl);

			MarkOperatorPosition ();

			// A witness table is not strictly a function, but the overall structure of its descriptor
			// matches an uncurried function closely enough that we can make it pass for one.
			// The consuming code doesn't care and pulls it out as is.
			SwiftWitnessTableType wit = new SwiftWitnessTableType (WitnessType.Class);

			TLFunction func = new TLFunction (slice.Original, thisModule, null, cl, wit, offset);
			return func;
		}

		TLDefinition ToValueWitnessTable ()
		{
			return ToWitnessFoo ('V', "value witness table", WitnessType.Value);
		}

		TLDefinition ToProtocolWitnessTable ()
		{
			return ToWitnessFoo ('P', "protocol witness table", WitnessType.Protocol);
		}

		TLDefinition ToProtocolWitnessTableAccessor ()
		{
			return ToWitnessFoo ('a', "protocol witness table accessor", WitnessType.ProtocolAccessor);
		}

		TLDefinition ToWitnessFoo (char marker, string descriptor, WitnessType witnessType)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals (marker))
				Fail (String.Format ("{0} marker ({1})", descriptor, marker));

			SwiftClassName className = ReadClassStructEnumNameAlt ();
			if (className.Module != null && thisModule == null)
				thisModule = className.Module;

			SwiftClassType cl = new SwiftClassType (className, false);
			subs.Add (cl);


			SwiftClassName protoName = witnessType == WitnessType.Protocol ? ReadProtocolName () : null;
			SwiftClassType proto = protoName != null ? new SwiftClassType (protoName, false) : null;

			MarkOperatorPosition ();
			// this is likely wrong, but it's less wrong than it was
			SwiftWitnessTableType witf = new SwiftWitnessTableType (witnessType, proto);
			TLFunction func = new TLFunction (slice.Original, thisModule, null, cl, witf, offset);
			return func;
		}

		TLDefinition ToFieldOffset ()
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('v'))
				Fail ("a field offset marker (v)");
			if (!(slice.StartsWith ('d') || slice.StartsWith ('i')))
				Fail ("a direct or indirect marker (d, i)");
			bool direct = slice.StartsWith ('d');
			slice.Advance ();

			slice.Advance ();

			SwiftClassType cl = ReadType (false) as SwiftClassType;
			if (cl == null)
				Fail ("a class descriptor");
			thisModule = cl.ClassName.Module;

			SwiftName ident = slice.ExtractSwiftName ();
			SwiftType type = ReadType (false);

			return new TLFieldOffset (slice.Original, thisModule, cl, direct, ident, type, offset);
		}


		TLFunction ToFunction (bool isStatic, bool isReference)
		{
			MarkOperatorPosition ();
			if (slice.StartsWith ('F')) {
				return ReadExplicitClosure (isReference);
			}

			SwiftType st = ReadContext ();
			SwiftClassType cl = st as SwiftClassType;
			if (cl == null) {
				SwiftModuleNameType mod = st as SwiftModuleNameType;
				if (mod == null)
					Fail ("a SwiftModuleType", st.GetType ().Name);
				cl = new SwiftClassType (new SwiftClassName (mod.Name, new List<MemberNesting> (),
					new List<SwiftName> ()), false);
			}

			MarkOperatorPosition ();
			OperatorType oper = OperatorType.None;
			SwiftName functionName = slice.ExtractSwiftNameMaybeOperator (out oper);


			SwiftBaseFunctionType baseFunc = null;

			switch (slice.Current) {
			case 'C':
				baseFunc = ReadAllocatorType (isReference);
				break;
			case 'c':
				baseFunc = ReadConstructorType (isReference);
				break;
			case 'D':
				baseFunc = new SwiftDestructorType (true, cl, isReference, false);
				break;
			case 'd':
				baseFunc = new SwiftDestructorType (false, cl, isReference, false);
				break;
			case 'g':
				baseFunc = ReadGetter (cl, isStatic, isReference);
				break;
			case 's':
				baseFunc = ReadSetter (cl, isStatic, isReference);
				break;
			case 'm':
				baseFunc = ReadMaterializer (cl, isStatic, isReference);
				break;
			case 'a':
				baseFunc = ReadAddressorMutable (cl, isReference);
				break;
			case 'l':
				baseFunc = ReadAddressor (cl, isReference);
				break;
			default:
				baseFunc = ReadType (isReference) as SwiftBaseFunctionType;
				if (isStatic && baseFunc is SwiftUncurriedFunctionType) {
					SwiftUncurriedFunctionType ucf = baseFunc as SwiftUncurriedFunctionType;
					baseFunc = new SwiftStaticFunctionType (ucf.Parameters, ucf.ReturnType, ucf.IsReference, ucf.CanThrow, ucf.UncurriedParameter as SwiftClassType, ucf.Name);
				}
				break;
			}


			if (baseFunc == null)
				Fail ("a function");

			functionName = functionName ?? baseFunc.Name;

			if (functionName == null)
				throw new ArgumentNullException ("blah");

			return new TLFunction (slice.Original, cl.ClassName.Module, functionName, cl, baseFunc, offset, oper);
		}

		SwiftType ReadType (bool isReference, SwiftName name = null)
		{
			name = slice.IsNameNext ? slice.ExtractSwiftName () : name;
			char t = slice.Current;
			MarkOperatorPosition ();
			switch (t) {
			case 'f':
				return ReadUncurriedFunctionType (name, isReference, subs.Last () as SwiftClassType);

			case 'F':
				return ReadFunctionType (name, isReference);
			case 'c':
				return ReadCFunctionType (name, isReference);
			case 'G':
				return ReadBoundGenericType (name, isReference);
			case 'S':
				return DemangleSubstitution (name, isReference);
			case 'V':
			case 'C':
			case 'O':
				return ReadClassOrStructOrEnum (name, isReference);
			case 'P':
				return ReadProtocolList (name, isReference);
			case 'M':
				return ReadMetaClassType (name, isReference);
			case 'R':
				slice.Advance ();
				return ReadType (true, name);
			case 'T':
				return ReadTupleType (name, isReference);
			case 'u':
				return ReadUnboundGenericType (name, isReference);
			case 'x':
				slice.Advance ();
				return new SwiftGenericArgReferenceType (0, 0, isReference, name);
			case 'q':
				return ReadGenericReferenceType (name, isReference);
			default:
				Fail ("a type marker (one of F, G, S, V, C, O, M, R, T, u, x, or q)");
				return null;
			}
		}

		TLFunction ReadExplicitClosure (bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('F'))
				Fail ("an explicit closure marker (F)");

			SwiftClassType st = ReadType (isReference) as SwiftClassType;
			if (st == null)
				Fail ("a class type");
			return new TLFunction (slice.Original, st.ClassName.Module, st.Name, st, new SwiftExplicitClosureType (isReference), offset);
		}

		SwiftUncurriedFunctionType ReadUncurriedFunctionType (SwiftName name, bool isReference, SwiftType implicitType)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('f'))
				Fail ("an uncurried function type (f)");
			bool canThrow = slice.AdvanceIfEquals ('z');
			if (isOldVersion) {
				SwiftType uncurriedParamType = ReadType (false);
				SwiftFunctionType func = ReadFunctionType (null, false);
				return new SwiftUncurriedFunctionType (uncurriedParamType, func.Parameters, func.ReturnType, isReference, canThrow, name);
			} else {
				SwiftType args = ReadType (false);
				SwiftType ret = ReadType (false);
				return new SwiftUncurriedFunctionType (implicitType, args, ret, isReference, canThrow, name);
			}
		}

		SwiftConstructorType ReadAllocOrCons (bool isAllocator, char advanceOn, string expected, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals (advanceOn))
				Fail (expected);
			SwiftUncurriedFunctionType ucf = ReadUncurriedFunctionType (null, false, new SwiftMetaClassType (subs.Last () as SwiftClassType, false, null));
			return new SwiftConstructorType (isAllocator, ucf.UncurriedParameter, ucf.Parameters, ucf.ReturnType, isReference, ucf.CanThrow);
		}

		SwiftAddressorType ReadAddressorMutable (SwiftClassType cl, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('a'))
				Fail ("an addressor");
			AddressorType theType = AddressorType.UnsafeMutable;
			char c = slice.Advance ();
			switch (c) {
			case 'O':
				theType = AddressorType.OwningMutable;
				break;
			case 'o':
				theType = AddressorType.NativeOwningMutable;
				break;
			case 'p':
				theType = AddressorType.NativePinningMutable;
				break;
			case 'u':
				theType = AddressorType.UnsafeMutable;
				break;
			default:
				Fail ("one of O, o, p, or u", c.ToString ());
				break;
			}
			if (!slice.IsNameNext)
				Fail ("a swift name");
			SwiftName addressorName = slice.ExtractSwiftName ();
			SwiftType ofType = ReadType (true); // addressors return the address of the entity
			return new SwiftAddressorType (theType, ofType, isReference, addressorName);
		}

		SwiftAddressorType ReadAddressor (SwiftClassType cl, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('l'))
				Fail ("an addressor");
			AddressorType theType = AddressorType.UnsafeMutable;
			char c = slice.Advance ();
			switch (c) {
			case 'O':
				theType = AddressorType.Owning;
				break;
			case 'o':
				theType = AddressorType.NativeOwning;
				break;
			case 'p':
				theType = AddressorType.NativePinning;
				break;
			case 'u':
				theType = AddressorType.Unsafe;
				break;
			default:
				Fail ("one of O, o, p, or u", c.ToString ());
				break;
			}
			if (!slice.IsNameNext)
				Fail ("a swift name");
			SwiftName addressorName = slice.ExtractSwiftName ();
			SwiftType ofType = ReadType (true); // addressors return the address of the entity
			return new SwiftAddressorType (theType, ofType, isReference, addressorName);
		}

		SwiftPropertyType ReadGetter (SwiftClassType cl, bool isStatic, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('g'))
				Fail ("a property getter (g)");
			return ReadProperty (cl, PropertyType.Getter, isStatic, isReference);
		}

		SwiftPropertyType ReadSetter (SwiftClassType cl, bool isStatic, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('s'))
				Fail ("a property setter");
			return ReadProperty (cl, PropertyType.Setter, isStatic, isReference);
		}

		SwiftPropertyType ReadMaterializer (SwiftClassType cl, bool isStatic, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('m'))
				Fail ("a property materializer");
			return ReadProperty (cl, PropertyType.Materializer, isStatic, isReference);
		}

		SwiftPropertyType ReadProperty (SwiftClassType cl, PropertyType propType, bool isStatic, bool isReference)
		{
			SwiftName privateName = null;
			MarkOperatorPosition ();
			if (slice.AdvanceIfEquals ('P'))
				privateName = slice.ExtractSwiftName ();
			MarkOperatorPosition ();
			if (!slice.IsNameNext)
				Fail ("a swift name");
			SwiftName propName = slice.ExtractSwiftName ();

			if (IsSubscript (propName)) {
				SwiftType subscriptType = ReadType (false);
				SwiftBaseFunctionType subscriptFunc = subscriptType as SwiftBaseFunctionType;
				if (subscriptFunc == null)
					Fail ("a function type in an indexed property", subscriptType.GetType ().Name);

				if (propType == PropertyType.Setter && subscriptFunc.ReturnType != null && !subscriptFunc.ReturnType.IsEmptyTuple) {
					// oh hooray!
					// If I define an indexer in swift like this:
					// public subscript(T index) -> U {
					//    get { return getSomeUValue(index); }
					//    set (someUValue) { setSomeUValue(index, someUValue); }
					// }
					// This signature of the function attached to both properties is:
					// T -> U
					// which makes bizarre sense - the subscript() declaration is T -> U and the getter is T -> U, but
					// the setter is (T, U) -> void
					//
					// Since we have actual code that depends upon the signature, we need to "fix" this signature to reflect
					// what's really happening.

					// need to change this so that the tail parameters get names? Maybe just the head?

					SwiftTupleType newParameters = subscriptFunc.ParameterCount == 1 ?
						new SwiftTupleType (false, null, subscriptFunc.ReturnType,
							subscriptFunc.Parameters.RenamedCloneOf (new SwiftName (subscriptFunc.ReturnType.Name == null ||
								subscriptFunc.ReturnType.Name.Name != "a" ? "a" : "b", false)))
						: new SwiftTupleType (Enumerable.Concat (subscriptFunc.ReturnType.Yield (), subscriptFunc.EachParameter),
									       false, null);
					subscriptFunc = new SwiftFunctionType (newParameters, SwiftTupleType.Empty, false, subscriptFunc.CanThrow, subscriptFunc.Name);
				}
				return new SwiftPropertyType (cl, propType, propName, privateName, (SwiftFunctionType)subscriptFunc, isStatic, isReference);

			} else {
				SwiftType ofType = ReadType (false);

				return new SwiftPropertyType (cl, propType, propName, privateName, ofType, isStatic, isReference);
			}
		}

		public static bool IsSubscript (SwiftName name)
		{
			return name.Name == "subscript";
		}

		SwiftConstructorType ReadAllocatorType (bool isReference)
		{
			return ReadAllocOrCons (true, 'C', "an allocating constructor (C)", isReference);
		}

		SwiftConstructorType ReadConstructorType (bool isReference)
		{
			return ReadAllocOrCons (false, 'c', "a non-allocating constructor (c)", isReference);
		}

		SwiftType ReadProtocolList (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('P'))
				Fail ("A protocol list marker ('P')");
			if (slice.Current == 'M') {
				return ReadExistentialMetatype (name, isReference);
			}
			List<SwiftClassType> protocols = new List<SwiftClassType> ();
			while (slice.Current != '_') {
				SwiftClassName className = ReadProtocolName ();
				SwiftClassType theProtocol = new SwiftClassType (className, false, null);
				subs.Add (theProtocol);
				protocols.Add (theProtocol);
			}
			slice.Advance ();
			return new SwiftProtocolListType (protocols, isReference, name);
		}

		SwiftClassType ReadClassOrStructOrEnum (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			SwiftClassName className = ReadClassStructEnumNameAlt ();
			SwiftClassType clType = new SwiftClassType (className, isReference, name);
			subs.Add (clType);
			return clType;
		}

		SwiftMetaClassType ReadMetaClassType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('M'))
				Fail ("a metaclass marker (M)");
			SwiftClassType cl = ReadType (false) as SwiftClassType;
			if (cl == null)
				Fail ("a class type");
			return new SwiftMetaClassType (cl, isReference, name);
		}

		SwiftExistentialMetaType ReadExistentialMetatype (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('M'))
				Fail ("a metaclass marker (M)");
			SwiftProtocolListType pl = ReadType (false) as SwiftProtocolListType;
			if (pl == null)
				Fail ("a protocol list type");
			return new SwiftExistentialMetaType (pl, isReference, name);
		}


		SwiftType ReadUnboundGenericType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('u'))
				Fail ("an unbound generic type (u)");
			MarkOperatorPosition ();
			List<GenericArgument> parms = ReadGenericArguments ();
			SwiftType dependentType = ReadType (isReference, name);
			SwiftBaseFunctionType bft = dependentType as SwiftBaseFunctionType;
			if (bft != null) {
				bft.GenericArguments.AddRange (parms);
				return bft;
			}
			return new SwiftUnboundGenericType (dependentType, parms, isReference, dependentType.Name);
		}


		List<GenericArgument> ReadGenericArguments ()
		{
			int count = 0;
			while (slice.Current != 'R' && slice.Current != 'r') {
				count = DemangleIndex ();
			}
			count += 1;
			List<GenericArgument> args = new List<GenericArgument> (count);
			for (int i = 0; i < count; i++) {
				args.Add (new GenericArgument (0, i));
			}
			if (slice.AdvanceIfEquals ('r'))
				return args;
			if (!slice.AdvanceIfEquals ('R'))
				Fail ("A generic argument requirements marker (R)");
			while (!slice.AdvanceIfEquals ('r')) {
				int depth = 0;
				int index = 0;
				if (slice.AdvanceIfEquals ('d')) {
					depth = DemangleIndex ();
					index = DemangleIndex ();
				} else if (slice.AdvanceIfEquals ('x')) {
					// no change
				} else {
					index = DemangleIndex () + 1;
				}
				if (slice.StartsWith ('C')) {
					SwiftType constraintType = ReadType (false, null);
					args [index].Constraints.Add (constraintType);
				} else {
					SwiftClassName className = ReadProtocolName ();
					SwiftClassType theProtocol = new SwiftClassType (className, false, null);
					args [index].Constraints.Add (theProtocol);
				}
				args [index].Depth = depth;
			}
			// currently swift name mangling is done incorrectly to get the actual
			// depth. In the case where there is a 'where' clause, we can figure it out
			// and correct it.
			int maxDepth = 0;
			foreach (GenericArgument gen in args) {
				maxDepth = Math.Max (maxDepth, gen.Depth);
			}
			foreach (GenericArgument gen in args) {
				gen.Depth = maxDepth;
			}
			return args;
		}

		SwiftGenericArgReferenceType ReadGenericReferenceType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('q'))
				Fail ("a generic argument reference");
			MarkOperatorPosition ();
			int depth = 0;
			int index = 0;
			if (slice.AdvanceIfEquals ('d')) {
				depth = DemangleIndex () + 1;
				index = DemangleIndex () + 1;
			} else {
				index = DemangleIndex () + 1;
			}
			return new SwiftGenericArgReferenceType (depth, index, isReference, name);
		}

		SwiftBoundGenericType ReadBoundGenericType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('G'))
				Fail ("a bound generic type (G)");
			MarkOperatorPosition ();
			SwiftType baseType = ReadType (false);

			List<SwiftType> generics = new List<SwiftType> ();
			while (!slice.AdvanceIfEquals ('_')) {
				MarkOperatorPosition ();
				SwiftType t = ReadType (false);
				if (t != null)
					generics.Add (t);
			}
			return new SwiftBoundGenericType (baseType, generics, isReference, name);
		}

		SwiftArrayType ReadArrayType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('S') || !slice.AdvanceIfEquals ('a'))
				Fail ("an Array type (Sa)");
			return new SwiftArrayType (isReference, name);
		}

		SwiftFunctionType ReadFunctionType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('F'))
				Fail ("a function marker (F)");
			bool canThrow = slice.AdvanceIfEquals ('z');
			SwiftType parms = ReadType (false);
			SwiftType ret = ReadType (false);
			return new SwiftFunctionType (parms, ret, isReference, canThrow, name);
		}

		SwiftCFunctionType ReadCFunctionType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('c'))
				Fail ("a C function marker (c)");
			SwiftType parms = ReadType (false);
			SwiftType ret = ReadType (false);
			return new SwiftCFunctionType (parms, ret, isReference, name);
		}

		SwiftType ReadScalarType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('S'))
				Fail ("a core built in type marker (S)");
			if (slice.Current == 'S') {
				slice.Advance ();
				return ClassForBuiltInType (name, "String", MemberNesting.Struct, isReference);
			} else if (slice.Current == 'q') {
				slice.Advance ();
				return ClassForBuiltInType (name, "Optional", MemberNesting.Enum, isReference);
			} else if (slice.Current == 'Q') {
				slice.Advance ();
				return ClassForBuiltInType (name, "ImplicitlyUnwrappedOptional", MemberNesting.Struct, isReference);
			} else if (slice.Current == 'P') {
				slice.Advance ();
				return ClassForBuiltInType (name, "UnsafePointer", MemberNesting.Struct, isReference);
			} else if (slice.Current == 'p') {
				slice.Advance ();
				return ClassForBuiltInType (name, "UnsafeMutablePointer", MemberNesting.Struct, isReference);
			} else if (slice.Current == 'R') {
				slice.Advance ();
				return ClassForBuiltInType (name, "UnsafeBufferPointer", MemberNesting.Struct, isReference);
			} else if (slice.Current == 'r') {
				slice.Advance ();
				return ClassForBuiltInType (name, "UnsafeMutableBufferPointer", MemberNesting.Struct, isReference);
			} else {
				CoreBuiltInType scalarType = ToCoreScalar (slice.Advance ());
				return new SwiftBuiltInType (scalarType, isReference, name);
			}
		}

		SwiftTupleType ReadTupleType (SwiftName name, bool isReference)
		{
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('T'))
				Fail ("a tuple marker (T)");

			List<SwiftType> contents = new List<SwiftType> ();
			while (!slice.AdvanceIfEquals ('_')) {
				MarkOperatorPosition ();
				SwiftType ty = ReadType (false);
				contents.Add (ty);
			}
			return new SwiftTupleType (contents, isReference, name);
		}

		static SwiftClassType ClassForBuiltInType (SwiftName name, string className, MemberNesting nesting, bool isReference)
		{
			return new SwiftClassType (new SwiftClassName (new SwiftName ("Swift", false),
				new List<MemberNesting> { nesting },
				new List<SwiftName> { new SwiftName (className, false) }), isReference, name);
		}

		SwiftType DemangleSubstitution (SwiftName name, bool isReference)
		{
			if (!slice.AdvanceIfEquals ('S'))
				Fail ("a core built in type marker (S)");
			if (slice.AdvanceIfEquals ('a')) {
				return ClassForBuiltInType (name, "Array", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('c')) {
				return ClassForBuiltInType (name, "UnicodeScalar", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('S')) {
				return ClassForBuiltInType (name, "String", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('q')) {
				return ClassForBuiltInType (name, "Optional", MemberNesting.Enum, isReference);
			}
			if (slice.AdvanceIfEquals ('Q')) {
				return ClassForBuiltInType (name, "ImplicitlyUnwrappedOptional", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('P')) {
				return ClassForBuiltInType (name, "UnsafePointer", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('p')) {
				return ClassForBuiltInType (name, "UnsafeMutablePointer", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('R')) {
				return ClassForBuiltInType (name, "UnsafeBufferPointer", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('r')) {
				return ClassForBuiltInType (name, "UnsafeMutableBufferPointer", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('V')) {
				return ClassForBuiltInType (name, "UnsafeRawPointer", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('v')) {
				return ClassForBuiltInType (name, "UnsafeMutableRawPointer", MemberNesting.Struct, isReference);
			}
			if (slice.AdvanceIfEquals ('b')) {
				return new SwiftBuiltInType (CoreBuiltInType.Bool, isReference, name);
			}
			if (slice.AdvanceIfEquals ('d')) {
				return new SwiftBuiltInType (CoreBuiltInType.Double, isReference, name);
			}
			if (slice.AdvanceIfEquals ('f')) {
				return new SwiftBuiltInType (CoreBuiltInType.Float, isReference, name);
			}
			if (slice.AdvanceIfEquals ('i')) {
				return new SwiftBuiltInType (CoreBuiltInType.Int, isReference, name);
			}
			if (slice.AdvanceIfEquals ('u')) {
				return new SwiftBuiltInType (CoreBuiltInType.UInt, isReference, name);
			}
			if (slice.AdvanceIfEquals ('s')) {
				return new SwiftModuleNameType (new SwiftName ("Swift", false), isReference);
			}
			int index = DemangleIndex ();
			SwiftType st = subs [index];
			SwiftClassType sct = st as SwiftClassType;
			if (sct == null) {
				SwiftModuleNameType mod = st as SwiftModuleNameType;
				if (mod == null)
					Fail ("a SwiftModuleNameType", st.GetType ().Name);
				return mod;
			}
			return new SwiftClassType (sct.ClassName, isReference, name);
		}

		int DemangleIndex ()
		{
			if (slice.AdvanceIfEquals ('_')) {
				return 0;
			}
			MarkOperatorPosition ();
			if (!Char.IsDigit (slice.Current))
				Fail ("a number", slice.Current.ToString ());
			int index = ReadNumber ();
			MarkOperatorPosition ();
			if (!slice.AdvanceIfEquals ('_'))
				Fail ("a macro terminator '-'", slice.Current.ToString ());
			return index + 1;
		}

		int ReadNumber ()
		{
			int number = 0;
			while (Char.IsDigit (slice.Current)) {
				number = 10 * number + slice.Current - '0';
				slice.Advance ();
			}
			return number;
		}
	}
}

