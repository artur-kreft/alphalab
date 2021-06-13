namespace QuoteService
{
    public class Order
    {
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public char Side { get; set; }
        public string Time { get; set; }
    }
}