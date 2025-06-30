using Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestHQ;

namespace ConnectorTest
{
    public class BitfinexWebSocketClient
    {
        private const string WebSocketUri = "wss://api-pub.bitfinex.com/ws/2";
        private ClientWebSocket _webSocket;

        public async Task ConnectAsync()
        {
            _webSocket = new ClientWebSocket();
            await _webSocket.ConnectAsync(new Uri(WebSocketUri), CancellationToken.None);
        }

        public async Task SubscribeToTradesAsync(string symbol, Action<Trade> onTrade)
        {
            await ConnectAsync();

            var subscribeMsg = new
            {
                @event = "subscribe",
                channel = "trades",
                symbol = symbol
            };

            await SendAsync(JsonConvert.SerializeObject(subscribeMsg));

            _ = Task.Run(async () =>
            {
                var buffer = new byte[8192];

                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    if (message.StartsWith("["))
                    {
                        try
                        {
                            var array = JArray.Parse(message);
                            if (array.Count > 1 && array[1] is JArray dataArray && dataArray.Count >= 4)
                            {
                                if (dataArray[0].Type == JTokenType.Array) continue;

                                var trade = new Trade
                                {
                                    Id = dataArray[0].ToString(),
                                    Time = DateTimeOffset.FromUnixTimeMilliseconds((long)dataArray[1]).UtcDateTime,
                                    Amount = (decimal)dataArray[2],
                                    Price = (decimal)dataArray[3]
                                };
                                onTrade?.Invoke(trade);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error trade failed to parse message: {message}");
                            Console.WriteLine($"Exception: {ex.Message}");
                        }
                    }
                }
            });
        }

        public async Task SubscribeToCandlesAsync(string key, Action<Candle> onCandle)
        {
            await ConnectAsync();

            var subscribeMsg = new
            {
                @event = "subscribe",
                channel = "candles",
                key = key
            };

            await SendAsync(JsonConvert.SerializeObject(subscribeMsg));

            _ = Task.Run(async () =>
            {
                var buffer = new byte[8192];

                while (_webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    if (message.StartsWith('['))
                    {
                        try
                        {
                            var array = JArray.Parse(message);
                            if (array.Count > 1 && array[1] is JArray dataArray && dataArray.Count == 6)
                            {
                                var candle = new Candle
                                {
                                    OpenTime = DateTimeOffset.FromUnixTimeMilliseconds((long)dataArray[0]).UtcDateTime,
                                    OpenPrice = (decimal)dataArray[1],
                                    ClosePrice = (decimal)dataArray[2],
                                    HighPrice = (decimal)dataArray[3],
                                    LowPrice = (decimal)dataArray[4],
                                    TotalVolume = (decimal)dataArray[5]
                                };
                                onCandle?.Invoke(candle);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error candle failed to parse message: {message}");
                            Console.WriteLine($"Exception: {ex.Message}");
                        }
                    }
                }
            });
        }

        private async Task SendAsync(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
