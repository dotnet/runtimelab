using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmarks
{
    /// <summary>
    /// Filter benchmarks to only benchmarks that work with the built-in and source-generated stubs.
    /// </summary>
    class BuiltinCompareFilter : IFilter
    {
        private AnyCategoriesFilter includeFilter = new AnyCategoriesFilter(new[] { CategoryNames.DllImportGeneratorOnly });

        public bool Predicate(BenchmarkCase benchmarkCase)
        {
            return !includeFilter.Predicate(benchmarkCase);
        }
    }
}
