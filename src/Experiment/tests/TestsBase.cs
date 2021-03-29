
using System.Diagnostics;
using System.Threading.Tasks;

namespace System.Net.Http.LowLevel.Tests
{
    public class TestsBase
    {
        public int DefaultTestTimeout = 500; // in milliseconds.

        public async Task RunClientServer(Func<Task> clientFunc, Func<Task> serverFunc, int? millisecondsTimeout = null)
        {
            Task[] tasks = new[]
            {
                Task.Run(() => clientFunc()),
                Task.Run(() => serverFunc())
            };

            if (Debugger.IsAttached)
            {
                await tasks.WhenAllOrAnyFailed().ConfigureAwait(false);
            }
            else
            {
                await tasks.WhenAllOrAnyFailed(millisecondsTimeout ?? DefaultTestTimeout).ConfigureAwait(false);
            }
        }
    }
}
