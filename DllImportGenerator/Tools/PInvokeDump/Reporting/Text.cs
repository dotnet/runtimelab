using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DllImportGenerator.Tools.Reporting
{
    internal static class Text
    {
        public static string Generate(PInvokeDump dump)
        {
            var result = new StringBuilder();
            foreach ((string assemblyPath, IReadOnlyCollection<PInvokeMethod> importedMethods) in dump.MethodsByAssemblyPath)
            {
                result.AppendLine($"{assemblyPath} - total: {importedMethods.Count}");
                if (importedMethods.Count == 0)
                    continue;

                foreach (var method in importedMethods)
                {
                    result.AppendLine($"  {method.EnclosingTypeName}{Type.Delimiter}{method.MethodName}");
                    result.AppendLine($"    {method.ReturnType} ({string.Join(", ", method.ArgumentTypes.Select(t => t.ToString()))})");
                    result.AppendLine("    Attributes:");
                    result.AppendLine(string.Join(Environment.NewLine,
                        $"      {nameof(method.BestFitMapping)}={method.BestFitMapping}",
                        $"      {nameof(method.CharSet)}={method.CharSet}",
                        $"      {nameof(method.PreserveSig)}={method.PreserveSig}",
                        $"      {nameof(method.SetLastError)}={method.SetLastError}",
                        $"      {nameof(method.ThrowOnUnmappableChar)}={method.ThrowOnUnmappableChar}"));
                }
            }

            result.AppendLine();
            result.AppendLine("All types:");
            foreach (string type in dump.AllTypeNames.OrderBy(t => t))
                result.AppendLine($"  {type}");

            return result.ToString();
        }
    }
}
