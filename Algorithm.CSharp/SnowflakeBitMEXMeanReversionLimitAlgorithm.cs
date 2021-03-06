/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using NodaTime;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Crypto;
using QuantConnect.Util;
using Snowflake;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash. This is a skeleton
    /// framework you can use for designing an algorithm.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class SnowflakeBitMEXMeanReversionLimitAlgorithm : QCAlgorithm, ISnowflakeAlgorithm
    {
        private const string ENTRY = "ENTRY"; 
        private const string EXIT = "EXIT";
        private const string XBTUSD = "XBTUSD";
        private static readonly decimal TICK_SIZE = new decimal(0.5);
        private Signal _lastSignal = new Signal{Time = DateTime.Now, Type = EXIT};
        private static readonly decimal MIN_MEAN_REVERSION_THRESHOLD = new decimal(0.002);
        private static readonly decimal MAX_MEAN_REVERSION_THRESHOLD = new decimal(0.02);
        private static readonly decimal MAX_VOLATILITY = 5;
        private static readonly decimal MaxOrderflow = 1000000m;
        private static readonly int WINDOW_LENGTH = 14;
        private Crypto _xbtusd;
        private const int MINUTES = 1;
        private decimal bidPrice = 0;
        private decimal askPrice = 0;
        private List<Quote> _quotes = new List<Quote>();
        private List<Trade> _trades = new List<Trade>();
        private OrderTicket _orderticket;
        private static readonly decimal LEVERAGE = new decimal(0.1);
        private AverageTrueRange _natr;
        private DateTime _resetDate = DateTime.MinValue;
        private decimal _fees;

        public DateTime _startDate { get; private set; }
        public DateTime _endDate { get; private set; }
        public string _name { get; set; }

        public override void Initialize()
        {
            _startDate = new DateTime(2018, 9, 18);
            _endDate = new DateTime(2018, 9, 18);
            _name = "SnowflakeBitMEXMeanReversionLimitAlgorithmTrue";
            SetStartDate(_startDate);  //Set Start Date
            SetEndDate(_endDate);    //Set End Date
            SetCash(1000000);
            SetTimeZone(DateTimeZone.Utc);
            
            
            _xbtusd = AddCrypto("XBTUSD", Resolution.Tick, Market.GDAX);
            
            _xbtusd.SetMarginModel(new SecurityMarginModel());

            _fees = 0m;
        }

        public override void OnData(Slice data)
        {   
            if (data.Ticks["XBTUSD"].IsNullOrEmpty()) return;
            var quote = UpdateQuotes(data);
            var trade = UpdateTrades(data);

            if (quote == null) return;
            if (trade == null) return;
            if (_quotes.IsNullOrEmpty()) return;

            // var volatility = _trades.GroupBy(t => t.Time.Second).Average(trades => trades.Max(t => t.Price) - trades.Min(t => t.Price));
            // if (volatility > MAX_VOLATILITY) _resetDate = data.Time.AddMinutes(5);
            
            var orderflow = _trades.Sum(t => t.Side == "Buy" ? t.Quantity : -t.Quantity );
            if (Math.Abs(orderflow) > MaxOrderflow) _resetDate = _resetDate = data.Time.AddMinutes(5);
            
            if (_resetDate > data.Time)
            {
                var quantity = Portfolio[XBTUSD].Quantity;
                CancelOrder();
                if (quantity == 0) return;
                var fee = Math.Abs(quantity) * trade.Price * 0.00075m;
                _fees += fee;
                MarketOrder(_xbtusd.Symbol, -quantity);
                Console.WriteLine($"{data.Time} Executed Market Order, fee: {fee} totalFees: {_fees}");
                return;
            }
            
            
            var firstQuote = _quotes.First();
            var rateOfChange = quote.MidPrice / firstQuote.MidPrice;
            
            if (Portfolio.CashBook["XBT"].ConversionRate == 0) return;
            
            var meanReversion = rateOfChange - 1;

            var isMeanReverting = Math.Abs(meanReversion) > MIN_MEAN_REVERSION_THRESHOLD &&
                                  Math.Abs(meanReversion) < MAX_MEAN_REVERSION_THRESHOLD;

            if (Portfolio.Invested && _lastSignal.Type == ENTRY && _lastSignal.IsOld(data.Time))
            {
                _lastSignal = new Signal {Time = data.Time, Type = EXIT, Quantity = -1 * _lastSignal.Quantity};
                // Debug($"Closing {data.Time}");
            }
            
            if (!Portfolio.Invested && _lastSignal.Type == EXIT && isMeanReverting)
            {
                _lastSignal = new Signal{Time = data.Time, Type = ENTRY, Quantity = -1 * Math.Sign(meanReversion) * Portfolio.Cash / quote.AskPrice * LEVERAGE};
                var side = _lastSignal.Quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell;
                // Debug($"{side} {data.Time} meanReversion {meanReversion} quote: {quote.Time} {quote.MidPrice} firstQuote: {firstQuote.Time} {firstQuote.MidPrice}");
            }

            SyncOrders();
        }

        private Trade UpdateTrades(Slice data)
        {
            var ticks = data.Ticks["XBTUSD"].Where(t => t.TickType == TickType.Trade);
            if (ticks.IsNullOrEmpty()) return null;
            var tick = ticks.Last();
            if (tick.LastPrice == 0) return null;
            var trade = new Trade {Time = data.Time, Price = tick.LastPrice, Quantity = tick.Quantity, Side = tick.SaleCondition};
            _trades.Add(trade);
            _trades.RemoveAll(q => (data.Time - q.Time).TotalSeconds > WINDOW_LENGTH);
            return trade;
        }

        private Quote UpdateQuotes(Slice data)
        {
            var ticks = data.Ticks["XBTUSD"].Where(t => t.TickType == TickType.Quote);
            if (ticks.IsNullOrEmpty()) return null;
            var tick = ticks.Last();
            if (tick.BidPrice == 0) return null;
            if (tick.AskPrice == 0) return null;
            var quote = new Quote {Time = data.Time, BidPrice = tick.BidPrice, AskPrice = tick.AskPrice, MidPrice = (tick.BidPrice + tick.AskPrice)/ 2};
            _quotes.Add(quote);
            _quotes.RemoveAll(q => (data.Time - q.Time).TotalMinutes > MINUTES);
            return quote;
        }

        private void SyncOrders()
        { 
            if (!Portfolio.Invested && _lastSignal.Type == EXIT) CancelOrder();
            if (!Portfolio.Invested && _lastSignal.Type == ENTRY) SyncOrder(_lastSignal.Quantity);
            if (Portfolio.Invested && _lastSignal.Type == EXIT) SyncOrder(-1 * Portfolio[XBTUSD].Quantity);
            if (Portfolio.Invested && _lastSignal.Type == ENTRY) CancelOrder();
        }

        private void CancelOrder()
        {
            if (_orderticket == null) return;
            
            switch (_orderticket.Status)
            {
                case OrderStatus.Canceled:
                case OrderStatus.CancelPending:
                case OrderStatus.Filled:
                case OrderStatus.Invalid: return;
                default: _orderticket.Cancel(); return;
            }
        }

        private void SyncOrder(decimal quantity)
        {
            var quote = _quotes.Last();
            var price = quantity > 0 ? quote.AskPrice - TICK_SIZE : quote.BidPrice + TICK_SIZE;
            
            if (_orderticket == null)
            {
                _orderticket = LimitOrder(_xbtusd.Symbol, quantity, price); 
                return;
            }
            
            switch (_orderticket.Status)
            {
                case OrderStatus.Canceled:
                case OrderStatus.CancelPending:
                case OrderStatus.Filled:
                case OrderStatus.Invalid: 
                    _orderticket = LimitOrder(_xbtusd.Symbol, quantity, price); 
                    return;
                case OrderStatus.New:
                case OrderStatus.Submitted:
                case OrderStatus.PartiallyFilled:
                case OrderStatus.None:
                    if (price == _orderticket.Get(OrderField.LimitPrice)) return;
                    _orderticket.Update(new UpdateOrderFields {LimitPrice = price});
                    return;
            }
           
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.FillQuantity == 0) return;
            // Debug(orderEvent.ToString());
        }

        public override void OnEndOfAlgorithm()
        {
            // Debug($"Total Fees: {_fees}");
            Snowflake.Common.OnEndOfAlgorithm(this);
        }

        private class Quote
        {
            public DateTime Time { get; set; }
            public decimal BidPrice { get; set; }
            public decimal AskPrice { get; set; }
            public decimal MidPrice { get; set; }
        }

        private class Trade
        {
            public DateTime Time { get; set; }
            public decimal Price { get; set; }
            public decimal Quantity { get; set; }
            public string Side { get; set; }
        }
        
        private class Signal
        {
            public DateTime Time { get; set; }
            public string Type { get; set; }
            public decimal Quantity { get; set; }
            public bool IsOld(DateTime time) { return (time - Time).TotalMinutes >= MINUTES; }
        }
    }
}

namespace Snowflake
{
    public interface ISnowflakeAlgorithm : IAlgorithm
    {
        DateTime _startDate { get; }
        DateTime _endDate { get; }
        string _name { get; }
    }
    
    public static class Common
    {
        public static void OnEndOfAlgorithm(ISnowflakeAlgorithm a)
        {
            var json = JsonConvert.SerializeObject(a.Portfolio.Transactions.TransactionRecord.ToArray());
            const string format = "yyyyMMdd";
            System.IO.File.WriteAllText($@"../../../snowflake/public/backtest.json", json);
            System.IO.File.WriteAllText($@"../../../snowflake/public/backtest_{a._name}_{a._startDate.ToString(format)}_{a._endDate.ToString(format)}.json", json);
            Process.Start("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome", "http://localhost:3000");
        }
    }

}

/*
 with private bool _enableMarketOrderOnHighVolatility = true;
  totalFees: 2563,431750
20180923 09:53:03.394 Trace:: Engine.Run(): Exiting Algorithm Manager
Ett nytt fönster skapades under den aktuella webbläsarsessionen.
20180923 09:53:04.447 Trace:: Debug: Algorithm Id:(SnowflakeBitMEXMeanReversionLimitAlgorithm) completed in 50,20 seconds at 37k data points per second. Processing total of 1 843 004 data points.
20180923 09:53:04.723 Trace:: STATISTICS:: Total Trades 26
20180923 09:53:04.723 Trace:: STATISTICS:: Average Win 0.01%
20180923 09:53:04.723 Trace:: STATISTICS:: Average Loss -0.01%
20180923 09:53:04.723 Trace:: STATISTICS:: Compounding Annual Return 1.101%
20180923 09:53:04.723 Trace:: STATISTICS:: Drawdown 0.000%
20180923 09:53:04.723 Trace:: STATISTICS:: Expectancy 0.037
20180923 09:53:04.723 Trace:: STATISTICS:: Net Profit 0.003%
20180923 09:53:04.723 Trace:: STATISTICS:: Sharpe Ratio 0
20180923 09:53:04.723 Trace:: STATISTICS:: Loss Rate 62%
20180923 09:53:04.723 Trace:: STATISTICS:: Win Rate 38%
20180923 09:53:04.723 Trace:: STATISTICS:: Profit-Loss Ratio 1.70
20180923 09:53:04.724 Trace:: STATISTICS:: Alpha 0
20180923 09:53:04.724 Trace:: STATISTICS:: Beta 0
20180923 09:53:04.724 Trace:: STATISTICS:: Annual Standard Deviation 0
20180923 09:53:04.724 Trace:: STATISTICS:: Annual Variance 0
20180923 09:53:04.724 Trace:: STATISTICS:: Information Ratio 0
20180923 09:53:04.724 Trace:: STATISTICS:: Tracking Error 0
20180923 09:53:04.724 Trace:: STATISTICS:: Treynor Ratio 0
20180923 09:53:04.724 Trace:: STATISTICS:: Total Fees $0.00
20180923 09:53:04.724 Trace:: BacktestingResultHandler.SendAnalysisResult(): Processed final packet
20180923 09:53:04.725 Trace:: FileSystemDataFeed.Exit(): Exit triggered.
20180923 09:53:04.725 Trace:: BrokerageTransactionHandler.Run(): Ending Thread...
20180923 09:53:04.726 Trace:: FileSystemDataFeed.Run(): Ending Thread... 
20180923 09:53:04.750 Trace:: Debug: Your log was successfully created and can be retrieved from: /Users/andersback/Projects/lean/Launcher/bin/Debug/SnowflakeBitMEXMeanReversionLimitAlgorithm-log.txt
20180923 09:53:04.751 Trace:: BacktestingResultHandler.Run(): Ending Thread...
20180923 09:53:04.833 Trace:: Waiting for threads to exit...
20180923 09:53:14.806 Trace:: Engine.Run(): Disconnecting from brokerage...
20180923 09:53:14.807 Trace:: Engine.Run(): Disposing of setup handler...
20180923 09:53:14.807 Trace:: Engine.Main(): Analysis Completed and Results Posted.
20180923 09:53:14.807 Trace:: FileSystemDataFeed.Exit(): Exit triggered.
Engine.Main(): Analysis Complete. Press any key to continue.

  */