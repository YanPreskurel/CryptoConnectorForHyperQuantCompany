using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Portfolio;
using ConnectorTest;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly PortfolioService _calculator;

    public PortfolioController(ITestConnector connector)
    {
        _calculator = new PortfolioService(connector);
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        var balances = new Dictionary<string, decimal>
        {
            { "BTC", 1m },
            { "XRP", 15000m },
            { "XMR", 50m },
            { "DASH", 30m }
        };

        var targetCurrencies = new List<string> { "USDT", "BTC", "XRP", "XMR", "DASH" };

        var result = await _calculator.CalculatePortfolioValueAsync(balances, targetCurrencies);

        return Ok(result);
    }

}
