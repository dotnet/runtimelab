using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Net.Http.LowLevel.Tests
{
    public class EnhancedNetworkStreamTests : TestsBase
    {
        //private static readonly byte[] s_TestPattern = Encoding.ASCII.GetBytes("foo");

        //[Theory]
        //public async Task ReadScattered_Success()
        //{
        //}

        //[Theory]
        //public async Task WriteGathered_Success()
        //{
        //}

        public EnhancedNetworkStreamTests()
        {
            DefaultTestTimeout = 5000;
        }

        private async Task RunSocketClientServer(Func<Socket, Task> clientFunc, Func<Socket, Task> serverFunc, int? millisecondsTimeout = null)
        {
            using var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listenSocket.Listen();
            EndPoint listenEndPoint = listenSocket.LocalEndPoint!;

            using var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            cts.CancelAfter(millisecondsTimeout ?? DefaultTestTimeout);

            using var _ = token.UnsafeRegister(o => ((Socket)o!).Dispose(), listenSocket);

            await RunClientServer(async () =>
            {
                using Socket clientSocket = new Socket(listenSocket.AddressFamily, listenSocket.SocketType, listenSocket.ProtocolType);
                await clientSocket.ConnectAsync(listenSocket.LocalEndPoint!, token).ConfigureAwait(false);
                await clientFunc(clientSocket).ConfigureAwait(false);
            },
            async () =>
            {
                using Socket acceptSocket = await listenSocket.AcceptAsync().ConfigureAwait(false);
                await serverFunc(acceptSocket).ConfigureAwait(false);
            }, millisecondsTimeout + 11000).ConfigureAwait(false);
        }
    }
}
