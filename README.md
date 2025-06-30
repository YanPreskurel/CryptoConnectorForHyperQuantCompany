# Crypto connector for Hyper Quant company

📌 *Task (based on Bitfinex) with:*

  1. Trades (trades)

  2. Candlesticks (candles)

  3. Ticker information

Also includes portfolio value calculation across multiple currencies, shown in an ASP.NET Core application.

🏗️ *Project Structure*
  1. ConnectorTest — Class Library with REST and WebSocket client implementations for Bitfinex

  2. Models — Common models: Trade, Candle, Ticker, etc.

  3. Portfolio — Business logic for portfolio value calculation

  4. WebApp — ASP.NET Core web app showing the results

  5. Pages/Portfolio — Razor Page to display the portfolio table

⚙️ *Features*
  1. REST API: Ticker, Candles, Trades

  2. WebSocket API: Real-time Trades and Candles

  3. Portfolio value in BTC, USDT, XRP, XMR, DASH

  4. Frontend via ASP.NET Razor Pages

# Requirements for the test assignment

*1) It is necessary to implement a connector for the original interface (item 3) in C# (Class Library), as well as to cover it with integration tests, or to make a simple output on the GUI Framework (WPF, asp.net) in a separate project based on the MVVM pattern, not WinForms. 
What should be in this connector:*

*2) A client class for the REST API of the Bitfinex exchange, which implements 2 functions:
    Getting trades (trades) 
    Getting candles (candles) 
    Getting information about the ticker (Ticker)
    Client class for the Bitfinex exchange Websocket API, which implements 2 functions:
    Getting trades (trades)
    Getting candles (candles)*

*3) Also, implement the calculation: there are 4 different cryptocurrencies on the balance: 1 BTC, 15000 XRP, 50 XMR, and 30 DASH. It is necessary to display the total portfolio balance in each of the listed   currencies in USDT, BTC, XRP, XMR, and DASH (i.e. add and/or convert) and display the results in the Datagrid WPF*
