using System;
using System.Net;
using System.Threading.Tasks;
using System.IO;

namespace MarketData
{
    class Program
    {
        static string path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.Parent.FullName;

        static async Task Main(string[] args)
        {
            var outputFile = $"{path}\\output.txt";

            if (!File.Exists(outputFile))
            {
                using (StreamWriter creator = File.CreateText(outputFile))
                {
                }
            }

            using StreamWriter sw = File.AppendText(path);

            using var client = new MarketDataClient
            {
                OnInitBook = (order) =>
                {
                    sw.WriteLine("init " + order);
                },
                OnBook = (order) =>
                {
                    sw.WriteLine(order);
                },
                OnTrade = (trade) =>
                {
                    sw.WriteLine(trade);
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
