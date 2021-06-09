using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using ServiceStack;

namespace QuoteService.RestApi
{
    public class ExchangeRestClient : JsonHttpClient
    {
        private readonly CultureInfo _provider = new CultureInfo("en-US");

        public ExchangeRestClient(string url) : base(url)
        {
            AddHeader("User-Agent", "Artur");
        }

        public async Task<(ReadOnlyMemory<char> sequence, Dictionary<string, Order> book)> GetOrderBook(string symbol)
        {
            var book = await GetAsync(new OrderBookRequest { ProductId = symbol });
            var result = new Dictionary<string, Order>(book.Asks.Length + book.Bids.Length);
            
            FillOrders(result, book.Asks, 's');
            FillOrders(result, book.Bids, 'b');

            return (book.Sequence.AsMemory(), result);
        }

        private void FillOrders(Dictionary<string, Order> result, string[][] orders, char side)
        {
            for (int i = 0; i < orders.Length; ++i)
            {
                result.Add(orders[i][2], new Order()
                {
                    Price = decimal.Parse(orders[i][0], NumberStyles.AllowDecimalPoint, _provider),
                    Size = decimal.Parse(orders[i][1], NumberStyles.AllowDecimalPoint, _provider),
                    Side = side
                });
            }
        }
    }
}