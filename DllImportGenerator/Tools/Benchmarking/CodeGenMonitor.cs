using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Benchmarking.Visualizer;

namespace Benchmarking
{
    /// <summary>
    /// Data about the method and its generated code.
    /// </summary>
    public record MethodCodeGen
    {
        public uint MetadataToken { get; init; }
        public bool IsILStub { get; init; }
        public string FullyQualifiedClassName { get; init; }
        public string MethodName { get; init; }
        public string GeneratedCode { get; init; }
        public uint GeneratedCodeSize { get; init; }
        public string ILCode { get; init; }
    }

    /// <summary>
    /// Class used to monitor code generation in the CLR
    /// </summary>
    public sealed class CodeGenMonitor : IDisposable
    {
        public const int MetadataTokenNil = 0;

        private readonly CodeGenEventListener listener;
        private readonly int retryDelayInMs;
        private readonly HtmlCompareTwoPane htmlCompare;

        private bool isDisposed = false;

        private Dictionary<string, List<MethodCodeGen>> methods = new Dictionary<string, List<MethodCodeGen>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initialize a <see cref="CodeGenMonitor"/> instance.
        /// </summary>
        /// <param name="retryDelayInMs">Time delay (ms) to use when requesting a function name that isn't immediately known</param>
        public CodeGenMonitor(int retryDelayInMs = 20)
        {
            this.listener = new CodeGenEventListener();
            this.listener.NewMethodCodeGen += NewMethodCodeGen;
            this.retryDelayInMs = retryDelayInMs;

            this.htmlCompare = new HtmlCompareTwoPane()
            {
                Title = "Compare generated code",
                Pane1Title = "Code Gen 1",
                Pane2Title = "Code Gen 2",
                SelectionTitle = "All methods",
            };
        }

        /// <summary>
        /// Asynchronously get the generated code for the supplied method.
        /// </summary>
        /// <param name="fullyQualifiedMethodName">Fully qualified method name</param>
        /// <param name="metadataToken">[Optional] method's metadata token - used to clarify overloads.</param>
        /// <param name="retryCount">Number of times to retry if method isn't immediately found</param>
        /// <returns>A <see cref="MethodCodeGen"/> instance</returns>
        public async Task<MethodCodeGen> GetLastCodeGenForAsync(string fullyQualifiedMethodName, int metadataToken = MetadataTokenNil, int retryCount = 5)
        {
            for (int i = 0; i < retryCount; ++i)
            {
                lock (this.methods)
                {
                    if (this.methods.TryGetValue(fullyQualifiedMethodName, out List<MethodCodeGen> mcgs))
                    {
                        var ret = mcgs.Last();
                        if (metadataToken != MetadataTokenNil)
                        {
                            // Return the last matching element in the collection
                            var lastMaybe = mcgs.FindLast((m) => m.MetadataToken == (uint)metadataToken);
                            if (lastMaybe is not null)
                            {
                                ret = lastMaybe;
                            }
                        }

                        return ret;
                    }
                }

                await Task.Delay(this.retryDelayInMs);
            }

            return null;
        }

        /// <summary>
        /// Generate a static HTML page to compare all collected data.
        /// </summary>
        /// <param name="pattern">Regular expression to filter through all collected data.</param>
        /// <param name="waitForMonitorInMs">Time delay to wait prior to accessing current state</param>
        /// <returns>Static HTML code to view in browser</returns>
        public string GenerateHtml(string pattern = "", int waitForMonitorInMs = 2_000)
        {
            if (waitForMonitorInMs > 0)
            {
                Thread.Sleep(waitForMonitorInMs);
            }

            lock (this.methods)
            {
                return this.htmlCompare.Generate(this.FilterTo_Unsafe(pattern));
            }
        }

        private IEnumerable<(string Name, string Content)> FilterTo_Unsafe(string pattern)
        {
            var regEx = new Regex(pattern);
            foreach (var mcgs in this.methods)
            {
                if (!string.IsNullOrEmpty(pattern)
                    && !regEx.IsMatch(mcgs.Key))
                {
                    continue;
                }

                if (mcgs.Value.Count == 1)
                {
                    var mcg = mcgs.Value.Last();
                    yield return new(mcgs.Key, mcg.GeneratedCode + Environment.NewLine + mcg.ILCode);
                }
                else
                {
                    int i = 1;
                    foreach (var mcg in mcgs.Value)
                    {
                        yield return new(mcgs.Key + $" ({i})", mcg.GeneratedCode + Environment.NewLine + mcg.ILCode);
                        ++i;
                    }
                }
            }
        }

        private void NewMethodCodeGen(object sender, MethodCodeGen e)
        {
            lock (this.methods)
            {
                string key = e.FullyQualifiedClassName + Type.Delimiter + e.MethodName;
                if (!methods.TryGetValue(key, out List<MethodCodeGen> inst))
                {
                    methods.Add(key, new List<MethodCodeGen>() { e });
                }
                else
                {
                    inst.Add(e);
                }
            }
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.listener.NewMethodCodeGen -= NewMethodCodeGen;
            this.listener.Dispose();

            this.isDisposed = true;
        }
    }
}
