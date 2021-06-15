using System;
using System.Text;

namespace MarketData.Message
{
    public class Trade
    {
        public string Symbol { get; set; }
        public char Side { get; set; }
        public string TradeId { get; set; }
        public string MakerOrderId { get; set; }
        public string TakerOrderId { get; set; }
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public DateTime Time { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Symbol);
            builder.Append(' ');
            builder.Append(Side);
            builder.Append(' ');
            builder.Append(TradeId);
            builder.Append(' ');
            builder.Append(MakerOrderId);
            builder.Append(' ');
            builder.Append(TakerOrderId);
            builder.Append(' ');
            builder.Append(Price);
            builder.Append(' ');
            builder.Append(Size);
            builder.Append(' ');
            builder.Append(Time);
            return builder.ToString();
        }
    }
}