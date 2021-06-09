namespace QuoteService.Websocket.Message
{
    public class Open
    {
        public string time { get; set; }
        public string order_id { get; set; }
        public decimal price { get; set; }
        public decimal remaining_size { get; set; }
        public string side { get; set; }
    }
}