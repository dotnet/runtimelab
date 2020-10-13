using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DllImportGenerator.Tools.Reporting
{
    internal static class Csv
    {
        private const char Separator = ',';

        /// <summary>
        /// Generate a CSV
        /// </summary>
        /// <param name="dump">P/Invoke information</param>
        /// <returns>CSV text</returns>
        public static string Generate(PInvokeDump dump)
        {
            string[] headers = new string[]
            {
                "Assembly Path",
                "Method Name",
                "Return",
                "Arguments",
                nameof(PInvokeMethod.BestFitMapping),
                nameof(PInvokeMethod.CharSet),
                nameof(PInvokeMethod.PreserveSig),
                nameof(PInvokeMethod.SetLastError),
                nameof(PInvokeMethod.ThrowOnUnmappableChar)
            };
            var result = new StringBuilder(string.Join(Separator, headers));
            result.AppendLine();
            foreach ((string assemblyPath, IReadOnlyCollection<PInvokeMethod> importedMethods) in dump.MethodsByAssemblyPath)
            {
                if (importedMethods.Count == 0)
                    continue;

                foreach (var method in importedMethods)
                {
                    var argumentTypes = string.Join(Separator, method.ArgumentTypes.Select(t => t.ToString()));
                    result.AppendLine(string.Join(
                        Separator,
                        $"\"{assemblyPath}\"",
                        $"{method.EnclosingTypeName}{Type.Delimiter}{method.MethodName}",
                        method.ReturnType,
                        $"\"{argumentTypes}\"",
                        method.BestFitMapping,
                        method.CharSet,
                        method.PreserveSig,
                        method.SetLastError,
                        method.ThrowOnUnmappableChar));
                }
            }

            return result.ToString();
        }
    }
}
