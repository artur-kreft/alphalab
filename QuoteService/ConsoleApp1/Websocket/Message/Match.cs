namespace QuoteService.Websocket.Message
{
    public class Match
    {
        public string time { get; set; }
        public int trade_id { get; set; }
        public string maker_order_id { get; set; }
        public string taker_order_id { get; set; }
        public decimal price { get; set; }
        public decimal size { get; set; }
    }
}