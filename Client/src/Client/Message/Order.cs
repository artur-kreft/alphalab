using System;
using System.Text;

namespace MarketData.Message
{
    public class Order
    {
        public string Symbol { get; set; }
        public char Side { get; set; }
        public string OrderId { get; set; }
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
            builder.Append(OrderId);
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