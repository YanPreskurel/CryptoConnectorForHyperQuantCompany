using TestHQ;
using ConnectorTest;
using Models;
using Newtonsoft.Json;
using Websocket.Client;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace Bitfinex
{
    public class BitfinexConnector : ITestConnector, IDisposable
    {
        private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api-pub.bitfinex.com/v2/") };
        private WebsocketClient? _ws;
        private readonly Dictionary<int, string> _channels = new();
        private readonly ConcurrentDictionary<string, int> _pairToChannelId = new();
        private readonly ConcurrentDictionary<int, string> _channelIdToPair = new();

        public event Action<Trade>? NewBuyTrade;
        public event Action<Trade>? NewSellTrade;
        public event Action<Candle>? CandleSeriesProcessing;

        private const int DefaultWsReconnectDelay = 5000;

        // Получение последних публичных трейдов
        public async Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
        {
            var cleanPair = pair.StartsWith("t") ? pair : "t" + pair;
            var url = $"trades/{cleanPair}/hist?limit={maxCount}&sort=-1";

            var resp = await _http.GetStringAsync(url);
            var data = JsonConvert.DeserializeObject<List<List<object>>>(resp);

            return data.Select(item => new Trade
            {
                Id = item[0].ToString(),
                Time = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(item[1])),
                Amount = Convert.ToDecimal(item[2]),
                Price = Convert.ToDecimal(item[3]),
                Side = Convert.ToDecimal(item[2]) >= 0 ? "buy" : "sell",
                Pair = cleanPair
            });
        }

        // Получение серии свечей
        public async Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from, DateTimeOffset? to = null, long? count = 0)
        {
            var cleanPair = pair.StartsWith("t") ? pair : "t" + pair;
            var resolution = $"{periodInSec}s";
            var url = $"candles/trade:{resolution}:{cleanPair}/hist?limit={(count > 0 ? count : 100)}";

            if (from != null)
                url += "&start=" + from.Value.ToUnixTimeMilliseconds();
            if (to != null)
                url += "&end=" + to.Value.ToUnixTimeMilliseconds();
            url += "&sort=1";

            var resp = await _http.GetStringAsync(url);
            var data = JsonConvert.DeserializeObject<List<List<object>>>(resp);

            return data.Select(item => new Candle
            {
                OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(item[0])),
                OpenPrice = Convert.ToDecimal(item[1]),
                ClosePrice = Convert.ToDecimal(item[2]),
                HighPrice = Convert.ToDecimal(item[3]),
                LowPrice = Convert.ToDecimal(item[4]),
                TotalVolume = Convert.ToDecimal(item[5]),
                Pair = cleanPair
            });
        }

        private void EnsureWebSocket()
        {
            if (_ws != null) return;

            var url = new Uri("wss://api-pub.bitfinex.com/ws/2");

            _ws = new WebsocketClient(url)
            {
                ReconnectTimeout = TimeSpan.FromMilliseconds(DefaultWsReconnectDelay),
                IsReconnectionEnabled = true
            };

            _ws.MessageReceived.Subscribe(OnMessage);

            _ws.ReconnectionHappened.Subscribe(info =>
            {
                Console.WriteLine($"🔁 Переподключение WebSocket: {info.Type}");

                foreach (var key in _channels.Values.ToList())
                {
                    if (key.StartsWith("trades:"))
                    {
                        var symbol = key.Split(':')[1];

                        var msg = JsonConvert.SerializeObject(new
                        {
                            @event = "subscribe",
                            channel = "trades",
                            symbol = symbol
                        });

                        _ws.Send(msg);

                        Console.WriteLine($"↩️ Повторная подписка на трейды: {symbol}");
                    }
                    else if (key.StartsWith("candles:"))
                    {
                        var parts = key.Split(':');
                        var resolution = parts[2];
                        var symbol = parts[3];

                        var msg = JsonConvert.SerializeObject(new
                        {
                            @event = "subscribe",
                            channel = "candles",
                            key = $"trade:{resolution}:{symbol}"
                        });

                        _ws.Send(msg);

                        Console.WriteLine($"↩️ Повторная подписка на свечи: {symbol} @ {resolution}");
                    }
                }
            });

            _ws.Start().Wait();
        }


        private void OnMessage(ResponseMessage msg)
        {
            if (msg.Text != null && !msg.Text.StartsWith("["))
                return;

            var arr = JsonConvert.DeserializeObject<object[]>(msg.Text);
            int chanId = Convert.ToInt32(arr[0]);

            if (!_channels.TryGetValue(chanId, out var key)) return;

            var payload = arr[1];

            if (key.StartsWith("trades"))
            {
                HandleWsTrades(chanId, payload);
            }
            else if (key.StartsWith("candles"))
            {
                HandleWsCandles(chanId, payload, key);
            }
        }

        private void HandleWsTrades(int chanId, object payload)
        {
            var trades = payload as Newtonsoft.Json.Linq.JArray;

            foreach (var t in trades)
            {
                var item = t.ToObject<long[]>();
                var trade = new Trade
                {
                    Pair = _channels[chanId].Split(':').Last(),
                    Id = item[0].ToString(),
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(item[1]),
                    Amount = Convert.ToDecimal(item[2]),
                    Price = Convert.ToDecimal(item[3]),
                    Side = item[2] >= 0 ? "buy" : "sell"
                };

                if (trade.Side == "buy") NewBuyTrade?.Invoke(trade);
                else NewSellTrade?.Invoke(trade);
            }
        }

        private void HandleWsCandles(int chanId, object payload, string key)
        {
            var arr = payload as Newtonsoft.Json.Linq.JArray;
            var item = arr.Select(x => ((Newtonsoft.Json.Linq.JArray)x).ToObject<long[]>()).FirstOrDefault();

            if (item == null) return;

            var candle = new Candle
            {
                Pair = key.Split(':').Last(),
                OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(item[0]),
                OpenPrice = item[1],
                ClosePrice = item[2],
                HighPrice = item[3],
                LowPrice = item[4],
                TotalVolume = item[5]
            };
            CandleSeriesProcessing?.Invoke(candle);
        }

        public void SubscribeTrades(string pair, int maxCount = 100)
        {
            EnsureWebSocket();
            var clean = pair.StartsWith("t") ? pair : "t" + pair;
            var key = $"trades:{clean}";
            var msg = JsonConvert.SerializeObject(new { @event = "subscribe", channel = "trades", symbol = clean });
            _ws.Send(msg);
            _ws.MessageReceived.Take(1).Subscribe(m =>
            {
                var obj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(m.Text);
                var chanId = obj["chanId"].Value<int>();

                _pairToChannelId[key] = chanId;
                _channelIdToPair[chanId] = key;

                _channels[chanId] = key;
            });
        }

        public void UnsubscribeTrades(string pair)
        {
            var clean = pair.StartsWith("t") ? pair : "t" + pair;
            var key = $"trades:{clean}";

            if (_pairToChannelId.TryRemove(key, out int chanId))
            {
                _channelIdToPair.TryRemove(chanId, out _);
                _channels.Remove(chanId);

                var unsubscribeMessage = new
                {
                    @event = "unsubscribe",
                    chanId = chanId
                };

                _ws.Send(JsonConvert.SerializeObject(unsubscribeMessage));
                Console.WriteLine($"Отписка от трейдов: {pair}");
            }
            else
            {
                Console.WriteLine($"Не найден активный канал для trades: {pair}");
            }
        }


        public void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null, long? count = 0)
        {
            EnsureWebSocket();

            var clean = pair.StartsWith("t") ? pair : "t" + pair;
            var resolution = $"{periodInSec}s";
            var key = $"candles:trade:{resolution}:{clean}";
            var msg = JsonConvert.SerializeObject(new { @event = "subscribe", channel = "candles", key = $"trade:{resolution}:{clean}" });

            _ws.Send(msg);

            _ws.MessageReceived.Take(1).Subscribe(m =>
            {
                var obj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(m.Text);
                var chanId = obj["chanId"].Value<int>();

                _pairToChannelId[key] = chanId;
                _channelIdToPair[chanId] = key;

                _channels[chanId] = $"candles:trade:{resolution}:{clean}";
            });
        }

        public void UnsubscribeCandles(string pair)
        {
            var clean = pair.StartsWith("t") ? pair : "t" + pair;
            var keyPrefix = $"candles:";

            var chanId = _channels.FirstOrDefault(x =>
                x.Value.StartsWith(keyPrefix) && x.Value.EndsWith(clean)).Key;

            if (chanId != 0 && _channels.Remove(chanId))
            {
                var unsubscribeMessage = new
                {
                    @event = "unsubscribe",
                    chanId = chanId
                };

                string json = JsonConvert.SerializeObject(unsubscribeMessage);

                _ws.Send(json);

                Console.WriteLine($"Отписка от свечей для пары: {pair}");
            }
            else
            {
                Console.WriteLine($"Не найден канал подписки на candles для пары: {pair}");
            }
        }

        public async Task<Ticker> GetTickerAsync(string pair)
        {
            var cleanPair = pair.StartsWith("t") ? pair : "t" + pair;
            var url = $"ticker/{cleanPair}";
            var resp = await _http.GetStringAsync(url);
            var data = JsonConvert.DeserializeObject<List<object>>(resp);

            return new Ticker
            {
                Pair = cleanPair,
                Bid = Convert.ToDecimal(data[0]),
                Ask = Convert.ToDecimal(data[2]),
                LastPrice = Convert.ToDecimal(data[6]),
                Volume = Convert.ToDecimal(data[7]),
                High = Convert.ToDecimal(data[8]),
                Low = Convert.ToDecimal(data[9])
            };
        }
        public void Dispose()
        {
            _ws?.Dispose();
            _http?.Dispose();
        }
    }
}
