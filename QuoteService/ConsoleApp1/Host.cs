using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuoteService.Tools;
using QuoteService.Websocket;

#pragma warning disable 4014

namespace QuoteService
{
    public class Host : IDisposable
    {
        private readonly ExchangeWebsocketClient _exchangeWebsocketClient = new ExchangeWebsocketClient();
        private readonly Socket _socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        private readonly Dictionary<string, List<Stream>> _subscribers = new Dictionary<string, List<Stream>>();
        public Action<string, Stream> OnSubscribe { private get; set; }

        public async Task Listen(IPAddress address, int port, CancellationToken cancellationToken)
        {
            await _exchangeWebsocketClient.ConnectAsync("wss://ws-feed.pro.coinbase.com", cancellationToken);
            _exchangeWebsocketClient.Listen();
            _exchangeWebsocketClient.OnMessage = async (symbol, message) =>
            {
                var subscribers = _subscribers[symbol];
                var toSend = Encoding.ASCII.GetBytes(message);

                for (int i = 0; i < subscribers.Count; ++i)
                {
                    var stream = subscribers[i];

                    try
                    {
                        await stream.WriteAsync(toSend, cancellationToken);
                    }
                    catch (IOException exception)
                    {
                        if (exception.InnerException is SocketException)
                        {
                            Log.Info($"{stream.RemoteEndPoint} disconnected");
                            subscribers.Remove(stream);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            };

            _socket.Bind(new IPEndPoint(address, port));
            Log.Info($"Listening on {address}:{port}");
            _socket.Listen(1000);

            await AcceptConnectionsAsync();
        }

        private async Task AcceptConnectionsAsync()
        {
            try
            {
                while (true)
                {
                    var socket = await _socket.AcceptAsync();
                    OnClientConnected(socket);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                throw;
            }
        }

        private async Task OnClientConnected(Socket socket)
        {
            Log.Info($"{socket.RemoteEndPoint} connected");

            var stream = new Stream(socket);
            var reader = PipeReader.Create(stream);

            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    var symbol = GetSymbol(line);
                    if (!_subscribers.TryGetValue(symbol, out var streams))
                    {
                        streams = new List<Stream>();
                        _subscribers.Add(symbol, streams);
                    }

                    streams.Add(stream);

                    await _exchangeWebsocketClient.Subscribe(symbol);
                    await _exchangeWebsocketClient.Consume(symbol);
                    OnSubscribe(symbol, stream);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted)
                {
                    break;
                }
            }

            await reader.CompleteAsync();
            Log.Info($"[{socket.RemoteEndPoint}]: disconnected");
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

        private static string GetSymbol(in ReadOnlySequence<byte> buffer)
        {
            var bytes = buffer.ToArray();
            var chars = ArrayPool<char>.Shared.Rent(Encoding.ASCII.GetCharCount(bytes, 0, bytes.Length));
            var span = new Span<char>(chars);

            Encoding.ASCII.GetChars(bytes, span);

            var endOfCommand = span.IndexOf('|');
            var command = span.Slice(0, endOfCommand);

            var endOfSymbol = span.Slice(endOfCommand + 1).IndexOf('|');
            var symbol = span.Slice(endOfCommand + 1, endOfSymbol - endOfCommand + 1);

            ArrayPool<char>.Shared.Return(chars);

            return symbol.ToString();
        }

        public void Dispose()
        {
            _exchangeWebsocketClient?.Dispose();
            _socket?.Dispose();
        }
    }
}