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
            var result = new StringBuilder($"Assembly Path{Separator}Method Name{Separator}Return{Separator}Arguments{Environment.NewLine}");
            foreach ((string assemblyPath, IReadOnlyCollection<PInvokeMethod> importedMethods) in dump.MethodsByAssemblyPath)
            {
                if (importedMethods.Count == 0)
                    continue;

                foreach (var method in importedMethods)
                {
                    var argumentTypes = string.Join(Separator, method.ArgumentTypes.Select(t => t.ToString()));
                    result.AppendLine($"\"{assemblyPath}\"{Separator}{method.EnclosingTypeName}{Type.Delimiter}{method.MethodName}{Separator}{method.ReturnType}{Separator}\"{argumentTypes}\"");
                }
            }

            return result.ToString();
        }
    }
}
