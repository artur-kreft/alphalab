using ServiceStack;

namespace QuoteService.RestApi
{
    [Route("products/{ProductId}/book?level=3")]
    public class OrderBookRequest : IReturn<OrderBookResponse>
    {
        public string ProductId { get; set; }
    }
}