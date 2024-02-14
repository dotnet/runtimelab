// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using static SwiftInterfaceParser;
using System.Text;
using SyntaxDynamo;
using SwiftReflector.TypeMapping;
using SwiftReflector.SwiftXmlReflection;
using System.Threading.Tasks;
using System.Threading;

namespace SwiftReflector.SwiftInterfaceReflector {
	public class SwiftInterfaceReflector : SwiftInterfaceBaseListener {
		// swift-interface-format-version: 1.0
		const string kSwiftInterfaceFormatVersion = "// swift-interface-format-version:";
		// swift-compiler-version: Apple Swift version 5.3 (swiftlang-1200.0.29.2 clang-1200.0.30.1)
		const string kSwiftCompilerVersion = "// swift-compiler-version: ";
		// swift-module-flags: -target x86_64-apple-macosx10.9 -enable-objc-interop -ena
		const string kSwiftModuleFlags = "// swift-module-flags:";

		internal const string kModuleName = "module-name";
		internal const string kTarget = "target";
		internal const string kIgnore = "IGNORE";
		internal const string kInheritanceKind = "inheritanceKind";
		internal const string kModule = "module";
		internal const string kFunc = "func";
		internal const string kType = "type";
		internal const string kName = "name";
		internal const string kFinal = "final";
		internal const string kPublic = "public";
		internal const string kPrivate = "private";
		internal const string kInternal = "internal";
		internal const string kOpen = "open";
		internal const string kPublicCap = "Public";
		internal const string kPrivateCap = "Private";
		internal const string kInternalCap = "Internal";
		internal const string kOpenCap = "Open";
		internal const string kFilePrivate = "fileprivate";
		internal const string kStatic = "static";
		internal const string kIsStatic = "isStatic";
		internal const string kOptional = "optional";
		internal const string kObjC = "objc";
		internal const string kExtension = "extension";
		internal const string kProtocol = "protocol";
		internal const string kClass = "class";
		internal const string kInnerClasses = "innerclasses";
		internal const string kStruct = "struct";
		internal const string kInnerStructs = "innerstructs";
		internal const string kEnum = "enum";
		internal const string kInnerEnums = "innerenums";
		internal const string kMutating = "mutating";
		internal const string kRequired = "required";
		internal const string kAssociatedTypes = "associatedtypes";
		internal const string kAssociatedType = "associatedtype";
		internal const string kDefaultType = "defaulttype";
		internal const string kConformingProtocols = "conformingprotocols";
		internal const string kConformingProtocol = "conformingprotocol";
		internal const string kMembers = "members";
		internal const string kConvenience = "convenience";
		internal const string kParameterLists = "parameterlists";
		internal const string kParameterList = "parameterlist";
		internal const string kParameter = "parameter";
		internal const string kParam = "param";
		internal const string kGenericParameters = "genericparameters";
		internal const string kWhere = "where";
		internal const string kRelationship = "relationship";
		internal const string kEquals = "equals";
		internal const string kInherits = "inherits";
		internal const string kInherit = "inherit";
		internal const string kIndex = "index";
		internal const string kGetSubscript = "get_subscript";
		internal const string kSetSubscript = "set_subscript";
		internal const string kOperator = "operator";
		internal const string kLittlePrefix = "prefix";
		internal const string kLittlePostfix = "postfix";
		internal const string kPrefix = "Prefix";
		internal const string kPostfix = "Postfix";
		internal const string kInfix = "Infix";
		internal const string kDotCtor = ".ctor";
		internal const string kDotDtor = ".dtor";
		internal const string kNewValue = "newValue";
		internal const string kOperatorKind = "operatorKind";
		internal const string kPublicName = "publicName";
		internal const string kPrivateName = "privateName";
		internal const string kKind = "kind";
		internal const string kNone = "None";
		internal const string kLittleUnknown = "unknown";
		internal const string kUnknown = "Unknown";
		internal const string kOnType = "onType";
		internal const string kAccessibility = "accessibility";
		internal const string kIsVariadic = "isVariadic";
		internal const string kTypeDeclaration = "typedeclaration";
		internal const string kIsAsync = "isAsync";
		internal const string kProperty = "property";
		internal const string kIsProperty = "isProperty";
		internal const string kStorage = "storage";
		internal const string kComputed = "Computed";
		internal const string kEscaping = "escaping";
		internal const string kAutoClosure = "autoclosure";
		internal const string kAttributes = "attributes";
		internal const string kAttribute = "attribute";
		internal const string kAttributeParameterList = "attributeparameterlist";
		internal const string kAttributeParameter = "attributeparameter";
		internal const string kLabel = "Label";
		internal const string kLiteral = "Literal";
		internal const string kSeparator = "Separator";
		internal const string kSublist = "Sublist";
		internal const string kValue = "Value";
		internal const string kObjCSelector = "objcSelector";
		internal const string kDeprecated = "deprecated";
		internal const string kUnavailable = "unavailable";
		internal const string kAvailable = "available";
		internal const string kIntroduced = "introduced";
		internal const string kObsoleted = "obsoleted";
		internal const string kElements = "elements";
		internal const string kElement = "element";
		internal const string kIntValue = "intValue";
		internal const string kRawType = "rawType";
		internal const string kRawValue = "RawValue";
		internal const string kTypeAliases = "typealiases";
		internal const string kTypeAlias = "typealias";
		internal const string kSuperclass = "superclass";

		Stack<XElement> currentElement = new Stack<XElement> ();
		Version interfaceVersion;
		Version compilerVersion;

		List<string> importModules = new List<string> ();
		List<XElement> operators = new List<XElement> ();
		List<Tuple<Function_declarationContext, XElement>> functions = new List<Tuple<Function_declarationContext, XElement>> ();
		List<XElement> extensions = new List<XElement> ();
		Dictionary<string, string> moduleFlags = new Dictionary<string, string> ();
		List<string> nominalTypes = new List<string> ();
		List<string> classes = new List<string> ();
		List<XElement> associatedTypesWithConformance = new List<XElement> ();
		List<XElement> unknownInheritance = new List<XElement> ();
		List<XElement> typeAliasMap = new List<XElement> ();
		string moduleName;
		TypeDatabase typeDatabase;
		IModuleLoader moduleLoader;
		ICharStream inputStream;

		public SwiftInterfaceReflector (TypeDatabase typeDatabase, IModuleLoader moduleLoader)
		{
			this.typeDatabase = typeDatabase;
			this.moduleLoader = moduleLoader;
		}

		public async Task ReflectAsync (string inFile, Stream outStm)
		{
			Exceptions.ThrowOnNull (inFile, nameof (inFile));
			Exceptions.ThrowOnNull (outStm, nameof (outStm));


			await Task.Run (() => {
				var xDocument = Reflect (inFile);
				xDocument.Save (outStm);
				currentElement.Clear ();
			});
		}

		public void Reflect (string inFile, Stream outStm)
		{
			Exceptions.ThrowOnNull (inFile, nameof (inFile));
			Exceptions.ThrowOnNull (outStm, nameof (outStm));

			var xDocument = Reflect (inFile);

			xDocument.Save (outStm);
			currentElement.Clear ();
		}

		public async Task<XDocument> ReflectAsync (string inFile)
		{
			return await Task.Run (() => {
				return Reflect (inFile);
			});
		}

		public XDocument Reflect (string inFile)
		{
			// try {
				Exceptions.ThrowOnNull (inFile, nameof (inFile));

				if (!File.Exists (inFile))
					throw new ParseException ($"Input file {inFile} not found");


				var fileName = Path.GetFileName (inFile);
				moduleName = fileName.Split ('.') [0];

				var module = new XElement (kModule);
				currentElement.Push (module);

				var desugarer = new SyntaxDesugaringParser (inFile);
				var desugaredResult = desugarer.Desugar ();
				inputStream = CharStreams.fromString (desugaredResult);

				var lexer = new SwiftInterfaceLexer (inputStream);
				var tokenStream = new CommonTokenStream (lexer);
				var parser = new SwiftInterfaceParser (tokenStream);
				var walker = new ParseTreeWalker ();
				walker.Walk (this, parser.swiftinterface ());

				if (currentElement.Count != 1)
					throw new ParseException ("At end of parse, stack should contain precisely one element");

				if (module != currentElement.Peek ())
					throw new ParseException ("Expected the final element to be the initial module");

				// LoadReferencedModules ();

				// PatchPossibleOperators ();
				// PatchExtensionShortNames ();
				// PatchExtensionSelfArgs ();
				// PatchPossibleBadInheritance ();
				// PatchAssociatedTypeConformance ();

				if (typeAliasMap.Count > 0) {
					module.Add (new XElement (kTypeAliases, typeAliasMap.ToArray ()));
				}

				module.Add (new XAttribute (kName, moduleName));
				SetLanguageVersion (module);

				var tlElement = new XElement ("xamreflect", new XAttribute ("version", "1.0"),
					new XElement ("modulelist", module));
				var xDocument = new XDocument (new XDeclaration ("1.0", "utf-8", "yes"), tlElement);
				return xDocument;
			// } catch (ParseException parseException) {
			// 	throw;
			// } catch (Exception e) {
			// 	throw new ParseException ($"Unknown error parsing {inFile}: {e.Message}", e.InnerException);
			// }
		}

		public override void EnterComment ([NotNull] CommentContext context)
		{
			var commentText = context.GetText ();
			InterpretCommentText (commentText);
		}

		public override void EnterClass_declaration ([NotNull] Class_declarationContext context)
		{
			var inheritance = GatherInheritance (context.type_inheritance_clause (), forceProtocolInheritance: false);
			var attributes = GatherAttributes (context.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isObjC = AttributesContains (context.attributes (), kObjC);
			var accessibility = ToAccess (context.access_level_modifier ());
			var isFinal = context.final_clause () != null || accessibility != kOpenCap;
			var typeDecl = ToTypeDeclaration (kClass, UnTick (context.class_name ().GetText ()),
				accessibility, isObjC, isFinal, isDeprecated, isUnavailable, inheritance, generics: null,
				attributes);
			var generics = HandleGenerics (context.generic_parameter_clause (), context.generic_where_clause (), false);
			if (generics != null)
				typeDecl.Add (generics);
			currentElement.Push (typeDecl);
		}

		public override void ExitClass_declaration ([NotNull] Class_declarationContext context)
		{
			var classElem = currentElement.Pop ();
			var givenClassName = classElem.Attribute (kName).Value;
			var actualClassName = UnTick (context.class_name ().GetText ());
			if (givenClassName != actualClassName)
				throw new ParseException ($"class name mismatch on exit declaration: expected {actualClassName} but got {givenClassName}");
			AddClassToCurrentElement (classElem);
		}

		public override void EnterStruct_declaration ([NotNull] Struct_declarationContext context)
		{
			var inheritance = GatherInheritance (context.type_inheritance_clause (), forceProtocolInheritance: true);
			var attributes = GatherAttributes (context.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isFinal = true; // structs are always final
			var isObjC = AttributesContains (context.attributes (), kObjC);
			var accessibility = ToAccess (context.access_level_modifier ());
			var typeDecl = ToTypeDeclaration (kStruct, UnTick (context.struct_name ().GetText ()),
				accessibility, isObjC, isFinal, isDeprecated, isUnavailable, inheritance, generics: null,
				attributes);
			var generics = HandleGenerics (context.generic_parameter_clause (), context.generic_where_clause (), false);
			if (generics != null)
				typeDecl.Add (generics);
			currentElement.Push (typeDecl);
		}

		public override void ExitStruct_declaration ([NotNull] Struct_declarationContext context)
		{
			var structElem = currentElement.Pop ();
			var givenStructName = structElem.Attribute (kName).Value;
			var actualStructName = UnTick (context.struct_name ().GetText ());
			if (givenStructName != actualStructName)
				throw new ParseException ($"struct name mismatch on exit declaration: expected {actualStructName} but got {givenStructName}");
			AddStructToCurrentElement (structElem);
		}

		public override void EnterEnum_declaration ([NotNull] Enum_declarationContext context)
		{
			var inheritanceClause = context.union_style_enum ()?.type_inheritance_clause () ??
				context.raw_value_style_enum ()?.type_inheritance_clause ();
			var inheritance = GatherInheritance (inheritanceClause, forceProtocolInheritance: true, removeNonProtocols: true);
			var attributes = GatherAttributes (context.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isFinal = true; // enums are always final
			var isObjC = AttributesContains (context.attributes (), kObjC);
			var accessibility = ToAccess (context.access_level_modifier ());
			var typeDecl = ToTypeDeclaration (kEnum, EnumName (context),
				accessibility, isObjC, isFinal, isDeprecated, isUnavailable, inheritance, generics: null,
				attributes);
			var generics = HandleGenerics (EnumGenericParameters (context), EnumGenericWhere (context), false);
			if (generics != null)
				typeDecl.Add (generics);
			currentElement.Push (typeDecl);
		}

		public override void ExitEnum_declaration ([NotNull] Enum_declarationContext context)
		{
			var enumElem = currentElement.Pop ();
			var givenEnumName = enumElem.Attribute (kName).Value;
			var actualEnumName = EnumName (context);
			if (givenEnumName != actualEnumName)
				throw new ParseException ($"enum name mismatch on exit declaration: expected {actualEnumName} but got {givenEnumName}");

			var rawType = GetEnumRawType (context);
			if (rawType != null)
				enumElem.Add (rawType);

			AddEnumToCurrentElement (enumElem);
		}

		static string EnumName (Enum_declarationContext context)
		{
			return UnTick (context.union_style_enum () != null ?
				context.union_style_enum ().enum_name ().GetText () :
				context.raw_value_style_enum ().enum_name ().GetText ());
		}

		XAttribute GetEnumRawType (Enum_declarationContext context)
		{
			var alias = EnumTypeAliases (context).FirstOrDefault (ta => ta.typealias_name ().GetText () == kRawValue);
			if (alias == null)
				return null;
			var rawType = TypeText (alias.typealias_assignment ().type ());
			return new XAttribute (kRawType, rawType);
		}

		IEnumerable<Typealias_declarationContext> EnumTypeAliases (Enum_declarationContext context)
		{
			if (context.union_style_enum () != null)
				return UnionTypeAliases (context.union_style_enum ());
			else
				return RawTypeAliases (context.raw_value_style_enum ());
		}

		IEnumerable<Typealias_declarationContext> UnionTypeAliases (Union_style_enumContext context)
		{
			var members = context.union_style_enum_members ();
			while (members != null) {
				if (members.union_style_enum_member () != null) {
					var member = members.union_style_enum_member ();
					if (member.nominal_declaration ()?.typealias_declaration () != null)
						yield return member.nominal_declaration ().typealias_declaration ();
				}
				members = members.union_style_enum_members ();
			}
			yield break;
		}

		IEnumerable<Typealias_declarationContext> RawTypeAliases (Raw_value_style_enumContext context)
		{
			var members = context.raw_value_style_enum_members ();
			while (members != null) {
				if (members.raw_value_style_enum_member () != null) {
					var member = members.raw_value_style_enum_member ();
					if (member.nominal_declaration ()?.typealias_declaration () != null)
						yield return member.nominal_declaration ().typealias_declaration ();
				}
				members = members.raw_value_style_enum_members ();
			}
			yield break;
		}

		public override void EnterRaw_value_style_enum_case_clause ([NotNull] Raw_value_style_enum_case_clauseContext context)
		{
			var enumElements = new XElement (kElements);
			foreach (var enumCase in RawCases (context.raw_value_style_enum_case_list ())) {
				var enumElement = ToRawEnumElement (enumCase);
				enumElements.Add (enumElement);
			}
			AddEnumNonEmptyElements (enumElements);
		}

		public override void EnterUnion_style_enum_case_clause ([NotNull] Union_style_enum_case_clauseContext context)
		{
			var enumElements = new XElement (kElements);
			foreach (var enumCase in UnionCases (context.union_style_enum_case_list ())) {
				var enumElement = ToUnionEnumElement (enumCase);
				enumElements.Add (enumElement);
			}
			AddEnumNonEmptyElements (enumElements);
		}

		void AddEnumNonEmptyElements (XElement enumElements)
		{
			if (enumElements.HasElements) {
				var currentEnum = currentElement.Peek ();
				if (currentEnum.Attribute (kKind)?.Value != kEnum)
					throw new ParseException ("Current element needs to be an enum");

				var existingElements = currentEnum.Element (kElements);
				if (existingElements != null) {
					foreach (var elem in enumElements.Elements ()) {
						existingElements.Add (elem);
					}
				} else {
					currentEnum.Add (enumElements);
				}
			}
		}

		IEnumerable<Raw_value_style_enum_caseContext> RawCases (Raw_value_style_enum_case_listContext context)
		{
			while (context != null) {
				if (context.raw_value_style_enum_case () != null) {
					yield return context.raw_value_style_enum_case ();
				}
				context = context.raw_value_style_enum_case_list ();
			}
			yield break;
		}

		XElement ToRawEnumElement (Raw_value_style_enum_caseContext context)
		{
			var enumElem = new XElement (kElement, new XAttribute (kName, UnTick (context.enum_case_name ().GetText ())));
			var value = context.raw_value_assignment ();
			if (value != null)
				enumElem.Add (new XAttribute (kIntValue, value.raw_value_literal ().GetText ()));
			return enumElem;
		}

		IEnumerable<Union_style_enum_caseContext> UnionCases (Union_style_enum_case_listContext context)
		{
			while (context != null) {
				if (context.union_style_enum_case () != null) {
					yield return context.union_style_enum_case ();
				}
				context = context.union_style_enum_case_list ();
			}
			yield break;
		}

		XElement ToUnionEnumElement (Union_style_enum_caseContext context)
		{
			var enumElement = new XElement (kElement, new XAttribute (kName, UnTick (context.enum_case_name ().GetText ())));
			if (context.tuple_type () != null) {
				var tupString = TypeText (context.tuple_type ());
				// special casing:
				// the type of a union case is a tuple, but we special case
				// unit tuples to be just the type of the unit
				// which may be something like ((((((()))))))
				// in which case we want to let it come through as is.
				// a unit tuple may also have a type label which we don't care
				// about so make that go away too.

				if (tupString.IndexOf (',') < 0 && tupString.IndexOf (':') < 0) {
					var pastLastOpen = tupString.LastIndexOf ('(') + 1;
					var firstClosed = tupString.IndexOf (')');
					if (pastLastOpen != firstClosed) {
						tupString = tupString.Substring (pastLastOpen, firstClosed - pastLastOpen);
						var colonIndex = tupString.IndexOf (':');
						if (colonIndex >= 0) {
							tupString = tupString.Substring (colonIndex + 1);
						}
					}
				}
				enumElement.Add (new XAttribute (kType, tupString));
			}
			return enumElement;
		}

		public override void EnterProtocol_declaration ([NotNull] Protocol_declarationContext context)
		{
			var inheritance = GatherInheritance (context.type_inheritance_clause (), forceProtocolInheritance: true);
			var attributes = GatherAttributes (context.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isFinal = true; // protocols don't have final
			var isObjC = AttributesContains (context.attributes (), kObjC);
			var accessibility = ToAccess (context.access_level_modifier ());
			var typeDecl = ToTypeDeclaration (kProtocol, UnTick (context.protocol_name ().GetText ()),
				accessibility, isObjC, isFinal, isDeprecated, isUnavailable, inheritance, generics: null,
				attributes);
			currentElement.Push (typeDecl);
		}

		public override void ExitProtocol_declaration ([NotNull] Protocol_declarationContext context)
		{
			var protocolElem = currentElement.Pop ();
			var givenProtocolName = protocolElem.Attribute (kName).Value;
			var actualProtocolName = UnTick (context.protocol_name ().GetText ());
			if (givenProtocolName != actualProtocolName)
				throw new ParseException ($"protocol name mismatch on exit declaration: expected {actualProtocolName} but got {givenProtocolName}");
			if (currentElement.Peek ().Name != kModule)
				throw new ParseException ($"Expected a module on the element stack but found {currentElement.Peek ()}");
			currentElement.Peek ().Add (protocolElem);
		}

		public override void EnterProtocol_associated_type_declaration ([NotNull] Protocol_associated_type_declarationContext context)
		{
			var conformingProtocols = GatherConformingProtocols (context.type_inheritance_clause ());
			var defaultDefn = TypeText (context.typealias_assignment ()?.type ());
			var assocType = new XElement (kAssociatedType,
				new XAttribute (kName, UnTick (context.typealias_name ().GetText ())));
			if (defaultDefn != null)
				assocType.Add (new XAttribute (kDefaultType, UnTick (defaultDefn)));
			if (conformingProtocols != null && conformingProtocols.Count > 0) {
				var confomingElem = new XElement (kConformingProtocols, conformingProtocols.ToArray ());
				assocType.Add (confomingElem);
				associatedTypesWithConformance.Add (assocType);
			}
			AddAssociatedTypeToCurrentElement (assocType);
		}

		List<XElement> GatherConformingProtocols (Type_inheritance_clauseContext context)
		{
			if (context == null)
				return null;
			var elems = new List<XElement> ();
			if (context.class_requirement () != null) {
				// not sure what to do here
				// this is just the keyword 'class'
			}
			var inheritance = context.type_inheritance_list ();
			while (inheritance != null) {
				var elem = inheritance.GetText ();
				var name = TypeText (inheritance.type_identifier ());
				if (name != null)
					elems.Add (new XElement (kConformingProtocol, new XAttribute (kName, UnTick (name))));
				inheritance = inheritance.type_inheritance_list ();
			}
			return elems;
		}

		static Generic_parameter_clauseContext EnumGenericParameters (Enum_declarationContext context)
		{
			return context.union_style_enum ()?.generic_parameter_clause () ??
				context.raw_value_style_enum ()?.generic_parameter_clause ();
		}

		static Generic_where_clauseContext EnumGenericWhere (Enum_declarationContext context)
		{
			return context.union_style_enum ()?.generic_where_clause () ??
				context.raw_value_style_enum ()?.generic_where_clause ();
		}

		public override void EnterFunction_declaration ([NotNull] Function_declarationContext context)
		{
			var head = context.function_head ();
			var signature = context.function_signature ();

			var name = UnTick (context.function_name ().GetText ());
			var returnType = signature.function_result () != null ? TypeText (signature.function_result ().type ()) : "()";
			var accessibility = AccessibilityFromModifiers (head.declaration_modifiers ());
			var isStatic = IsStaticOrClass (head.declaration_modifiers ());
			var hasThrows = signature.throws_clause () != null || signature.rethrows_clause () != null;
			var isFinal = ModifiersContains (head.declaration_modifiers (), kFinal);
			var isOptional = ModifiersContains (head.declaration_modifiers (), kOptional);
			var isConvenienceInit = false;
			var operatorKind = kNone;
			var isMutating = ModifiersContains (head.declaration_modifiers (), kMutating);
			var isRequired = ModifiersContains (head.declaration_modifiers (), kRequired);
			var isProperty = false;
			var attributes = GatherAttributes (head.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isAsync = signature.async_clause () != null;
			var functionDecl = ToFunctionDeclaration (name, returnType, accessibility, isStatic, hasThrows,
				isFinal, isOptional, isConvenienceInit, objCSelector: null, operatorKind,
				isDeprecated, isUnavailable, isMutating, isRequired, isProperty, isAsync, attributes);
			var generics = HandleGenerics (context.generic_parameter_clause (), context.generic_where_clause (), true);
			if (generics != null)
				functionDecl.Add (generics);


			functions.Add (new Tuple<Function_declarationContext, XElement> (context, functionDecl));
			currentElement.Push (functionDecl);
		}

		public override void ExitFunction_declaration ([NotNull] Function_declarationContext context)
		{
			ExitFunctionWithName (UnTick (context.function_name ().GetText ()));
		}

		void ExitFunctionWithName (string expectedName)
		{
			var functionDecl = currentElement.Pop ();
			if (functionDecl.Name != kFunc)
				throw new ParseException ($"Expected a func node but got a {functionDecl.Name}");
			var givenName = functionDecl.Attribute (kName);
			if (givenName == null)
				throw new ParseException ("func node doesn't have a name element");
			if (givenName.Value != expectedName)
				throw new ParseException ($"Expected a func node with name {expectedName} but got {givenName.Value}");

			AddObjCSelector (functionDecl);

			AddElementToParentMembers (functionDecl);
		}

		void AddObjCSelector (XElement functionDecl)
		{
			var selectorFactory = new ObjCSelectorFactory (functionDecl);
			var selector = selectorFactory.Generate ();
			if (!String.IsNullOrEmpty (selector)) {
				functionDecl.Add (new XAttribute (kObjCSelector, selector));
			}
		}

		XElement PeekAsFunction ()
		{
			var functionDecl = currentElement.Peek ();
			if (functionDecl.Name != kFunc)
				throw new ParseException ($"Expected a func node but got a {functionDecl.Name}");
			return functionDecl;
		}

		void AddElementToParentMembers (XElement elem)
		{
			var parent = currentElement.Peek ();
			if (parent.Name == kModule) {
				parent.Add (elem);
				return;
			}
			var memberElem = GetOrCreate (parent, kMembers);
			memberElem.Add (elem);
		}

		bool IsInInstance ()
		{
			var parent = currentElement.Peek ();
			return parent.Name != kModule;
		}

		bool HasObjCElement (XElement elem)
		{
			var objcAttr = elem.Descendants ()
				.FirstOrDefault (el => el.Name == kAttribute && el.Attribute ("name")?.Value == kObjC);
			return objcAttr != null;
		}

		public override void EnterInitializer_declaration ([NotNull] Initializer_declarationContext context)
		{
			var head = context.initializer_head ();

			var name = kDotCtor;

			// may be optional, otherwise return type is the instance type
			var returnType = GetInstanceName () + (head.OpQuestion () != null ? "?" : "");
			var accessibility = AccessibilityFromModifiers (head.declaration_modifiers ());
			var isStatic = true;
			var hasThrows = context.throws_clause () != null || context.rethrows_clause () != null;
			var isFinal = ModifiersContains (head.declaration_modifiers (), kFinal);
			var isOptional = ModifiersContains (head.declaration_modifiers (), kOptional);
			var isConvenienceInit = ModifiersContains (head.declaration_modifiers (), kConvenience);
			var operatorKind = kNone;
			var attributes = GatherAttributes (head.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isMutating = ModifiersContains (head.declaration_modifiers (), kMutating);
			var isRequired = ModifiersContains (head.declaration_modifiers (), kRequired);
			var isProperty = false;
			var functionDecl = ToFunctionDeclaration (name, returnType, accessibility, isStatic, hasThrows,
				isFinal, isOptional, isConvenienceInit, objCSelector: null, operatorKind,
				isDeprecated, isUnavailable, isMutating, isRequired, isProperty,
				isAsync: false, attributes);
			currentElement.Push (functionDecl);
		}

		public override void ExitInitializer_declaration ([NotNull] Initializer_declarationContext context)
		{
			ExitFunctionWithName (kDotCtor);
		}

		public override void EnterDeinitializer_declaration ([NotNull] Deinitializer_declarationContext context)
		{
			var name = kDotDtor;
			var returnType = "()";
			// this might have to be forced to public, otherwise deinit is always internal, which it
			// decidedly is NOT.
			var accessibility = kPublic;
			var isStatic = false;
			var hasThrows = false;
			var isFinal = ModifiersContains (context.declaration_modifiers (), kFinal);
			var isOptional = ModifiersContains (context.declaration_modifiers (), kOptional);
			var isConvenienceInit = false;
			var operatorKind = kNone;
			var attributes = GatherAttributes (context.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isMutating = ModifiersContains (context.declaration_modifiers (), kMutating);
			var isRequired = ModifiersContains (context.declaration_modifiers (), kRequired);
			var isProperty = false;
			var functionDecl = ToFunctionDeclaration (name, returnType, accessibility, isStatic, hasThrows,
				isFinal, isOptional, isConvenienceInit, objCSelector: null, operatorKind,
				isDeprecated, isUnavailable, isMutating, isRequired, isProperty, isAsync: false, attributes);

			// always has two parameter lists: (instance)()
			currentElement.Push (functionDecl);
			var parameterLists = new XElement (kParameterLists, MakeInstanceParameterList ());
			currentElement.Pop ();

			parameterLists.Add (new XElement (kParameterList, new XAttribute (kIndex, "1")));
			functionDecl.Add (parameterLists);

			currentElement.Push (functionDecl);
		}

		public override void ExitDeinitializer_declaration ([NotNull] Deinitializer_declarationContext context)
		{
			ExitFunctionWithName (kDotDtor);
		}

		public override void EnterSubscript_declaration ([NotNull] Subscript_declarationContext context)
		{
			// subscripts are...funny.
			// They have one parameter list but expand out to two function declarations
			// To handle this, we process the parameter list here for the getter
			// If there's a setter, we make one of those too.
			// Then since we're effectively done, we push a special XElement on the stack
			// named IGNORE which will make the parameter list event handler exit.
			// On ExitSubscript_declaration, we remove the IGNORE tag

			var head = context.subscript_head ();
			var resultType = TypeText (context.subscript_result ().type ());
			var accessibility = AccessibilityFromModifiers (head.declaration_modifiers ());
			var attributes = GatherAttributes (head.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isStatic = false;
			var hasThrows = false;
			var isAsync = HasAsync (context.getter_setter_keyword_block ()?.getter_keyword_clause ());
			var isFinal = ModifiersContains (head.declaration_modifiers (), kFinal);
			var isOptional = ModifiersContains (head.declaration_modifiers (), kOptional);
			var isMutating = ModifiersContains (head.declaration_modifiers (), kMutating);
			var isRequired = ModifiersContains (head.declaration_modifiers (), kRequired);
			var isProperty = true;

			var getParamList = MakeParameterList (head.parameter_clause ().parameter_list (), 1, true);
			var getFunc = ToFunctionDeclaration (kGetSubscript, resultType, accessibility, isStatic, hasThrows,
				isFinal, isOptional, isConvenienceInit: false, objCSelector: null, kNone,
				isDeprecated, isUnavailable, isMutating, isRequired, isProperty, isAsync: isAsync, attributes);

			currentElement.Push (getFunc);
			var getParamLists = new XElement (kParameterLists, MakeInstanceParameterList (), getParamList);
			currentElement.Pop ();

			getFunc.Add (getParamLists);
			AddObjCSelector (getFunc);

			AddElementToParentMembers (getFunc);

			var setParamList = context.getter_setter_keyword_block ()?.setter_keyword_clause () != null
				? MakeParameterList (head.parameter_clause ().parameter_list (), 1, true, startIndex: 1) : null;


			if (setParamList != null) {
				var index = 0;
				var parmName = context.getter_setter_keyword_block ().setter_keyword_clause ().new_value_name ()?.GetText ()
					?? kNewValue;
				var newValueParam = new XElement (kParameter, new XAttribute (nameof (index), index.ToString ()),
					new XAttribute (kType, resultType), new XAttribute (kPublicName, ""),
					new XAttribute (kPrivateName, parmName), new XAttribute (kIsVariadic, false));
				setParamList.AddFirst (newValueParam);

				var setFunc = ToFunctionDeclaration (kSetSubscript, "()", accessibility, isStatic, hasThrows,
					isFinal, isOptional, isConvenienceInit: false, objCSelector: null, kNone,
					isDeprecated, isUnavailable, isMutating, isRequired, isProperty, isAsync: false, attributes);

				currentElement.Push (setFunc);
				var setParamLists = new XElement (kParameterLists, MakeInstanceParameterList (), setParamList);
				currentElement.Pop ();

				setFunc.Add (setParamLists);
				AddObjCSelector (setFunc);
				AddElementToParentMembers (setFunc);
			}

			// this makes the subscript parameter list get ignored because we already handled it.
			PushIgnore ();
		}

		public override void ExitSubscript_declaration ([NotNull] Subscript_declarationContext context)
		{
			PopIgnore ();
		}

		string TypeText (ParserRuleContext ty)
		{
			return SyntaxDesugaringParser.TypeText (inputStream, ty);
		}

		public override void EnterVariable_declaration ([NotNull] Variable_declarationContext context)
		{
			var head = context.variable_declaration_head ();
			var accessibility = AccessibilityFromModifiers (head.declaration_modifiers ());
			var attributes = GatherAttributes (head.attributes ());
			var isDeprecated = CheckForDeprecated (attributes);
			var isUnavailable = CheckForUnavailable (attributes);
			var isStatic = ModifiersContains (head.declaration_modifiers (), kStatic);
			var isFinal = ModifiersContains (head.declaration_modifiers (), kFinal);
			var isLet = head.let_clause () != null;
			var isOptional = ModifiersContains (head.declaration_modifiers (), kOptional);
			var isMutating = ModifiersContains (head.declaration_modifiers (), kMutating);
			var isRequired = ModifiersContains (head.declaration_modifiers (), kRequired);
			var isProperty = true;

			foreach (var tail in context.variable_declaration_tail ()) {
				var name = UnTick (tail.variable_name ().GetText ());
				var resultType = TypeText (tail.type_annotation ().type ());
				var hasThrows = false;
				var isAsync = HasAsync (tail.getter_setter_keyword_block ()?.getter_keyword_clause ());

				var getParamList = new XElement (kParameterList, new XAttribute (kIndex, "1"));
				var getFunc = ToFunctionDeclaration ("get_" + name,
					resultType, accessibility, isStatic, hasThrows, isFinal, isOptional,
					isConvenienceInit: false, objCSelector: null, operatorKind: kNone, isDeprecated,
					isUnavailable, isMutating, isRequired, isProperty, isAsync: isAsync, attributes);

				currentElement.Push (getFunc);
				var getParamLists = new XElement (kParameterLists, MakeInstanceParameterList (), getParamList);
				currentElement.Pop ();
				getFunc.Add (getParamLists);
				AddElementToParentMembers (getFunc);
				AddObjCSelector (getFunc);

				var setParamList = HasSetter (tail, isLet) ?
					new XElement (kParameterList, new XAttribute (kIndex, "1")) : null;

				if (setParamList != null) {
					var parmName = tail.getter_setter_keyword_block ()?.setter_keyword_clause ().new_value_name ()?.GetText ()
						?? kNewValue;
					var setterType = EscapePossibleClosureType (resultType);
					var newValueParam = new XElement (kParameter, new XAttribute (kIndex, "0"),
						new XAttribute (kType, setterType), new XAttribute (kPublicName, parmName),
						new XAttribute (kPrivateName, parmName), new XAttribute (kIsVariadic, false));
					setParamList.Add (newValueParam);
					var setFunc = ToFunctionDeclaration ("set_" + name,
						"()", accessibility, isStatic, hasThrows, isFinal, isOptional,
						isConvenienceInit: false, objCSelector: null, operatorKind: kNone, isDeprecated,
						isUnavailable, isMutating, isRequired, isProperty, isAsync: false, attributes);

					currentElement.Push (setFunc);
					var setParamLists = new XElement (kParameterLists, MakeInstanceParameterList (), setParamList);
					currentElement.Pop ();

					setFunc.Add (setParamLists);
					AddElementToParentMembers (setFunc);
					AddObjCSelector (setFunc);
				}

				var prop = new XElement (kProperty, new XAttribute (kName, name),
					new XAttribute (nameof (accessibility), accessibility),
					new XAttribute (kType, resultType),
					new XAttribute (kStorage, kComputed),
					new XAttribute (nameof (isStatic), XmlBool (isStatic)),
					new XAttribute (nameof (isLet), XmlBool (isLet)),
					new XAttribute (nameof (isDeprecated), XmlBool (isDeprecated)),
					new XAttribute (nameof (isUnavailable), XmlBool (isUnavailable)),
					new XAttribute (nameof (isOptional), XmlBool (isOptional)));
				AddElementToParentMembers (prop);
			}

			PushIgnore ();
		}

		bool HasSetter (Variable_declaration_tailContext context, bool isLet)
		{
			// conditions for having a setter:
			// defaultInitializer and getter_setter_keyword are both null (public var foo: Type)
			// defaultInitializer is not null (public var foo: Type = initialValue)
			// getter_setter_keyword_block is non-null and the getter_setter_keyword_block
			// has a non-null setter_keyword_clause (public var foo:Type { get; set; }, public foo: Type { set }

			var defaultInitializer = context.defaultInitializer ();
			var gettersetter = context.getter_setter_keyword_block ();

			return !isLet && ((defaultInitializer == null && gettersetter == null) ||
				defaultInitializer != null ||
				gettersetter?.setter_keyword_clause () != null);
		}

		public override void EnterExtension_declaration ([NotNull] Extension_declarationContext context)
		{
			var onType = UnTick (TypeText (context.type_identifier ()));
			var inherits = GatherInheritance (context.type_inheritance_clause (), forceProtocolInheritance: true);
			// why, you say, why put a kKind tag into an extension?
			// The reason is simple: this is a hack. Most of the contents
			// of an extension are the same as a class and as a result we can
			// pretend that it's a class and everything will work to fill it out
			// using the class/struct/enum code for members.
			var extensionElem = new XElement (kExtension,
				new XAttribute (nameof (onType), onType),
				new XAttribute (kKind, kClass));
			if (inherits?.Count > 0)
				extensionElem.Add (new XElement (nameof (inherits), inherits.ToArray ()));
			currentElement.Push (extensionElem);
			extensions.Add (extensionElem);
		}

		public override void ExitExtension_declaration ([NotNull] Extension_declarationContext context)
		{
			var extensionElem = currentElement.Pop ();
			var onType = extensionElem.Attribute (kOnType);
			var givenOnType = onType.Value;
			var actualOnType = UnTick (TypeText (context.type_identifier ()));
			if (givenOnType != actualOnType)
				throw new Exception ($"extension type mismatch on exit declaration: expected {actualOnType} but got {givenOnType}");
			// remove the kKind attribute - you've done your job.
			extensionElem.Attribute (kKind)?.Remove ();

			currentElement.Peek ().Add (extensionElem);
		}

		public override void ExitImport_statement ([NotNull] Import_statementContext context)
		{
			// this is something like: import class Foo.Bar
			// and we're not handling that yet
			if (context.import_kind () != null)
				return;
			importModules.Add (context.import_path ().GetText ());
		}

		public override void EnterOperator_declaration ([NotNull] Operator_declarationContext context)
		{
			var operatorElement = InfixOperator (context.infix_operator_declaration ())
				?? PostfixOperator (context.postfix_operator_declaration ())
				?? PrefixOperator (context.prefix_operator_declaration ());
			operators.Add (operatorElement);

			currentElement.Peek ().Add (operatorElement);
		}

		public override void EnterOptional_type ([NotNull] Optional_typeContext context)
		{
		}

		XElement InfixOperator (Infix_operator_declarationContext context)
		{
			if (context == null)
				return null;
			return GeneralOperator (kInfix, context.@operator (),
				context.infix_operator_group ()?.precedence_group_name ()?.GetText () ?? "");
		}

		XElement PostfixOperator (Postfix_operator_declarationContext context)
		{
			if (context == null)
				return null;
			return GeneralOperator (kPostfix, context.@operator (), "");
		}

		XElement PrefixOperator (Prefix_operator_declarationContext context)
		{
			if (context == null)
				return null;
			return GeneralOperator (kPrefix, context.@operator (), "");
		}

		XElement GeneralOperator (string operatorKind, OperatorContext context, string precedenceGroup)
		{
			return new XElement (kOperator,
				new XAttribute (kName, context.Operator ().GetText ()),
				new XAttribute (nameof (operatorKind), operatorKind),
				new XAttribute (nameof (precedenceGroup), precedenceGroup));
		}

		XElement HandleGenerics (Generic_parameter_clauseContext genericContext, Generic_where_clauseContext whereContext, bool addParentGenerics)
		{
			if (genericContext == null)
				return null;
			var genericElem = new XElement (kGenericParameters);
			if (addParentGenerics)
				AddParentGenerics (genericElem);
			foreach (var generic in genericContext.generic_parameter_list ().generic_parameter ()) {
				var name = UnTick (TypeText (generic.type_name ()));
				var genParam = new XElement (kParam, new XAttribute (kName, name));
				genericElem.Add (genParam);
				var whereType = TypeText (generic.type_identifier ()) ??
					TypeText (generic.protocol_composition_type ());
				if (whereType != null) {
					genericElem.Add (MakeConformanceWhere (name, whereType));
				}
			}

			if (whereContext == null)
				return genericElem;

			foreach (var requirement in whereContext.requirement_list ().requirement ()) {
				if (requirement.conformance_requirement () != null) {
					var name = UnTick (TypeText (requirement.conformance_requirement ().type_identifier () [0]));

					// if there is no protocol composition type, then it's the second type identifier
					var from = TypeText (requirement.conformance_requirement ().protocol_composition_type ())
						?? TypeText (requirement.conformance_requirement ().type_identifier () [1]);
					genericElem.Add (MakeConformanceWhere (name, from));
				} else {
					var name = UnTick (TypeText (requirement.same_type_requirement ().type_identifier ()));
					var type = TypeText (requirement.same_type_requirement ().type ());
					genericElem.Add (MakeEqualityWhere (name, type));
				}
			}

			return genericElem;
		}

		public override void ExitTypealias_declaration ([NotNull] Typealias_declarationContext context)
		{
			var name = UnTick (context.typealias_name ().GetText ());
			var generics = TypeText (context.generic_parameter_clause ()) ?? "";
			var targetType = TypeText (context.typealias_assignment ().type ());
			var access = ToAccess (context.access_level_modifier ());
			var map = new XElement (kTypeAlias, new XAttribute (kName, name + generics),
				new XAttribute (kAccessibility, access),
				new XAttribute (kType, targetType));
			if (access != null) {
				if (currentElement.Peek ().Name == kModule) {
					typeAliasMap.Add (map);
				}  else {
					var curr = currentElement.Peek ();
					var aliaslist = curr.Element (kTypeAliases);
					if (aliaslist == null) {
						aliaslist = new XElement (kTypeAliases);
						curr.Add (aliaslist);
					}
					aliaslist.Add (map);
				}
			}
		}

		XElement MakeConformanceWhere (string name, string from)
		{
			return new XElement (kWhere, new XAttribute (nameof (name), name),
				new XAttribute (kRelationship, kInherits),
				new XAttribute (nameof (from), from));
		}

		XElement MakeEqualityWhere (string firsttype, string secondtype)
		{
			return new XElement (kWhere, new XAttribute (nameof (firsttype), firsttype),
				new XAttribute (kRelationship, kEquals),
				new XAttribute (nameof (secondtype), secondtype));
		}

		public override void ExitVariable_declaration ([NotNull] Variable_declarationContext context)
		{
			PopIgnore ();
		}

		void PushIgnore ()
		{
			currentElement.Push (new XElement (kIgnore));
		}

		void PopIgnore ()
		{
			var elem = currentElement.Pop ();
			if (elem.Name != kIgnore)
				throw new ParseException ($"Expected an {kIgnore} element, but got {elem}");
		}

		bool ShouldIgnore ()
		{
			return currentElement.Peek ().Name == kIgnore;
		}

		public override void EnterParameter_clause ([NotNull] Parameter_clauseContext context)
		{
			if (ShouldIgnore ())
				return;

			var parameterLists = new XElement (kParameterLists);
			XElement instanceList = MakeInstanceParameterList ();
			var formalIndex = 0;
			if (instanceList != null) {
				parameterLists.Add (instanceList);
				formalIndex = 1;
			}

			var formalArguments = MakeParameterList (context.parameter_list (), formalIndex, false);

			parameterLists.Add (formalArguments);
			currentElement.Peek ().Add (parameterLists);
		}

		XElement MakeParameterList (Parameter_listContext parmList, int index, bool isSubscript, int startIndex = 0)
		{
			var formalArguments = new XElement (kParameterList, new XAttribute (kIndex, index.ToString ()));

			if (parmList != null) {
				var i = startIndex;
				foreach (var parameter in parmList.parameter ()) {
					var parameterElement = ToParameterElement (parameter, i, isSubscript);
					formalArguments.Add (parameterElement);
					i++;
				}
			}
			return formalArguments;
		}

		XElement MakeInstanceParameterList ()
		{
			var topElem = currentElement.Peek ();
			if (topElem.Name == kModule)
				return null;
			if (topElem.Name != kFunc)
				throw new ParseException ($"Expecting a func node but got {topElem.Name}");
			if (NominalParentAfter (0) == null)
				return null;
			var funcName = topElem.Attribute (kName).Value;
			var isStatic = topElem.Attribute (kIsStatic).Value == "true";
			var isCtorDtor = IsCtorDtor (funcName);
			var isClass = NominalParentAfter (0).Attribute (kKind).Value == kClass;
			var instanceName = GetInstanceName ();
			var type = $"{(isClass ? "" : "inout ")}{instanceName}{(isCtorDtor ? ".Type" : "")}";
			var parameter = new XElement (kParameter, new XAttribute (kType, type),
				new XAttribute (kIndex, "0"), new XAttribute (kPublicName, "self"),
				new XAttribute (kPrivateName, "self"), new XAttribute (kIsVariadic, "false"));
			return new XElement (kParameterList, new XAttribute (kIndex, "0"), parameter);
		}

		void AddParentGenerics (XElement genericResult)
		{
			var parentGenerics = new List<XElement> ();
			for (int i =0; i < currentElement.Count; i++) {
				var elem = currentElement.ElementAt (i);
				if (!IsNominal (elem))
					continue;
				var elemGenerics = elem.Element (kGenericParameters);
				if (elemGenerics == null)
					continue;
				foreach (var param in elemGenerics.Descendants (kParam)) {
					parentGenerics.Add (new XElement (param));
				}
			}
			genericResult.Add (parentGenerics.ToArray ());
		}

		XElement NominalParentAfter (int start)
		{
			for (var i = start + 1; i < currentElement.Count; i++) {
				var elem = currentElement.ElementAt (i);
				if (IsNominal (elem))
					return elem;
			}
			return null;
		}

		bool IsNominal (XElement elem)
		{
			var kind = elem.Attribute (kKind)?.Value;
			return kind != null && (kind == kClass || kind == kStruct || kind == kEnum || kind == kProtocol);
		}

		string GetInstanceName ()
		{
			var nameBuffer = new StringBuilder ();
			for (int i = 0; i < currentElement.Count; i++) {
				var elem = currentElement.ElementAt (i);
				if (IsNominal (elem)) {
					if (elem.Name == kExtension)
						return elem.Attribute (kOnType).Value;
					if (nameBuffer.Length > 0)
						nameBuffer.Insert (0, '.');
					nameBuffer.Insert (0, elem.Attribute (kName).Value);
					var generics = elem.Element (kGenericParameters);
					if (generics != null) {
						AddGenericsToName (nameBuffer, generics);
					}
				}
			}
			nameBuffer.Insert (0, '.');
			var module = currentElement.Last ();
			nameBuffer.Insert (0, moduleName);
			return nameBuffer.ToString ();
		}

		void AddGenericsToName (StringBuilder nameBuffer, XElement generics)
		{
			var isFirst = true;
			foreach (var name in GenericNames (generics)) {
				if (isFirst) {
					nameBuffer.Append ("<");
					isFirst = false;
				} else {
					nameBuffer.Append (", ");
				}
				nameBuffer.Append (name);
			}
			if (!isFirst)
				nameBuffer.Append (">");
		}

		IEnumerable<string> GenericNames (XElement generics)
		{
			return generics.Elements ().Where (elem => elem.Name == kParam).Select (elem => elem.Attribute (kName).Value);
		}

		XElement ToParameterElement (ParameterContext context, int index, bool isSubscript)
		{
			var typeAnnotation = context.type_annotation ();
			var isInOut = typeAnnotation.inout_clause () != null;
			var type = TypeText (typeAnnotation.type ());
			var privateName = NoUnderscore (UnTick (context.local_parameter_name ()?.GetText ()) ?? "");
			var replacementPublicName = isSubscript ? "" : privateName;
			var publicName = NoUnderscore (UnTick (context.external_parameter_name ()?.GetText ()) ?? replacementPublicName);
			var isVariadic = context.range_operator () != null;
			if (isVariadic)
				type = $"Swift.Array<{type}>";
			var isEscaping = AttributesContains (typeAnnotation.attributes (), kEscaping);
			var isAutoClosure = AttributesContains (typeAnnotation.attributes (), kAutoClosure);
			var typeBuilder = new StringBuilder ();
			if (isEscaping)
				typeBuilder.Append ("@escaping[] ");
			if (isAutoClosure)
				typeBuilder.Append ("@autoclosure[] ");
			if (isInOut)
				typeBuilder.Append ("inout ");
			typeBuilder.Append (type);
			type = typeBuilder.ToString ();

			var paramElement = new XElement (kParameter, new XAttribute (nameof (index), index.ToString ()),
				new XAttribute (nameof (type), type), new XAttribute (nameof (publicName), publicName),
				new XAttribute (nameof (privateName), privateName), new XAttribute (nameof (isVariadic), XmlBool (isVariadic)));
			return paramElement;
		}

		static string NoUnderscore (string s)
		{
			return s == "_" ? "" : s;
		}

		XElement GatherAttributes (AttributesContext context)
		{
			if (context == null)
				return null;
			var attributes = new XElement (kAttributes);
			foreach (var attr in context.attribute ()) {
				var attrElement = GatherAttribute (attr);
				if (attrElement != null)
					attributes.Add (attrElement);
			}
			return attributes.HasElements ? attributes : null;
		}

		XElement GatherAttribute (AttributeContext context)
		{
			var attribute = new XElement (kAttribute, new XAttribute (kName, context.attribute_name ().GetText ()));
			if (context.attribute_argument_clause () != null) {
				var parameters = GatherParameters (context.attribute_argument_clause ()?.balanced_tokens ());
				if (parameters != null)
					attribute.Add (parameters);
			}
			return attribute;
		}

		XElement GatherParameters (Balanced_tokensContext context)
		{
			if (context == null)
				return null;
			var parameterlist = new XElement (kAttributeParameterList);

			foreach (var balancedToken in context.balanced_token ()) {
				var parameter = ToAttributeParameter (balancedToken);
				if (parameter != null)
					parameterlist.Add (parameter);
			}
			return parameterlist.HasElements ? parameterlist : null;
		}

		XElement ToAttributeParameter (Balanced_tokenContext context)
		{
			if (context.balanced_tokens () != null) {
				var sublist = new XElement (kAttributeParameter, new XAttribute (kKind, kSublist));
				var subparams = GatherParameters (context.balanced_tokens ());
				if (subparams != null)
					sublist.Add (subparams);
				return sublist;
			}

			if (context.label_identifier () != null) {
				var label = new XElement (kAttributeParameter, new XAttribute (kKind, kLabel),
					new XAttribute (kValue, context.label_identifier ().GetText ()));
				return label;
			}

			if (context.literal () != null) {
				var literal = new XElement (kAttributeParameter, new XAttribute (kKind, kLiteral),
					new XAttribute (kValue, context.literal ().GetText ()));
				return literal;
			}

			// make the operator look like a label
			if (context.@operator () != null) {
				var label = new XElement (kAttributeParameter, new XAttribute (kKind, kLabel),
					new XAttribute (kValue, context.@operator ().GetText ()));
				return label;
			}

			if (context.any_punctuation_for_balanced_token () != null) {
				var label = new XElement (kAttributeParameter, new XAttribute (kKind, kLabel),
					new XAttribute (kValue, context.any_punctuation_for_balanced_token ().GetText ()));
				return label;
			}

			return null;
		}

		List<XElement> GatherInheritance (Type_inheritance_clauseContext context, bool forceProtocolInheritance,
			bool removeNonProtocols = false)
		{
			var inheritance = new List<XElement> ();
			if (context == null)
				return inheritance;
			var list = context.type_inheritance_list ();
			bool first = true;
			while (list != null) {
				var inheritanceKind = forceProtocolInheritance ? kProtocol :
					(inheritance.Count > 0 ? kProtocol : kLittleUnknown);
				var type = TypeText (list.type_identifier ());
				if (!(first && removeNonProtocols && TypeIsNotProtocol (type))) {
					var elem = new XElement (kInherit, new XAttribute (kType, type),
						new XAttribute (nameof (inheritanceKind), inheritanceKind));
					inheritance.Add (elem);
					if (inheritanceKind == kLittleUnknown)
						unknownInheritance.Add (elem);
				}
				first = false;
				list = list.type_inheritance_list ();
			}

			return inheritance;
		}

		bool TypeIsNotProtocol (string type)
		{
			// special case this - the type database as this as "other"
			// which is technically not a protocol, but it is a protocol.
			if (type == "Swift.Error")
				return false;
			var parts = type.Split ('.');
			if (parts.Length == 1)
				return true; // generic
			var module = parts [0];
			if (!typeDatabase.ModuleNames.Contains (module)) {
				moduleLoader.Load (module, typeDatabase);
			}
			var entity = typeDatabase.TryGetEntityForSwiftName (type);
			if (entity != null && entity.EntityType != EntityType.Protocol)
				return true;
			return false;
		}

		XElement ToTypeDeclaration (string kind, string name, string accessibility, bool isObjC,
			bool isFinal, bool isDeprecated, bool isUnavailable, List<XElement> inherits, XElement generics,
			XElement attributes)
		{
			var xobjects = new List<XObject> ();
			if (generics != null)
				xobjects.Add (generics);
			xobjects.Add (new XAttribute (nameof (kind), kind));
			xobjects.Add (new XAttribute (nameof (name), name));
			xobjects.Add (new XAttribute (nameof (accessibility), accessibility));
			xobjects.Add (new XAttribute (nameof (isObjC), XmlBool (isObjC)));
			xobjects.Add (new XAttribute (nameof (isFinal), XmlBool (isFinal)));
			xobjects.Add (new XAttribute (nameof (isDeprecated), XmlBool (isDeprecated)));
			xobjects.Add (new XAttribute (nameof (isUnavailable), XmlBool (isUnavailable)));

			xobjects.Add (new XElement (kMembers));
			if (inherits != null && inherits.Count > 0)
				xobjects.Add (new XElement (nameof (inherits), inherits.ToArray ()));
			if (attributes != null)
				xobjects.Add (attributes);
			return new XElement (kTypeDeclaration, xobjects.ToArray ());
		}


		XElement ToFunctionDeclaration (string name, string returnType, string accessibility,
			bool isStatic, bool hasThrows, bool isFinal, bool isOptional, bool isConvenienceInit,
			string objCSelector, string operatorKind, bool isDeprecated, bool isUnavailable,
			bool isMutating, bool isRequired, bool isProperty, bool isAsync, XElement attributes)
		{
			var decl = new XElement (kFunc, new XAttribute (nameof (name), name), new XAttribute (nameof (returnType), returnType),
				new XAttribute (nameof (accessibility), accessibility), new XAttribute (nameof (isStatic), XmlBool (isStatic)),
				new XAttribute (nameof (hasThrows), XmlBool (hasThrows)), new XAttribute (nameof (isFinal), XmlBool (isFinal)),
				new XAttribute (nameof (isOptional), XmlBool (isOptional)),
				new XAttribute (nameof (isConvenienceInit), XmlBool (isConvenienceInit)),
				new XAttribute (nameof (isDeprecated), XmlBool (isDeprecated)),
				new XAttribute (nameof (isUnavailable), XmlBool (isUnavailable)),
				new XAttribute (nameof (isRequired), XmlBool (isRequired)),
				new XAttribute (kIsAsync, XmlBool (isAsync)),
				new XAttribute (kIsProperty, XmlBool (isProperty)),
				new XAttribute (nameof (isMutating), XmlBool (isMutating)));

			if (operatorKind != null) {
				decl.Add (new XAttribute (nameof (operatorKind), operatorKind));
			}
			if (objCSelector != null) {
				decl.Add (new XAttribute (nameof (objCSelector), objCSelector));
			}
			if (attributes != null) {
				decl.Add (attributes);
			}
			return decl;
		}

		bool CheckForDeprecated (XElement attributes)
		{
			var availableTags = AvailableAttributes (attributes);
			foreach (var attribute in availableTags) {
				var args = AttrbuteParameters (attribute);
				var platform = args [0];
				if (!PlatformMatches (platform))
					continue;

				var deprecatedIndex = args.IndexOf (kDeprecated);
				if (deprecatedIndex < 0)
					continue;
				var deprecatedVersion = GetVersionAfter (args, deprecatedIndex);
				if (TargetVersionIsLessOrEqual (deprecatedVersion))
					return true;
			}
			return false;
		}

		bool CheckForUnavailable (XElement attributes)
		{
			var availableTags = AvailableAttributes (attributes);
			foreach (var attribute in availableTags) {
				var args = AttrbuteParameters (attribute);
				// if unavailable exists, need to match platform
				if (args.IndexOf (kUnavailable) >= 0 && PlatformMatches (args [0]))
					return true;

			}
			return !CheckForAvailable (attributes);
		}

		bool CheckForAvailable (XElement attributes)
		{
			var availableTags = AvailableAttributes (attributes);
			foreach (var attribute in availableTags) {
				var args = AttrbuteParameters (attribute);
				if (IsShortHand (args)) {
					return AvailableShorthand (args);
				} else {
					return AvailableLonghand (args);
				}
			}
			return true;
		}

		bool AvailableLonghand (List<string> args)
		{
			// args will be plat , specifiers
			// specificers will be:
			// introduced: version
			// deprecated: version
			// obsoleted: version
			// unavailable
			var platform = args [0];
			if (!PlatformMatches (platform))
				return true;

			// if unavailable is present, it's not there.
			if (args.IndexOf (kUnavailable) >= 0)
				return false;

			var introIndex = args.IndexOf (kIntroduced);
			if (introIndex >= 0) {
				var introVersion = GetVersionAfter (args, introIndex);
				if (TargetVersionIsGreaterOrEqual (introVersion))
					return false;
			}

			var obsoletedIndex = args.IndexOf (kObsoleted);
			if (obsoletedIndex >= 0) {
				var obsoletedVersion = GetVersionAfter (args, obsoletedIndex);
				if (TargetVersionIsLessOrEqual (obsoletedVersion))
					return false;
			}
			return true;
		}

		bool AvailableShorthand (List<string> args)
		{
			// args will be: plat ver . x . y , plat ver , ... *

			var startIndex = 0;
			while (startIndex < args.Count) {
				if (args [startIndex] == "*")
					return false;
				var platform = args [startIndex];
				if (PlatformMatches (platform)) {
					var endIndex = args.IndexOf (",", startIndex + 1);
					var versionNumber = args.GetRange (startIndex + 1, endIndex - startIndex);
					if (TargetVersionIsGreaterOrEqual (versionNumber))
						return true;
				} else {
					startIndex = args.IndexOf (",", startIndex + 1);
				}
			}
			return false;
		}

		bool IsShortHand (List<string> args)
		{
			if (args [1] == ",")
				return false;
			return args.Last () == "*";
		}

		Version GetVersionAfter (List<string> pieces, int indexAfter)
		{
			var colonIndex = ColonAfter (pieces, indexAfter);
			if (colonIndex < 0)
				return new Version (0, 0);
			var start = colonIndex + 1;
			var end = start + 1;
			while (end < pieces.Count && pieces [end] != ",")
				end++;
			var versionPieces = pieces.GetRange (start, end - start);
			return VersionPiecesToVersion (versionPieces);
		}

		int ColonAfter (List<string> pieces, int start)
		{
			for (int i = start + 1; i < pieces.Count; i++) {
				if (pieces [i] == ":")
					return i;
				if (pieces [i] == ",")
					return -1;
			}
			return -1;
		}

		bool TargetVersionIsGreaterOrEqual (List<string> versionPieces)
		{
			var expectedVersion = VersionPiecesToVersion (versionPieces);
			return TargetVersionIsGreaterOrEqual (expectedVersion);
		}

		bool TargetVersionIsGreaterOrEqual (Version expectedVersion)
		{
			var compiledVersionStr = PlatformVersionFromModuleFlags ();
			if (String.IsNullOrEmpty (compiledVersionStr))
				return true; // no version, I guess it's good?
			var compiledVersion = new Version (compiledVersionStr);
			return expectedVersion >= compiledVersion;
		}

		bool TargetVersionIsLessOrEqual (List<string> versionPieces)
		{
			var expectedVersion = VersionPiecesToVersion (versionPieces);
			return TargetVersionIsLessOrEqual (expectedVersion);
		}

		bool TargetVersionIsLessOrEqual (Version expectedVersion)
		{
			var compiledVersionStr = PlatformVersionFromModuleFlags ();
			if (String.IsNullOrEmpty (compiledVersionStr))
				return true; // no version, I guess it's good?
			var compiledVersion = new Version (compiledVersionStr);
			return expectedVersion <= compiledVersion;
		}

		Version VersionPiecesToVersion (List<string> pieces)
		{
			var sb = new StringBuilder ();
			for (int i = 0; i < pieces.Count; i++) {
				if (pieces [i] == "." && i + 1 < pieces.Count && pieces [i + 1] == "*")
					break;
				sb.Append (pieces [i]);
			}
			return new Version (sb.ToString ());
		}

		IEnumerable<XElement> AvailableAttributes (XElement attributes)
		{
			if (attributes == null)
				return Enumerable.Empty<XElement> ();
			return attributes.Descendants (kAttribute).Where (el => el.Attribute (kName).Value == kAvailable);

		}

		List<string> AttrbuteParameters (XElement attribute)
		{
			return attribute.Descendants (kAttributeParameter).Select (at =>
				at.Attribute (kValue)?.Value ?? "").ToList ();
		}

		bool PlatformMatches (string platform)
		{
			var currentPlatform = PlatformFromModuleFlags ();
			switch (platform) {
			case "*":
			case "swift":
				return true;
			case "iOS":
			case "iOSApplicationExtension":
				return currentPlatform.StartsWith ("ios", StringComparison.Ordinal) &&
					!currentPlatform.StartsWith ("ios-macabi", StringComparison.Ordinal);
			case "macOS":
			case "macOSApplicationExtension":
				return currentPlatform.StartsWith ("macos", StringComparison.Ordinal);
			case "macCatalyst":
			case "macCatalystExtension":
				return currentPlatform.StartsWith ("ios-macabi", StringComparison.Ordinal);
			case "watchOS":
			case "watchOSApplicationExtension":
				return currentPlatform.StartsWith ("watch", StringComparison.Ordinal);
			case "tvOS":
			case "tvOSApplicationExtension":
				return currentPlatform.StartsWith ("tv", StringComparison.Ordinal);
			default:
				return false;
			}
		}

		string PlatformFromModuleFlags ()
		{
			var flagsValue = TargetFromModuleFlags ();
			var os = flagsValue.ClangTargetOS ();
			var digitIndex = FirstDigitIndex (os);
			if (digitIndex < 0)
				return os;
			return os.Substring (0, digitIndex);
		}

		static int FirstDigitIndex (string s)
		{
			var index = 0;
			foreach (char c in s) {
				if (Char.IsDigit (c))
					return index;
				index++;
			}
			return -1;
		}

		string PlatformVersionFromModuleFlags ()
		{
			var flagsValue = TargetFromModuleFlags ();
			var os = flagsValue.ClangTargetOS ();
			var digitIndex = FirstDigitIndex (os);
			if (digitIndex < 0)
				return "";
			return os.Substring (digitIndex);
		}

		string TargetFromModuleFlags ()
		{
			string flagsValue = null;
			if (!moduleFlags.TryGetValue ("target", out flagsValue)) {
				return "";
			}
			return flagsValue;
		}

		static HashSet<string> ModulesThatWeCanSkip = new HashSet<string> () {
			"XamGlue",
			"RegisterAccess",
			"_StringProcessing",
			"_Concurrency",
		};

		void LoadReferencedModules ()
		{
			var failures = new StringBuilder ();
			foreach (var module in importModules) {
				// XamGlue and RegisterAccess may very well get
				// used, but the functions/types exported from these
				// should never need to be loaded.
				if (ModulesThatWeCanSkip.Contains (module))
					continue;
				// if (!moduleLoader.Load (module, typeDatabase)) {
				// 	if (failures.Length > 0)
				// 		failures.Append (", ");
				// 	failures.Append (module);
				// }
			}
			if (failures.Length > 0)
				throw new ParseException ($"Unable to load the following module(s): {failures.ToString ()}");
		}

		void PatchPossibleOperators ()
		{
			foreach (var func in functions) {
				var operatorKind = GetOperatorType (func.Item1);
				if (operatorKind != OperatorType.None) {
					func.Item2.Attribute (nameof (operatorKind))?.Remove ();
					func.Item2.SetAttributeValue (nameof (operatorKind), operatorKind.ToString ());
				}
			}
		}

		void PatchPossibleBadInheritance ()
		{
			foreach (var inh in unknownInheritance) {
				var type = inh.Attribute (kType).Value;
				if (IsLocalClass (type) || IsGlobalClass (type) || IsNSObject (type))
					inh.Attribute (kInheritanceKind).Value = kClass;
				else
					inh.Attribute (kInheritanceKind).Value = kProtocol;
			}
		}

		void PatchAssociatedTypeConformance ()
		{
			foreach (var assoc in associatedTypesWithConformance) {
				var conformances = assoc.Element (kConformingProtocols);
				var first = conformances.Element (kConformingProtocol);
				var className = (string)first.Attribute (kName);
				if (IsLocalClass (className) || IsGlobalClass (className)) {
					first.Remove ();
					if (conformances.Nodes ().Count () == 0)
						conformances.Remove ();
					assoc.Add (new XElement (kSuperclass, new XAttribute (kName, className)));
				}
			}
		}

		bool IsNSObject (string typeName)
		{
			return typeName == "ObjectiveC.NSObject";
		}

		bool IsLocalClass (string typeName)
		{
			return classes.Contains (typeName);
		}

		bool IsGlobalClass (string typeName)
		{
			return typeDatabase.EntityForSwiftName (typeName)?.EntityType == EntityType.Class;
		}

		void PatchExtensionSelfArgs ()
		{
			foreach (var ext in extensions) {
				var onType = (string)ext.Attribute (kOnType).Value;
				var parts = onType.Split ('.');
				if (parts [0] == "XamGlue")
					continue;
				if (parts.Length > 1 && !typeDatabase.ModuleNames.Contains (parts [0])) {
					moduleLoader.Load (parts [0], typeDatabase);
				}
				var entity = typeDatabase.EntityForSwiftName (onType);
				if (entity != null) {
					PatchExtensionSelfArgs (ext, entity);
				}
			}
		}

		void PatchExtensionSelfArgs (XElement ext, Entity entity)
		{
			var isStructOrScalar = entity.IsStructOrEnum || entity.EntityType == EntityType.Scalar;
			foreach (var func in ext.Descendants (kFunc)) {
				var selfArg = SelfParameter (func);
				if (selfArg == null)
					continue;
				var attr = selfArg.Attribute (kType);
				var type = (string)attr.Value;
				if (entity.Type.ContainsGenericParameters) {
					type = entity.Type.ToFullyQualifiedNameWithGenerics ();
					attr.Value = type;
					var generics = entity.Type.Generics.ToXElement ();
					if (func.Element (kGenericParameters) != null) {
						var funcGenerics = func.Element (kGenericParameters);
						funcGenerics.Remove ();
						foreach (var generic in funcGenerics.Elements ())
							generics.Add (generic);
					}
					func.Add (generics);
				}
				if (isStructOrScalar && !type.StartsWith ("inout", StringComparison.Ordinal)) {
					attr.Value = "inout " + type;
				}
			}
		}

		XElement SelfParameter (XElement func)
		{
			var selfList = WhereIndexZero (func.Descendants (kParameterList));
			if (selfList == null)
				return null;
			var selfArg = WhereIndexZero (selfList.Descendants (kParameter));
			return selfArg;
		}

		static XElement WhereIndexZero (IEnumerable<XElement> elems)
		{
			return elems.FirstOrDefault (el => (string)el.Attribute (kIndex).Value == "0");
		}

		void PatchExtensionShortNames ()
		{
			foreach (var ext in extensions) {
				var onType = TypeSpecParser.Parse (ext.Attribute (kOnType).Value);
				var replacementType = FullyQualify (onType);
				ext.Attribute (kOnType).Value = replacementType.ToString ();
				foreach (var func in ext.Descendants (kFunc)) {
					var selfArg = SelfParameter (func);
					if (selfArg == null)
						continue;
					onType = TypeSpecParser.Parse (selfArg.Attribute (kType).Value);
					replacementType = FullyQualify (onType);
					selfArg.Attribute (kType).Value = replacementType.ToString ();
				}
			}
		}

		TypeSpec FullyQualify (TypeSpec spec)
		{
			switch (spec.Kind) {
			case TypeSpecKind.Named:
				return FullyQualify (spec as NamedTypeSpec);
			case TypeSpecKind.Closure:
				return FullyQualify (spec as ClosureTypeSpec);
			case TypeSpecKind.ProtocolList:
				return FullyQualify (spec as ProtocolListTypeSpec);
			case TypeSpecKind.Tuple:
				return FullyQualify (spec as TupleTypeSpec);
			default:
				throw new NotImplementedException ($"unknown TypeSpec kind {spec.Kind}");
			}
		}

		TypeSpec FullyQualify (NamedTypeSpec named)
		{
			var dirty = false;
			var newName = named.Name;

			if (!named.Name.Contains (".")) {
				newName = ReplaceName (named.Name);
				dirty = true;
			}

			var genParts = new TypeSpec [named.GenericParameters.Count];
			var index = 0;
			foreach (var gen in named.GenericParameters) {
				var newGen = FullyQualify (gen);
				genParts[index++] = newGen;
				if (newGen != gen)
					dirty = true;
			}

			if (dirty) {
				var newNamed = new NamedTypeSpec (newName, genParts);
				newNamed.Attributes.AddRange (named.Attributes);
				return newNamed;
			}

			return named;
		}

		TypeSpec FullyQualify (TupleTypeSpec tuple)
		{
			var dirty = false;
			var parts = new TypeSpec [tuple.Elements.Count];
			var index = 0;
			foreach (var spec in tuple.Elements) {
				var newSpec = FullyQualify (spec);
				if (newSpec != spec)
					dirty = true;
				parts [index++] = newSpec;
			}

			if (dirty) {
				var newTup = new TupleTypeSpec (parts);
				newTup.Attributes.AddRange (tuple.Attributes);
				return newTup;
			}

			return tuple;
		}

		TypeSpec FullyQualify (ProtocolListTypeSpec protolist)
		{
			var dirty = false;
			var parts = new List<NamedTypeSpec> ();
			foreach (var named in protolist.Protocols.Keys) {
				var newNamed = FullyQualify (named);
				parts.Add (newNamed as NamedTypeSpec);
				if (newNamed != named)
					dirty = true;
			}

			if (dirty) {
				var newProto = new ProtocolListTypeSpec (parts);
				newProto.Attributes.AddRange (protolist.Attributes);
				return newProto;
			}

			return protolist;
		}

		TypeSpec FullyQualify (ClosureTypeSpec clos)
		{
			var dirty = false;
			var args = FullyQualify (clos.Arguments);
			if (args != clos.Arguments)
				dirty = true;
			var returnType = FullyQualify (clos.ReturnType);
			if (returnType != clos.ReturnType)
				dirty = true;

			if (dirty) {
				var newClosure = new ClosureTypeSpec (args, returnType);
				newClosure.Attributes.AddRange (clos.Attributes);
				return newClosure;
			}

			return clos;
		}

		string ReplaceName (string nonQualified)
		{
			Exceptions.ThrowOnNull (nonQualified, nameof (nonQualified));

			var localName = ReplaceLocalName (nonQualified);
			if (localName != null)
				return localName;
			var globalName = ReplaceGlobalName (nonQualified);
			if (globalName == null)
				throw new ParseException ($"Unable to find fully qualified name for non qualified type {nonQualified}");
			return globalName;
		}

		string ReplaceLocalName (string nonQualified)
		{
			foreach (var candidate in nominalTypes) {
				var candidateWithoutModule = StripModule (candidate);
				if (nonQualified == candidateWithoutModule)
					return candidate;
			}
			return null;
		}

		string ReplaceGlobalName (string nonQualified)
		{
			foreach (var module in importModules) {
				var candidateName = $"{module}.{nonQualified}";
				var entity = typeDatabase.TryGetEntityForSwiftName (candidateName);
				if (entity != null)
					return candidateName;
			}
			if (nonQualified == "EveryProtocol")
				return "XamGlue.EveryProtocol";
			return null;
		}

		string StripModule (string fullyQualifiedName)
		{
			if (fullyQualifiedName.StartsWith (moduleName, StringComparison.Ordinal))
				// don't forget the '.'
				return fullyQualifiedName.Substring (moduleName.Length + 1);
			return fullyQualifiedName; 
		}

		static bool AttributesContains (AttributesContext context, string key)
		{
			if (context == null)
				return false;
			foreach (var attr in context.attribute ()) {
				if (attr.attribute_name ().GetText () == key)
					return true;
			}
			return false;
		}

		static bool AttributesContainsAny (AttributesContext context, string [] keys)
		{
			foreach (var attr in context.attribute ()) {
				var attrName = attr.attribute_name ().GetText ();
				foreach (var key in keys) {
					if (key == attrName)
						return true;
				}
			}
			return false;
		}

		string EscapePossibleClosureType (string type)
		{
			var typeSpec = TypeSpecParser.Parse (type);
			return typeSpec is ClosureTypeSpec ? "@escaping[] " + type : type;
		}

		static Dictionary<string, string> accessMap = new Dictionary<string, string> () {
			{ kPublic, kPublicCap },
			{ kPrivate, kPrivateCap },
			{ kOpen, kOpenCap },
			{ kInternal, kInternalCap },
		};

		string AccessibilityFromModifiers (Declaration_modifiersContext context)
		{
			// If there is no context, we need to search for the appropriate context
			// Swift has a number of "interesting" rules for implicitly defined accessibility
			// If the parent element is a protocol, it's public
			// If the parent is public, internal, or open then it's open
			// If the parent is private or fileprivate, then it's private

			// Note that I don't make any distinction between private and fileprivate
			// From our point of view, they're the same: they're things that we don't
			// have access to and don't care about in writing a reflector of the public
			// API.
			if (context == null) {
				var parentElem = NominalParentAfter (-1);
				if (parentElem == null)
					return kInternalCap;
				if (parentElem.Attribute (kKind).Value == kProtocol)
					return kPublicCap;
				switch (parentElem.Attribute (kAccessibility).Value) {
				case kPublic:
				case kInternal:
				case kOpen:
					return kInternalCap;
				case kPrivate:
				case kFilePrivate:
					return kPrivateCap;
				}
			}
			foreach (var modifer in context.declaration_modifier ()) {
				string result;
				if (accessMap.TryGetValue (modifer.GetText (), out result))
					return result;
			}
			return kInternalCap;
		}

		static bool HasAsync (Getter_keyword_clauseContext context)
		{
			return context == null ? false : context.async_clause () != null;
		}

		static bool ModifiersContains (Declaration_modifiersContext context, string match)
		{
			if (context == null)
				return false;
			foreach (var modifier in context.declaration_modifier ()) {
				var text = modifier.GetText ();
				if (text == match)
					return true;
			}
			return false;
		}

		public override void EnterDeclaration_modifier ([NotNull] Declaration_modifierContext context)
		{
			var modifier = context.GetText ();

		}

		static bool ModifiersContainsAny (Declaration_modifiersContext context, string [] matches)
		{
			if (context == null)
				return false;
			foreach (var modifier in context.declaration_modifier ()) {
				var text = modifier.GetText ();
				foreach (var match in matches)
					if (text == match)
						return true;
			}
			return false;
		}

		static bool IsStaticOrClass (Declaration_modifiersContext context)
		{
			return ModifiersContainsAny (context, new string [] { kStatic, kClass });
		}

		static bool IsFinal (Declaration_modifiersContext context)
		{
			return ModifiersContains (context, kFinal);
		}

		void AddStructToCurrentElement (XElement elem)
		{
			var parentElement = GetOrCreateParentElement (kInnerStructs);
			parentElement.Add (elem);
			RegisterNominal (elem);
		}

		void AddEnumToCurrentElement (XElement elem)
		{
			var parentElement = GetOrCreateParentElement (kInnerEnums);
			parentElement.Add (elem);
			RegisterNominal (elem);
		}

		void AddClassToCurrentElement (XElement elem)
		{
			var parentElement = GetOrCreateParentElement (kInnerClasses);
			parentElement.Add (elem);
			RegisterNominal (elem);
		}

		void RegisterNominal (XElement elem)
		{
			var isClass = elem.Attribute (kKind).Value == kClass;
			var builder = new StringBuilder ();
			while (elem != null) {
				if (builder.Length > 0)
					builder.Insert (0, '.');
				var namePart = elem.Attribute (kName)?.Value ?? moduleName;
				builder.Insert (0, namePart);
				elem = elem.Parent;
			}
			var typeName = builder.ToString ();
			nominalTypes.Add (typeName);
			if (isClass)
				classes.Add (typeName);
		}

		void AddAssociatedTypeToCurrentElement (XElement elem)
		{
			var parentElement = GetOrCreateParentElement (kAssociatedTypes);
			parentElement.Add (elem);
		}

		XElement GetOrCreateParentElement (string parentContainerName)
		{
			var current = currentElement.Peek ();
			if (current.Name == kModule) {
				return current;
			}
			var container = GetOrCreate (current, parentContainerName);
			return container;
		}

		OperatorType GetOperatorType (Function_declarationContext context)
		{
			var localOp = LocalOperatorType (context);
			return localOp == OperatorType.None ? GlobalOperatorType (context.function_name ().GetText ())
				: localOp;
		}

		OperatorType LocalOperatorType (Function_declarationContext context)
		{
			var head = context.function_head ();


			// if the function declaration contains prefix 
			if (ModifiersContains (head.declaration_modifiers (), kLittlePrefix)) {
				return OperatorType.Prefix;
			} else if (ModifiersContains (head.declaration_modifiers (), kLittlePostfix)) {
				return OperatorType.Postfix;
			}

			var opName = context.function_name ().GetText ();

			foreach (var op in operators) {
				var targetName = op.Attribute (kName).Value;
				var targetKind = op.Attribute (kOperatorKind).Value;
				if (opName == targetName && targetKind == kInfix)
					return OperatorType.Infix;
			}
			return OperatorType.None;
		}

		OperatorType GlobalOperatorType (string name)
		{
			foreach (var op in typeDatabase.FindOperators (importModules)) {
				if (op.Name == name)
					return op.OperatorType;
			}
			return OperatorType.None;
		}

		void InterpretCommentText (string commentText)
		{
			if (commentText.StartsWith (kSwiftInterfaceFormatVersion)) {
				AssignSwiftInterfaceFormat (commentText.Substring (kSwiftInterfaceFormatVersion.Length));
			} else if (commentText.StartsWith (kSwiftCompilerVersion)) {
				AssignSwiftCompilerVersion (commentText.Substring (kSwiftCompilerVersion.Length));
			} else if (commentText.StartsWith (kSwiftModuleFlags)) {
				ExtractModuleFlags (commentText.Substring (kSwiftModuleFlags.Length));
				moduleFlags.TryGetValue (kModuleName, out moduleName);
			}
		}

		void AssignSwiftInterfaceFormat (string formatVersion)
		{
			// when we get here, we should see something like
			// [white-space]*VERSION[white-space]
			formatVersion = formatVersion.Trim ();
			if (!Version.TryParse (formatVersion, out interfaceVersion))
				throw new ArgumentOutOfRangeException (nameof (formatVersion), $"Expected a version string in the interface format but got {formatVersion}");
		}

		void AssignSwiftCompilerVersion (string compilerVersion)
		{
			// when we get here, we should see something like:
			// [white-space]*Apple? Swift version VERSION (swiftlang-VERSION clang-VERSION)
			var parts = compilerVersion.Trim ().Split (' ', '\t'); // don't know if tab is a thing
									       // expect in the array:
									       // 0: Apple
									       // 1: Swift
									       // 2: verion
									       // 3: VERSION

			var swiftIndex = Array.IndexOf (parts, "Swift");
			if (swiftIndex < 0)
				throw new ArgumentOutOfRangeException (nameof (compilerVersion), $"Expected 'Swift' in the version string, but got {compilerVersion}");
			if (parts [swiftIndex + 1] != "version")
				throw new ArgumentOutOfRangeException (nameof (compilerVersion), $"Expected a compiler version string but got {compilerVersion}");
			var version = parts [swiftIndex + 2];
			if (version.EndsWith ("-dev", StringComparison.Ordinal))
				version = version.Substring (0, version.Length - "-dev".Length);
			if (!Version.TryParse (version, out this.compilerVersion))
				throw new ArgumentOutOfRangeException (nameof (compilerVersion), $"Expected a compiler version number but got {compilerVersion}");
		}

		void ExtractModuleFlags (string commentText)
		{
			var args = commentText.Trim ().Split (' ', '\t');
			int index = 0;
			while (index < args.Length) {
				var arg = args [index++];
				if (arg [0] != '-')
					throw new ArgumentOutOfRangeException (nameof (CommentContext),
						$"Expected argument {index - 1} to start with a '-' but got {arg} (args: {commentText}");
				var key = arg.Substring (1);
				var val = "";
				if (index < args.Length && args [index] [0] != '-') {
					val = args [index++];
				}
				moduleFlags [key] = val;
			}
		}

		void SetLanguageVersion (XElement module)
		{
			if (compilerVersion != null) {
				module.Add (new XAttribute ("swiftVersion", compilerVersion.ToString ()));
			}
		}

		static string XmlBool (bool b)
		{
			return b ? "true" : "false";
		}

		static string ToAccess (Access_level_modifierContext access)
		{
			var accessstr = access != null ? access.GetText () : kInternalCap;
			switch (accessstr) {
			case kPublic:
				return kPublicCap;
			case kPrivate:
				return kPrivateCap;
			case kOpen:
				return kOpenCap;
			case kInternal:
			case kInternalCap:
				return kInternalCap;
			default:
				return kUnknown;
			}
		}


		static XElement GetOrCreate (XElement elem, string key)
		{
			var members = elem.Element (key);
			if (members == null) {
				members = new XElement (key);
				elem.Add (members);
			}
			return members;
		}

		static string [] ctorDtorNames = new string [] {
			kDotCtor, kDotDtor
		};

		static bool IsCtorDtor (string name)
		{
			return ctorDtorNames.Contains (name);
		}

		public static string UnTick (string str)
		{
			// a back-ticked string will start and end with `
			// the swift grammar guarantees this.
			// Identifier :
			// Identifier_head Identifier_characters?
			// | OpBackTick Identifier_head Identifier_characters? OpBackTick
			// | ImplicitParameterName
			// There will be no starting and ending whitespace.
			//
			// There are some edge cases that we can take advantage of:
			// 1. If it starts with `, it *has* to end with back tick, so we don't need to check
			// 2. `` will never exist, so the minimum length *has* to be 3
			// In generalized string manipulation, we couldn't make these assumptions,
			// but in this case the grammar works for us.
			// first weed out the easy cases:
			// null, too short, does start and end with back tick
			// then just substring it
			if (str is null || str.Length < 3 || str [0] != '`')
				return str;
			return str.Substring (1, str.Length - 2);
		}


		public List<String> ImportModules { get { return importModules; } }
	}
}
