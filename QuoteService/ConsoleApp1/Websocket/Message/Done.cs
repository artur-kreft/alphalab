namespace QuoteService.Websocket.Message
{
    public class Done
    {
        public string time { get; set; }
        public string order_id { get; set; }
    }
}