using QuoteService.Websocket.Message;

namespace QuoteService.Websocket
{
    public class Request
    {
        public string type { get; set; }
        public Channel[] channels { get; set; }
    }
}