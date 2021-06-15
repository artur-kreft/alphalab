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
                OnInitBook = (order) =>
                {
                    Console.WriteLine("init " + order);
                },
                OnBook = (order) =>
                {
                    Console.WriteLine(order);
                },
                OnTrade = (trade) =>
                {
                    Console.WriteLine(trade);
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
