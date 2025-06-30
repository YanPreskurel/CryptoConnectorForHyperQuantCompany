using System.Threading.Tasks;
using Xunit;
using ConnectorTest;
using Models;
using Bitfinex;

public class BitfinexConnectorTests
{
    private readonly ITestConnector _connector = new BitfinexConnector();

    [Fact]
    public async Task GetNewTradesAsync_ShouldReturnTrades()
    {
        var trades = await _connector.GetNewTradesAsync("BTCUSD", 10);
        Assert.NotNull(trades);
        Assert.NotEmpty(trades);
    }

    [Fact]
    public async Task GetCandleSeriesAsync_ShouldReturnCandles()
    {
        var candles = await _connector.GetCandleSeriesAsync("BTCUSD", 60, null, null, 5);
        Assert.NotNull(candles);
        Assert.NotEmpty(candles);
    }

    [Fact]
    public async Task GetTickerAsync_ShouldReturnTicker()
    {
        var ticker = await _connector.GetTickerAsync("BTCUSD");
        Assert.NotNull(ticker);
        Assert.True(ticker.LastPrice > 0);
    }
}
