// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using SwiftReflector.ExceptionTools;
using System.IO;
using SwiftReflector.IOUtils;
using System.Diagnostics;
using System.Text;
using SwiftReflector.TypeMapping;

namespace SwiftReflector.SwiftXmlReflection {
	public class Reflector {
		public const double kCurrentVersion = 1.0;
		const double kNextMajorVersion = 2.0;

		public static List<ModuleDeclaration> FromXml (XDocument doc, TypeDatabase typeDatabase)
		{
			var xamreflect = doc.Element ("xamreflect");
			var version = xamreflect.DoubleAttribute ("version");

			if (version < kCurrentVersion || version >= kNextMajorVersion) {
				throw ErrorHelper.CreateError (ReflectorError.kReflectionErrorBase + 5, $"Unsupported xamreflect version {version}. Current is {kCurrentVersion}");
			}

			try {
				List<ModuleDeclaration> modules = (from module in xamreflect.Descendants ("module")
								   select ModuleDeclaration.FromXElement (module, typeDatabase)).ToList ();
				return modules;
			} catch (Exception e) {
				throw ErrorHelper.CreateError (ReflectorError.kReflectionErrorBase + 6, $"Error while reading XML reflection information: {e.Message}");
			}
		}

		public static List<ModuleDeclaration> FromXml (Stream stm, TypeDatabase typeDatabase)
		{
			var doc = XDocument.Load (stm);
			return FromXml (doc, typeDatabase);
		}

		public static List<ModuleDeclaration> FromXmlFile (string path, TypeDatabase typeDatabase)
		{
			try {
				using (FileStream stm = new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					return FromXml (stm, typeDatabase);
				}
			} catch (Exception e) {
				throw ErrorHelper.CreateError (ReflectorError.kReflectionErrorBase + 10, e, $"Failed to load xml file '{path}': {e.Message}");
			}
		}

		public static List<ModuleDeclaration> FromXml (string xmlText, TypeDatabase typeDatabase)
		{
			using (StringReader reader = new StringReader (xmlText)) {
				var doc = XDocument.Load (reader);
				return FromXml (doc, typeDatabase);
			}
		}

		public static List<ModuleDeclaration> FromModules (string executablePath, List<string> searchDirectories, List<string> modules,
			IFileProvider<string> provider, string outfileName, out string fullOutputPath)
		{
			fullOutputPath = PathToXmlFromModules (executablePath, searchDirectories, modules, provider, outfileName);
			return FromXmlFile (fullOutputPath, null);
		}

		public static string PathToXmlFromModules (string executablePath, List<string> searchDirectories, List<string> modules,
			IFileProvider<string> provider, string outfileName)
		{
			string fullOutputPath = provider.ProvideFileFor (outfileName);
			try {
				return PathToXmlFromModules (executablePath, searchDirectories, modules, fullOutputPath);
			} finally {
				provider.NotifyFileDone (outfileName, fullOutputPath);
			}
		}

		// returns the path to the output XML file
		public static string PathToXmlFromModules (string executablePath, List<string> searchDirectories, List<string> modules,
			string outfilePath)
		{
			var args = BuildArgs (searchDirectories, modules, outfilePath);
			ExecAndCollect.Run (executablePath, args);
			return outfilePath;
		}

		static string BuildArgs (List<string> searchDirectories, List<string> modules, string outfilePath)
		{
			StringBuilder sb = new StringBuilder ();

			// -xamreflect [-I dir]* -o outfilePath module1 [module2 ...]

			sb.Append ("-xamreflect ");

			foreach (string s in searchDirectories) {
				sb.Append ("-I ");
				sb.Append (QuoteIfNeeded (s));
			}
			if (sb.Length > 0)
				sb.Append (" ");
			sb.Append ("-o ").Append (QuoteIfNeeded (outfilePath));
			sb.Append (" ");
			foreach (string s in modules) {
				sb.Append (QuoteIfNeeded (s));
			}
			return sb.ToString ();
		}

		static string QuoteIfNeeded (string s)
		{
			throw new NotImplementedException ();
		}
	}
}

