namespace QuoteService.RestApi
{
    public class OrderBookResponse
    {
        public string Sequence { get; set; }
        public string[][] Bids { get; set; }
        public string[][] Asks { get; set; }
    }
}