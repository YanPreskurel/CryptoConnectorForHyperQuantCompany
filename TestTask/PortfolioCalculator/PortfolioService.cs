using ConnectorTest;
using Models;  // предположим, тут есть классы Trade, Ticker и т.п.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Portfolio
{
    public class PortfolioService
    {
        private readonly ITestConnector _connector;

        public PortfolioService(ITestConnector connector)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        }

        /// <summary>
        /// Рассчитывает стоимость портфеля в разных целевых валютах.
        /// </summary>
        /// <param name="balances">Баланс по валютам, ключ — валюта, значение — количество</param>
        /// <param name="targetCurrencies">Валюты для конвертации (например, USDT, BTC и т.п.)</param>
        /// <returns>Словарь, где ключ — валюта, значение — суммарная стоимость портфеля в этой валюте</returns>
        public async Task<Dictionary<string, decimal>> CalculatePortfolioValueAsync(
            Dictionary<string, decimal> balances,
            List<string> targetCurrencies)
        {
            var result = new Dictionary<string, decimal>();

            foreach (var target in targetCurrencies)
            {
                decimal totalValue = 0m;

                foreach (var balance in balances)
                {
                    string fromCurrency = balance.Key.ToUpper();
                    string toCurrency = target.ToUpper();

                    if (fromCurrency == toCurrency)
                    {
                        totalValue += balance.Value;
                        continue;
                    }

                    string pair = $"t{fromCurrency}{toCurrency}";

                    try
                    {
                        var ticker = await _connector.GetTickerAsync(pair);
                        decimal price = ticker.LastPrice;
                        totalValue += balance.Value * price;
                    }
                    catch
                    {
                        if (toCurrency != "BTC" && fromCurrency != "BTC")
                        {
                            decimal interValue = 0m;

                            string pair1 = $"t{fromCurrency}BTC";
                            string pair2 = $"tBTC{toCurrency}";

                            try
                            {
                                var tickerFromToBTC = await _connector.GetTickerAsync(pair1);
                                decimal priceFromToBTC = tickerFromToBTC.LastPrice;
                                interValue = balance.Value * priceFromToBTC;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка при запросе пары {pair1}: {ex.Message}");
                                continue;
                            }

                            try
                            {
                                var tickerBTCtoTarget = await _connector.GetTickerAsync(pair2);
                                decimal priceBTCtoTarget = tickerBTCtoTarget.LastPrice;
                                totalValue += interValue * priceBTCtoTarget;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка при запросе пары {pair2}: {ex.Message}");
                                continue;
                            }
                        }
                    }

                }

                result[target] = totalValue;
            }

            return result;
        }
    }
}
