using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using QuoteService.RestApi;
using QuoteService.Tools;
using QuoteService.Websocket.Message;
using static System.Net.WebSockets.WebSocketMessageType;
using Channel = System.Threading.Channels.Channel;

// ReSharper disable TooWideLocalVariableScope

namespace QuoteService.Websocket
{
    public class ExchangeWebsocketClient : IDisposable
    {
        public Action<string, string> OnMessage { private get; set; }

        private readonly ClientWebSocket _webSocket = new ClientWebSocket();
        private readonly ExchangeRestClient _restClient = new ExchangeRestClient("https://api.pro.coinbase.com/");

        private CancellationToken _cancellationToken;
        private readonly Dictionary<string, Channel<string>> _messages = new Dictionary<string, Channel<string>>();
        private readonly object _bookLocker = new object();
        private readonly Dictionary<string, Dictionary<string, Order>> _orderBook = new Dictionary<string, Dictionary<string, Order>>();
        private readonly Dictionary<string, ReadOnlyMemory<char>> _orderStartSequence = new Dictionary<string, ReadOnlyMemory<char>>();

        public Task ConnectAsync(string url, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            return _webSocket.ConnectAsync(new Uri(url), cancellationToken);
        }

        public void Listen()
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await Receive();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    throw;
                }
            }, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public async Task Subscribe(string symbol)
        {
            if (_orderStartSequence.ContainsKey(symbol))
            {
                return;
            }

            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {SingleReader = true, SingleWriter = true});
            _messages.Add(symbol, channel);

            var req = new Request
            {
                type = "subscribe",
                channels = new[] { new Message.Channel { name = "full", product_ids = new[] { symbol } } }
            };

            var data = JsonSerializer.Serialize(req);
            await _webSocket.SendAsync(Encoding.UTF8.GetBytes(data), Text, true, _cancellationToken);

            var result = await _restClient.GetOrderBook(symbol);

            _orderBook.Add(symbol, result.book);
            _orderStartSequence.Add(symbol, result.sequence);
        }

        public async Task Receive()
        {
            var buffer = new ArraySegment<byte>(new byte[512]);
            WebSocketReceiveResult result;

            do
            {
                await using var ms = new MemoryStream();

                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, _cancellationToken);
                    await ms.WriteAsync(buffer.Array, buffer.Offset, result.Count, _cancellationToken);
                } while (!result.EndOfMessage);

                if (result.MessageType == Close)
                {
                    break;
                }

                ms.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(ms, Encoding.UTF8);
                var line = await reader.ReadToEndAsync();
                var symbol = GetSymbol(line);

                if (symbol == null)
                {
                    continue;
                }

                await _messages[symbol].Writer.WriteAsync(line, _cancellationToken);
            } while (true);
        }

        public Task Consume(string symbol)
        {
            var channel = _messages[symbol];

            return Task.Factory.StartNew(async () =>
            {
                try
                {
                    while (await channel.Reader.WaitToReadAsync(_cancellationToken))
                    {
                        while (channel.Reader.TryRead(out string item))
                        {
                            ProcessMessage(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    throw;
                }
            }, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void ProcessMessage(string message)
        {
            var line = message.AsSpan();
            if (false == IsSequencedAfterStart(line))
            {
                return;
            }

            if (line.IndexOf(messageTypeReceived.Span) != -1 || line.IndexOf(messageTypeActivate.Span) != -1)
            {
                return;
            }

            var symbol = GetSymbol(message);
            var book = _orderBook[symbol];

            if (line.IndexOf(messageTypeOpen.Span) != -1)
            {
                var parsed = JsonSerializer.Deserialize<Open>(message, _serializerOptions);
                var order = new Order()
                {
                    Price = parsed.price,
                    Size = parsed.remaining_size,
                    Side = parsed.side[0]
                };

                lock (_bookLocker)
                {
                    book.Add(parsed.order_id, order);
                }

                var builder = new StringBuilder();
                builder.Append('0');
                builder.Append('|');
                builder.Append(symbol);
                builder.Append('|');
                builder.Append(order.Side);
                builder.Append('|');
                builder.Append(parsed.order_id);
                builder.Append('|');
                builder.Append(order.Price);
                builder.Append('|');
                builder.Append(order.Size);
                builder.Append('|');
                builder.Append(parsed.time);
                builder.Append('|');
                builder.Append('\n');

                OnMessage(symbol, builder.ToString());
            }
            else if (line.IndexOf(messageTypeChange.Span) != -1)
            {
                var parsed = JsonSerializer.Deserialize<Change>(message, _serializerOptions);

                lock (_bookLocker)
                {
                    if (book.ContainsKey(parsed.order_id))
                    {
                        var order = book[parsed.order_id];
                        order.Size = parsed.new_size;

                        var builder = new StringBuilder();
                        builder.Append('0');
                        builder.Append('|');
                        builder.Append(symbol);
                        builder.Append('|');
                        builder.Append(order.Side);
                        builder.Append('|');
                        builder.Append(parsed.order_id);
                        builder.Append('|');
                        builder.Append(order.Price);
                        builder.Append('|');
                        builder.Append(order.Size);
                        builder.Append('|');
                        builder.Append(parsed.time);
                        builder.Append('|');
                        builder.Append('\n');

                        OnMessage(symbol, builder.ToString());
                    }
                }
            }
            else if (line.IndexOf(messageTypeMatch.Span) != -1)
            {
                var parsed = JsonSerializer.Deserialize<Match>(message, _serializerOptions);

                lock (_bookLocker)
                {
                    if (book.ContainsKey(parsed.maker_order_id))
                    {
                        var order = book[parsed.maker_order_id];

                        order.Size -= parsed.size;

                        var builder = new StringBuilder();
                        builder.Append('0');
                        builder.Append('|');
                        builder.Append(symbol);
                        builder.Append('|');
                        builder.Append(order.Side);
                        builder.Append('|');
                        builder.Append(parsed.maker_order_id);
                        builder.Append('|');
                        builder.Append(order.Price);
                        builder.Append('|');
                        builder.Append(order.Size);
                        builder.Append('|');
                        builder.Append(parsed.time);
                        builder.Append('|');
                        builder.Append('\n');

                        OnMessage(symbol, builder.ToString());

                        builder.Clear();
                        builder.Append('1');
                        builder.Append('|');
                        builder.Append(symbol);
                        builder.Append('|');
                        builder.Append(order.Side);
                        builder.Append('|');
                        builder.Append(parsed.trade_id);
                        builder.Append('|');
                        builder.Append(parsed.maker_order_id);
                        builder.Append('|');
                        builder.Append(parsed.taker_order_id);
                        builder.Append('|');
                        builder.Append(parsed.price);
                        builder.Append('|');
                        builder.Append(parsed.size);
                        builder.Append('|');
                        builder.Append(parsed.time);
                        builder.Append('|');
                        builder.Append('\n');

                        OnMessage(symbol, builder.ToString());
                    }
                }
            }
            else if (line.IndexOf(messageTypeDone.Span) != -1)
            {
                var parsed = JsonSerializer.Deserialize<Done>(message, _serializerOptions);

                lock (_bookLocker)
                {
                    if (book.ContainsKey(parsed.order_id))
                    {
                        var order = book[parsed.order_id];
                        book.Remove(parsed.order_id);

                        var builder = new StringBuilder();
                        builder.Append('0');
                        builder.Append('|');
                        builder.Append(symbol);
                        builder.Append('|');
                        builder.Append(order.Side);
                        builder.Append('|');
                        builder.Append(parsed.order_id);
                        builder.Append('|');
                        builder.Append(order.Price);
                        builder.Append('|');
                        builder.Append('0');
                        builder.Append('|');
                        builder.Append(parsed.time);
                        builder.Append('|');
                        builder.Append('\n');

                        OnMessage(symbol, builder.ToString());
                    }
                }
            }
        }

        private bool IsSequencedAfterStart(ReadOnlySpan<char> messageSpan)
        {
            var symbolSpan = symbolProperty.Span;
            int symbolStart = messageSpan.IndexOf(symbolSpan);
            int symbolValueStart = symbolStart + symbolSpan.Length;
            int symbolValueSize = messageSpan.Slice(symbolValueStart).IndexOf(',');
            var symbolValue = messageSpan.Slice(symbolValueStart + 1, symbolValueSize - 2);

            var startSpan = _orderStartSequence[symbolValue.ToString()].Span;
            var sequenceSpan = sequenceProperty.Span;

            int sequenceStart = messageSpan.IndexOf(sequenceSpan);
            if (sequenceStart < 0)
            {
                return false;
            }

            int sequenceValueStart = sequenceStart + sequenceSpan.Length;
            int sequenceValueSize = messageSpan.Slice(sequenceValueStart).IndexOf(',');
            var sequenceValue = messageSpan.Slice(sequenceValueStart, sequenceValueSize);


            if (startSpan.Length > sequenceValue.Length)
            {
                return false;
            }

            if (startSpan.Length < sequenceValue.Length)
            {
                return true;
            }

            for (int i = 0; i < startSpan.Length; ++i)
            {
                if (startSpan[i] > sequenceValue[i])
                {
                    return false;
                }
                if (startSpan[i] < sequenceValue[i])
                {
                    return true;
                }
            }

            return false;
        }

        private string GetSymbol(string message)
        {
            var messageSpan = message.AsSpan();

            var symbolSpan = symbolProperty.Span;
            int symbolStart = messageSpan.IndexOf(symbolSpan);
            if (symbolStart < 0)
            {
                return null;
            }

            int symbolValueStart = symbolStart + symbolSpan.Length;
            int symbolValueSize = messageSpan.Slice(symbolValueStart).IndexOf(',');
            var symbolValue = messageSpan.Slice(symbolValueStart + 1, symbolValueSize - 2);

            return symbolValue.ToString();
        }

        private static ReadOnlyMemory<char> sequenceProperty = "\"sequence\":".AsMemory();
        private static ReadOnlyMemory<char> symbolProperty = "\"product_id\":".AsMemory();
        private static ReadOnlyMemory<char> messageTypeReceived = "\"type\":\"received\"".AsMemory();
        private static ReadOnlyMemory<char> messageTypeActivate = "\"type\":\"activate\"".AsMemory();
        private static ReadOnlyMemory<char> messageTypeOpen = "\"type\":\"open\"".AsMemory();
        private static ReadOnlyMemory<char> messageTypeChange = "\"type\":\"change\"".AsMemory();
        private static ReadOnlyMemory<char> messageTypeMatch = "\"type\":\"match\"".AsMemory();
        private static ReadOnlyMemory<char> messageTypeDone = "\"type\":\"done\"".AsMemory();
        private JsonSerializerOptions _serializerOptions = new JsonSerializerOptions { Converters = { new DecimalConverter() } };

        public void Dispose()
        {
            _webSocket?.Dispose();
            _restClient?.Dispose();
        }
    }
}