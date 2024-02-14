// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SwiftReflector.SwiftXmlReflection;
using SwiftReflector.ExceptionTools;
using SwiftRuntimeLibrary;

namespace SwiftReflector.TypeMapping {
	public class DotNetName
	{
		public readonly string Namespace;
		public readonly string TypeName;

		public DotNetName (string @namespace, string typeName)
		{
			Namespace = @namespace;
			TypeName = typeName;
		}

		public override string ToString ()
		{
			return $"{Namespace}.{TypeName}";
		}

		public override bool Equals (object obj)
		{
			var other = obj as DotNetName;
			return other != null && Namespace == other.Namespace && TypeName == other.TypeName;
		}

		public override int GetHashCode ()
		{
			return Namespace.GetHashCode () ^ TypeName.GetHashCode ();
		}
	}

	public class TypeDatabase {
		const double kCurrentVersion = 1.0;
		const double kMinVersion = 1.0;
		Dictionary<DotNetName, string> netNamesToSwiftNames = new Dictionary<DotNetName, string> ();
		Dictionary<string, DotNetName> swiftNamesToNetNames = new Dictionary<string, DotNetName> ();

		Dictionary<string, ModuleDatabase> modules = new Dictionary<string, ModuleDatabase> ();

		public TypeDatabase ()
		{
		}


		public DotNetName DotNetNameForSwiftName (string swiftName)
		{
			DotNetName netName = null;
			swiftNamesToNetNames.TryGetValue (Exceptions.ThrowOnNull (swiftName, "swiftName"), out netName);
			return netName; // may be null
		}

		public DotNetName DotNetNameForSwiftName (SwiftClassName swiftName)
		{
			return DotNetNameForSwiftName (Exceptions.ThrowOnNull (swiftName, "swiftName").ToFullyQualifiedName (true));
		}

		public string SwiftNameForDotNetName (DotNetName netName)
		{
			string swiftName = null;
			netNamesToSwiftNames.TryGetValue (Exceptions.ThrowOnNull (netName, "netName"), out swiftName);
			return swiftName;
		}

		public Entity EntityForDotNetName (DotNetName netClassName)
		{
			return EntityForSwiftName (netNamesToSwiftNames [netClassName]);
		}

		public Entity EntityForSwiftName (SwiftClassName swiftName)
		{
			return EntityForSwiftName (swiftName.ToFullyQualifiedName (true));
		}

		public Entity EntityForSwiftName (string swiftName)
		{
			var modName = swiftName.ModuleFromName ();
			if (modName == null)
				throw ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 1, $"Swift name '{swiftName}' needs to have a module.");

			var module = EntityCollection (modName);
			if (module == null)
				return null;

			Entity e = null;
			module.TryGetValue (swiftName, out e);
			return e;
		}

		public Entity TryGetEntityForSwiftName (string swiftName)
		{
			try {
				return EntityForSwiftName (swiftName);
			}
			catch {
				return null;
			}
		}

		public int Count {
			get {
				return modules.Select (m => m.Value.Count).Sum ();
			}
		}

		public bool Contains (string swiftClassName)
		{
			return EntityForSwiftName (swiftClassName) != null;
		}

		public bool Contains (SwiftClassName swiftClassName)
		{
			return Contains (swiftClassName.ToFullyQualifiedName (true));
		}

		public bool Contains (DotNetName name)
		{
			return netNamesToSwiftNames.ContainsKey (name);
		}

		public IEnumerable<string> ModuleNames { get { return modules.Keys; } }

		public void Update (Entity e)
		{
			var old = EntityForSwiftName (e.Type.ToFullyQualifiedName (true));
			if (old == null) {
				Add (e);
			} else {
				old.EntityType = e.EntityType;
				old.SharpNamespace = e.SharpNamespace;
				old.SharpTypeName = e.SharpTypeName;
				old.Type = e.Type;
			}
		}

		public void Add (Entity e)
		{
			if (e.Type != null && !e.Type.IsUnrooted)
				throw new ArgumentException ("Entity.Type needs to be unrooted.", "e");
			var errors = new ErrorHandling ();
			AddEntity (e, errors);
			if (errors.AnyErrors)
				throw new AggregateException (errors.Errors.Select ((v) => v.Exception));
		}

		ModuleDatabase EntityCollection (string moduleName)
		{
			ModuleDatabase module = null;
			modules.TryGetValue (Exceptions.ThrowOnNull (moduleName, nameof(moduleName)), out module);
			return module;
		}

		public IEnumerable<Entity> EntitiesForModule (string moduleName)
		{
			var module = EntityCollection (moduleName);
			if (module != null)
				return module.Values;
			return Enumerable.Empty<Entity> ();
		}

		public IEnumerable <OperatorDeclaration> OperatorsForModule (string moduleName)
		{
			var module = EntityCollection (moduleName);
			if (module != null)
				return module.Operators;
			return Enumerable.Empty<OperatorDeclaration> ();
		}

		public IEnumerable<TypeAliasDeclaration> TypeAliasesForModule (string moduleName)
		{
			var module = EntityCollection (moduleName);
			if (module != null)
				return module.TypeAliases;
			return Enumerable.Empty<TypeAliasDeclaration> ();
		}

		public void Write (string file, IEnumerable<string> modules)
		{
			using (FileStream stm = new FileStream (Exceptions.ThrowOnNull (file, nameof(file)), FileMode.Create)) {
				Write (stm, modules);
			}
		}

		public void Write (Stream stm, IEnumerable<string> modules)
		{
			Write (stm, modules.Select (s => EntitiesForModule (s)).SelectMany (x => x).Select (entity => entity.ToXElement ()));
		}

		IEnumerable<XElement> GatherXElements (IEnumerable<string> modules)
		{
			foreach (var modName in modules) {
				foreach (var entity in EntitiesForModule (modName)) {
					yield return entity.ToXElement ();
				}
				foreach (var op in OperatorsForModule (modName)) {
					yield return op.ToXElement ();
				}
				foreach (var alias in TypeAliasesForModule (modName)) {
					yield return alias.ToXElement ();
				}
			}
		}

		public void Write (string file, string module)
		{
			using (FileStream stm = new FileStream (Exceptions.ThrowOnNull (file, "file"), FileMode.Create)) {
				Write (stm, module);
			}
		}

		public void Write (Stream stm, string module)
		{
			Write (stm, EntitiesForModule (module).Select (entity => entity.ToXElement ()));
		}

		void Write (Stream stm, IEnumerable<XElement> entities)
		{
			var entityList = new XElement ("entities", entities);
			var db = new XElement ("xamtypedatabase",
			                       new XAttribute ("version", 1.0),
			                       entityList);
			var doc = new XDocument (
				new XDeclaration ("1.0", "utf-8", "yes"),
				db);
			doc.Save (stm);
		}

		public bool Merge (TypeDatabase other, ErrorHandling errors)
		{
			var initialErrorCount = errors.ErrorCount;
			foreach (var mod in other.modules.Values) {
				foreach (var entity in mod.Values) {
					AddEntity (entity, errors);
				}
				foreach (var op in mod.Operators) {
					AddOperator (op);
				}
			}
			return initialErrorCount != errors.ErrorCount; 
		}


		public ErrorHandling Read (List<string> files)
		{
			var errors = new ErrorHandling();
			foreach (string file in files) {
				errors.Add (Read (file));
			}
			return errors;
		}

		public ErrorHandling Read (string file)
		{
			var errors = new ErrorHandling ();
			Stream stm = null;
			try {
				stm = new FileStream (file, FileMode.Open, FileAccess.Read, FileShare.Read);
				errors.Add (Read (stm));
			} catch (Exception e) {
				errors.Add (ErrorHelper.CreateError (ReflectorError.kCantHappenBase + 59, e, "Failed opening file {0}: {1}", file, e.Message));
			} finally {
				if (stm != null)
					stm.Close ();
			}
			return errors;
		}

		public ErrorHandling Read (Stream stm)
		{
			var errors = new ErrorHandling ();
			try {
				var doc = XDocument.Load (stm);
				var xamtypedatabase = doc.Element ("xamtypedatabase");
				if (xamtypedatabase == null)
					throw ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 2, "Type database is missing required key 'xamtypedatabase'");
				var version = xamtypedatabase.DoubleAttribute ("version", -1.0);
				if (version < kMinVersion)
					if (version > kCurrentVersion)
						throw ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 3, $"Type database version, {version}, is greater than the current version, {kCurrentVersion}.");
				ReadVersion1_0 (xamtypedatabase, errors);
			} catch (Exception e) {
				errors.Add (ErrorHelper.CreateError (ReflectorError.kCantHappenBase + 60, e, "Failure reading type definition XML: " + e.Message));
			}
			return errors;
		}

		void ReadVersion1_0 (XElement typeDbRoot, ErrorHandling errors)
		{
			var localModules = new Dictionary<string, ModuleDeclaration> ();

			var entityLists = typeDbRoot.Descendants ("entities");

			foreach (var entityList in entityLists) {
				var entities = from entity in entityList.Elements ("entity")
					       select EntityFromXElement (entity, localModules, errors);

				foreach (Entity e in entities) {
					if (e == null)
						continue;
					AddEntity (e, errors);
				}

				var ops = from op in entityList.Elements ("operator")
					  select OperatorDeclaration.FromXElement (op, null);

				foreach (var op in ops) {
					AddOperator (op);
				}

				var aliases = from alias in entityList.Elements ("typealias")
					      select TypeAliasDeclaration.FromXElement (null, alias);

				foreach (var alias in aliases) {
					AddTypeAlias (alias);
				}
			}
		}

		public ModuleDatabase ModuleDatabaseForModuleName (string moduleName)
		{
			ModuleDatabase db;
			if (!modules.TryGetValue (moduleName, out db)) {
				db = new ModuleDatabase ();
				modules.Add (moduleName, db);
			}
			return db;
		}

		public void AddOperator (OperatorDeclaration op, string moduleName = null)
		{
			moduleName = moduleName ?? op.ModuleName;
			ModuleDatabase db = ModuleDatabaseForModuleName (moduleName);
			db.Operators.Add (op);
		}

		public IEnumerable <OperatorDeclaration> FindOperators (IEnumerable <string> moduleNames)
		{
			foreach (var moduleName in moduleNames) {
				ModuleDatabase db = null;
				if (!modules.TryGetValue (moduleName, out db))
					continue;
				foreach (var op in db.Operators)
					yield return op;
			}
			yield break;
		}

		public void AddTypeAlias (TypeAliasDeclaration alias, string moduleName = null)
		{
			moduleName = moduleName ?? alias.ModuleName;
			ModuleDatabase db = ModuleDatabaseForModuleName (moduleName);
			db.TypeAliases.Add (alias);
		}

		void AddEntity (Entity e, ErrorHandling errors)
		{
			var swiftName = e.Type.ToFullyQualifiedName (true);
			var sharpName = e.GetFullType ();
			if (netNamesToSwiftNames.ContainsKey (sharpName)) {
				errors.Add (new ReflectorError (ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 4, $"Already have a C# definition for {e.EntityType} {sharpName}")));
				return;
			}
			if (swiftNamesToNetNames.ContainsKey (swiftName)) {
				errors.Add (new ReflectorError (ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 5, $"Already have a Swift definition for {e.EntityType} {swiftName}")));
				return;
			}

			netNamesToSwiftNames.Add (sharpName, swiftName);
			swiftNamesToNetNames.Add (swiftName, sharpName);

			var moduleName = e.Type.Module.Name;
			AddEntityToModuleCollection (moduleName, e);
		}

		void AddEntityToModuleCollection (string moduleName, Entity e)
		{
			ModuleDatabase entities = null;
			if (!modules.TryGetValue (moduleName, out entities)) {
				entities = new ModuleDatabase ();
				modules.Add (moduleName, entities);
			}
			entities.Add (e.Type.ToFullyQualifiedName (true), e);
		}

		Entity EntityFromXElement (XElement entityElem, Dictionary<string, ModuleDeclaration> theModules, ErrorHandling errors)
		{
			var typeDeclElement = entityElem.Element ("typedeclaration");
			if (typeDeclElement == null) {
				errors.Add (new ReflectorError (ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 6, "Entity elements must contain a child element.")));
				return null;
			}
			var moduleName = (string)typeDeclElement.Attribute ("module");
			if (string.IsNullOrEmpty (moduleName)) {
				errors.Add (new ReflectorError (ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 7, "Entity elements must contain a module name.")));
				return null;
			}
			var module = MakeModuleForName (moduleName, theModules);
			var folder = new TypeAliasFolder (module.TypeAliases);
			var decl = TypeDeclaration.FromXElement (folder, typeDeclElement, module, null) as TypeDeclaration;
			if (decl == null) {
				errors.Add (new ReflectorError (ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 8, "Incorrect type declaration in entity.")));
				return null;
			}
			var e = new Entity {
				SharpNamespace = (string)entityElem.Attribute ("sharpNameSpace"),
				SharpTypeName = (string)entityElem.Attribute ("sharpTypeName"),
				EntityType = ToEntityType ((string)entityElem.Attribute ("entityType")),
				ProtocolProxyModule = (string)entityElem.Attribute ("protocolProxyModule"),
				IsDiscretionaryConstraint = IsDiscetionaryConstraint (entityElem),
				Type = decl
			};
			if (e.SharpNamespace == null) {
				errors.Add (new ReflectorError (ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 12, "Missing sharpNameSpace in entity.")));
				return null;
			}
			if (e.SharpTypeName == null) {
				errors.Add (new ReflectorError (ErrorHelper.CreateError (ReflectorError.kTypeMapBase + 13, "Missing sharpTypeName in entity.")));
				return null;
			}
			return e;
		}

		static EntityType ToEntityType (string s)
		{
			switch (Exceptions.ThrowOnNull (s, "s").ToLower ()) {
			case "scalar":
				return EntityType.Scalar;
			case "class":
				return EntityType.Class;
			case "struct":
				return EntityType.Struct;
			case "enum":
				return EntityType.Enum;
			case "trivialenum":
				return EntityType.TrivialEnum;
			case "protocol":
				return EntityType.Protocol;
			default:
			case "none":
				return EntityType.None;
			}
		}

		static bool IsDiscetionaryConstraint (XElement entityElem)
		{
			var discretionary = (string)entityElem.Attribute ("discretionaryConstraint") ?? "false";
			return discretionary.ToLower () == "true";
		}

		ModuleDeclaration MakeModuleForName (string name, Dictionary<string, ModuleDeclaration> modules)
		{
			ModuleDeclaration module = null;
			if (!modules.TryGetValue (name, out module)) {
				module = new ModuleDeclaration ();
				module.Name = name;
				modules.Add (name, module);
			}
			return module;
		}
	}
}

