using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConsoleApp1;
using MarketData.Message;

namespace MarketData
{
    public class MarketDataClient : IDisposable
    {
        public Action<Order> OnInitBook { private get; set; }
        public Action<Order> OnBook { private get; set; }
        public Action<Trade> OnTrade { private get; set; }

        private readonly Socket _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        private NetworkStream _stream;
        private CancellationTokenSource _cts;

        public bool Connect(IPAddress address, int port)
        {
            _socket.NoDelay = true;

            Console.WriteLine($"Connecting to {address}:{port}");
            try
            {
                _socket.Connect(new IPEndPoint(address, port));
                _stream = new NetworkStream(_socket);
                _cts = new CancellationTokenSource();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            return true;
        }

        public Task Listen()
        {
            if (_stream == null)
            {
                Log.Error("Not connected");
                return default;
            }

            return Task.Factory.StartNew(async () =>
            {
                var reader = PipeReader.Create(_stream);

                try
                {
                    while (true)
                    {
                        ReadResult result = await reader.ReadAsync();
                        ReadOnlySequence<byte> buffer = result.Buffer;

                        while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                        {
                            ProcessLine(line);
                        }

                        reader.AdvanceTo(buffer.Start, buffer.End);
                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    _cts.Cancel();
                }
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        }

        public void Disconnect()
        {
            Dispose();
        }

        public ValueTask SubscribeAsync(string symbol)
        {
            if (_stream == null)
            {
                Log.Error("Not connected");
                return default;
            }

            char command = '0';
            char separator = '|';
            char endOfLine = '\n';

            var builder = new StringBuilder();
            builder.Append(command);
            builder.Append(separator);
            builder.Append(symbol);
            builder.Append(separator);
            builder.Append(endOfLine);

            return _stream.WriteAsync(Encoding.ASCII.GetBytes(builder.ToString()), _cts.Token);
        }
        
        public void Dispose()
        {
            _cts.Cancel();
            _socket.Dispose();
            _stream.Dispose();
            GC.SuppressFinalize(this);
        }

        private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            SequencePosition? position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }

            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        private void ProcessLine(in ReadOnlySequence<byte> buffer)
        {
            var orderType = "0".AsSpan();
            var tradeType = "1".AsSpan();
            var initType = "2".AsSpan();

            var bytes = buffer.ToArray();
            var chars = ArrayPool<char>.Shared.Rent(Encoding.ASCII.GetCharCount(bytes, 0, bytes.Length));
            var span = new Span<char>(chars);

            Encoding.ASCII.GetChars(bytes, span);

            var sepIndex = span.IndexOf('|');
            var type = span.Slice(0, sepIndex);
            span = span.Slice(sepIndex + 1);

            if (type.SequenceEqual(initType))
            {
                var order = new Order();

                sepIndex = span.IndexOf('|');
                order.Symbol = span.Slice(0, sepIndex).ToString();
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                order.Side = span.Slice(0, sepIndex).ToArray()[0];
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                order.OrderId = span.Slice(0, sepIndex).ToString();
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                order.Price = decimal.Parse(span.Slice(0, sepIndex));
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                order.Size = decimal.Parse(span.Slice(0, sepIndex));
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                DateTime.TryParse(span.Slice(0, sepIndex), out DateTime time);
                order.Time = time;

                OnInitBook(order);
            }
            else if (type.SequenceEqual(orderType))
            {
                var order = new Order();

                sepIndex = span.IndexOf('|');
                order.Symbol = span.Slice(0, sepIndex).ToString();
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                order.Side = span.Slice(0, sepIndex).ToArray()[0];
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                order.OrderId = span.Slice(0, sepIndex).ToString();
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                order.Price = decimal.Parse(span.Slice(0, sepIndex));
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                order.Size = decimal.Parse(span.Slice(0, sepIndex));
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                DateTime.TryParse(span.Slice(0, sepIndex), out DateTime time);
                order.Time = time;

                OnBook(order);
            }
            else if (type.SequenceEqual(tradeType))
            {
                var trade = new Trade();

                sepIndex = span.IndexOf('|');
                trade.Symbol = span.Slice(0, sepIndex).ToString();
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                trade.Side = span.Slice(0, sepIndex).ToArray()[0];
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                trade.TradeId = span.Slice(0, sepIndex).ToString();
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                trade.MakerOrderId = span.Slice(0, sepIndex).ToString();
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                trade.TakerOrderId = span.Slice(0, sepIndex).ToString();
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                trade.Price = decimal.Parse(span.Slice(0, sepIndex));
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                trade.Size = decimal.Parse(span.Slice(0, sepIndex));
                span = span.Slice(sepIndex + 1);

                sepIndex = span.IndexOf('|');
                DateTime.TryParse(span.Slice(0, sepIndex), out DateTime time);
                trade.Time = time;

                OnTrade(trade);
            }

            ArrayPool<char>.Shared.Return(chars);
        }
    }
}