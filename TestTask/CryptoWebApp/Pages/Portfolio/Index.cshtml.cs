using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio;

namespace CryptoWebApp.Pages.Portfolio
{
    public class IndexModel : PageModel
    {
        private readonly PortfolioService _portfolioService;

        public IndexModel(PortfolioService portfolioService)
        {
            _portfolioService = portfolioService;
        }

        public Dictionary<string, decimal> PortfolioValues { get; private set; }

        public async Task OnGetAsync()
        {
            // Балансы из условия задачи
            var balances = new Dictionary<string, decimal>
            {
                { "BTC", 1m },
                { "XRP", 15000m },
                { "XMR", 50m },
                { "DASH", 30m }
            };

            var targetCurrencies = new List<string> { "USDT", "BTC", "XRP", "XMR", "DASH" };

            PortfolioValues = await _portfolioService.CalculatePortfolioValueAsync(balances, targetCurrencies);
        }
    }
}
