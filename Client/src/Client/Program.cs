using System;
using System.Net;
using System.Threading.Tasks;

namespace MarketData
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var client = new MarketDataClient
            {
                OnTrade = (trade) =>
                {
                    Console.WriteLine(trade);
                },
                OnBook = (order) =>
                {
                    Console.WriteLine(order);
                },
            };

            client.Connect(IPAddress.Loopback, 8087);
            await client.Listen();

            await client.SubscribeAsync("BTC-USD");
            Console.ReadKey();

            await client.SubscribeAsync("ETH-USD");
            Console.ReadKey();
        }
    }
}
