// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin;
using System.Text;
using SyntaxDynamo;
using System.Collections;

namespace SwiftReflector {
	public static class Extensions {
		public static IEnumerable<T> Yield<T> (this T elem)
		{
			yield return elem;
		}

		public static bool IsSwiftEntryPoint (this NListEntry entry)
		{
			return !String.IsNullOrEmpty (entry.str) && (entry.str.StartsWith ("__T", StringComparison.Ordinal) ||
				entry.str.StartsWith ("_$s", StringComparison.Ordinal) || entry.str.StartsWith ("_$S", StringComparison.Ordinal));
		}

		public static IEnumerable<NListEntry> SwiftEntryPoints (this IEnumerable<NListEntry> entries)
		{
			return entries.Where (IsSwiftEntryPoint);
		}

		public static IEnumerable<string> SwiftEntryPointNames (this IEnumerable<NListEntry> entries)
		{
			return entries.SwiftEntryPoints ().Select (nle => nle.str);
		}

		public static string DePunyCode (this string s)
		{
			return PunyCode.PunySingleton.Decode (s);
		}

		public static Tuple<string, string> SplitModuleFromName (this string s)
		{
			int dotIndex = s.IndexOf ('.');
			if (dotIndex < 0)
				return new Tuple<string, string> (null, s);
			if (dotIndex == 0)
				return new Tuple<string, string> (null, s.Substring (1));
			return new Tuple<string, string> (s.Substring (0, dotIndex), s.Substring (dotIndex + 1));
		}

		public static string ModuleFromName (this string s)
		{
			return s.SplitModuleFromName ().Item1;
		}

		public static string NameWithoutModule (this string s)
		{
			return s.SplitModuleFromName ().Item2;
		}

		public static string [] DecomposeClangTarget (this string s)
		{
			if (String.IsNullOrEmpty (s))
				throw new ArgumentNullException (nameof (s));
			string [] parts = s.Split ('-');
			// catalyst adds cpu-platform-ios-macabi
			if (parts.Length != 3 && parts.Length != 4)
				throw new ArgumentOutOfRangeException (nameof (s), s, "target should be in the form cpu-platform-os");

			var shortestIndex = parts.Length == 3 ?
				IndexOfMin (parts [0].Length, parts [1].Length, parts [2].Length) :
				IndexOfMin (parts [0].Length, parts [1].Length, parts [2].Length, parts [3].Length);

			if (parts [shortestIndex].Length == 0) {
				var missingPart = new string [] { "cpu", "platform", "os", "os" } [shortestIndex];
				throw new ArgumentException ($"target (cpu-platform-os) has an empty {missingPart} component.");
			}
																			
			return parts;
		}

		static int IndexOfMin (params int [] values)
		{
			var min = values.Min ();
			return Array.IndexOf (values, min);
		}

		public static string ClangTargetCpu (this string s)
		{
			return s.DecomposeClangTarget () [0];
		}

		public static string ClangTargetPlatform (this string s)
		{
			return s.DecomposeClangTarget () [1];
		}

		public static string ClangTargetOS (this string s)
		{
			var clangTarget = s.DecomposeClangTarget ();
			if (clangTarget.Length == 3)
				return clangTarget [2];
			if (clangTarget.Length == 4)
				return $"{clangTarget [2]}-{clangTarget [3]}";
			throw new ArgumentException ($"Clang target {s} should have 3 or 4 parts", nameof (s));
		}

		static int IndexOfFirstDigit (string s)
		{
			int index = 0;
			foreach (char c in s) {
				if (Char.IsDigit (c))
					return index;
				index++;
			}
			return -1;
		}

		public static string ClangOSNoVersion (this string s)
		{
			var clangTarget = s.DecomposeClangTarget ();
			var os = clangTarget [2];
			return OSNoVersion (os);
		}

		static string OSNoVersion (string s)
		{
			var firstNumber = IndexOfFirstDigit (s);
			return firstNumber < 0 ? s : s.Substring (0, firstNumber);
		}

		public static string ClangOSVersion (this string s)
		{
			var clangTarget = s.DecomposeClangTarget ();
			var os = clangTarget [2];
			var firstNumber = IndexOfFirstDigit (os);
			return os.Substring (firstNumber);
		}

		public static string MinimumClangVersion (IEnumerable<string> targets)
		{
			var osVersion = targets.Select (t => new Version (ClangOSVersion (t))).Min ();
			return osVersion.ToString ();
		}

		public static string ClangSubstituteOSVersion (this string s, string replacementVersion)
		{
			var clangTarget = s.DecomposeClangTarget ();
			var os = OSNoVersion (clangTarget [2]) + replacementVersion;
			clangTarget [2] = os;
			return clangTarget.InterleaveStrings ("-");
		}

		public static bool ClangTargetIsSimulator (this string s)
		{
			var parts = s.DecomposeClangTarget ();
			if (parts.Length == 4 && parts [3] == "simulator")
				return true;
			var osNoVersion = OSNoVersion (parts [2]);
			if (osNoVersion == "macosx")
				return false;
			var cpu = parts [0];
			return cpu == "i386" || cpu == "x86-64";
		}

		public static void Merge<T> (this HashSet<T> to, IEnumerable<T> from)
		{
			Exceptions.ThrowOnNull (from, nameof (from));
			foreach (T val in from)
				to.Add (val);
		}

		public static void DisposeAll<T> (this IEnumerable<T> coll) where T : IDisposable
		{
			Exceptions.ThrowOnNull (coll, nameof (coll));
			foreach (T obj in coll) {
				if ((IDisposable)obj != null)
					obj.Dispose ();
			}
		}

		public static string InterleaveStrings (this IEnumerable<string> elements, string separator, bool includeSepFirst = false)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (string s in elements.Interleave (separator, includeSepFirst))
				sb.Append (s);
			return sb.ToString ();
		}

		public static string InterleaveCommas (this IEnumerable<string> elements)
		{
			return elements.InterleaveStrings (", ");
		}

		public static bool IsSwift3(this Version vers)
		{
			return vers.Major == 3;
		}

		public static bool IsSwift4 (this Version vers)
		{
			return vers.Major == 4;
		}

		public static int ErrorCount(this List<ReflectorError> errors)
		{
			var count = 0;
			foreach (var error in errors) {
				if (!error.IsWarning)
					++count;
			}
			return count;
		}

		public static int WarningCount(this List<ReflectorError> errors)
		{
			return errors.Count - errors.ErrorCount ();
		}

		public static List<T> CloneAndPrepend<T>(this List<T> source, T item)
		{
			var result = new List<T> (source.Count + 1);
			result.Add (item);
			result.AddRange (source);
			return result;
		}

		public static T[] And<T> (this T[] first, T[] second)
		{
			Exceptions.ThrowOnNull (first, nameof (first));
			Exceptions.ThrowOnNull (second, nameof (second));
			var result = new T [first.Length + second.Length];
			Array.Copy (first, result, first.Length);
			Array.Copy (second, 0, result, first.Length, second.Length);
			return result;
		}
	}
}

