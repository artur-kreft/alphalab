using System;
using System.Net;
using System.Threading.Tasks;
using System.IO;

namespace MarketData
{
    class Program
    {
        static string path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.FullName;

        static async Task Main(string[] args)
        {
            string outputFile = "";

            int i = 0;
            while(true)
            {
                outputFile = $"{path}\\output_test\\output_{i}.txt";
                if (!File.Exists(outputFile))
                {
                    using (StreamWriter creator = File.CreateText(outputFile))
                    {
                    }
                    break;
                } 
                else
                {
                    ++i;
                }
            }            

            using StreamWriter sw = File.AppendText(outputFile);

            using var client = new MarketDataClient
            {
                OnInitBook = (order) =>
                {
                    Console.WriteLine("init " + order);
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
        }
    }
}
