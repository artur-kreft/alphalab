using System.Net;
using System.Threading;
using System.Threading.Tasks;
using QuoteService.Tools;

#pragma warning disable 4014

namespace QuoteService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            using var host = new Host
            {
                OnSubscribe = (symbol, stream) =>
                {
                    Log.Info($"{stream.RemoteEndPoint} subscribed to {symbol}");
                }
            };

            await host.Listen(IPAddress.Loopback, 8087, cts.Token);
        }
    }
}
