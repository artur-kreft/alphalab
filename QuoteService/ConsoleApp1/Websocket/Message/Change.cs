namespace QuoteService.Websocket.Message
{
    public class Change
    {
        public string time { get; set; }
        public string order_id { get; set; }
        public decimal new_size { get; set; }
    }
}