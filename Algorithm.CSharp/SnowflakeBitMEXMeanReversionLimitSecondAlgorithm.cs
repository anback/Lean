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
    public class SnowflakeBitMEXMeanReversionLimitSecondAlgorithm : QCAlgorithm, ISnowflakeAlgorithm
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
        private List<QuoteBar> _quotes = new List<QuoteBar>();
        private List<TradeBar> _trades = new List<TradeBar>();
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
            
            
            _xbtusd = AddCrypto("XBTUSD", Resolution.Second, Market.GDAX);
            
            _xbtusd.SetMarginModel(new SecurityMarginModel());

            _fees = 0m;
        }

        public override void OnData(Slice data)
        {   
            var quote = UpdateQuotes(data);
            var trade = UpdateTrades(data);

            if (quote == null) return;
            if (trade == null) return;
            if (_quotes.IsNullOrEmpty()) return;
            
            var orderflow = _trades.Sum(t => t.Orderflow);
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
            var rateOfChange = quote.High / firstQuote.Low;
            
            if (Portfolio.CashBook["XBT"].ConversionRate == 0) return;
            
            var meanReversion = rateOfChange - 1;

            var isMeanReverting = Math.Abs(meanReversion) > MIN_MEAN_REVERSION_THRESHOLD &&
                                  Math.Abs(meanReversion) < MAX_MEAN_REVERSION_THRESHOLD;

            if (Portfolio.Invested && _lastSignal.Type == ENTRY && _lastSignal.IsOld(data.Time))
            {
                _lastSignal = new Signal {Time = data.Time, Type = EXIT, Quantity = -1 * _lastSignal.Quantity};
                Debug($"Closing {data.Time}");
            }
            
            if (!Portfolio.Invested && _lastSignal.Type == EXIT && isMeanReverting)
            {
                _lastSignal = new Signal{Time = data.Time, Type = ENTRY, Quantity = -1 * Math.Sign(meanReversion) * Portfolio.Cash / quote.Bid.Close * LEVERAGE};
                var side = _lastSignal.Quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell;
                Debug($"{side} {data.Time} meanReversion {meanReversion} quote: {quote.Time} {quote.Bid.Close} firstQuote: {firstQuote.Time} {firstQuote.Bid.Close}");
            }

            SyncOrders();
        }

        private TradeBar UpdateTrades(Slice data)
        {
            var bar = data.Bars["XBTUSD"];
            if (bar == null) return null;
            if (bar.Close == 0) return null;
            _trades.Add(bar);
            _trades.RemoveAll(q => (data.Time - q.Time).TotalSeconds > WINDOW_LENGTH);
            return bar;
        }

        private QuoteBar UpdateQuotes(Slice data)
        {
            var bar = data.QuoteBars["XBTUSD"];
            if (bar == null) return null;
            if (bar.Bid.Close == 0) return null;
            if (bar.Ask.Close == 0) return null;
            _quotes.Add(bar);
            _quotes.RemoveAll(q => (data.Time - q.Time).TotalMinutes > MINUTES);
            return bar;
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
            var price = quantity > 0 ? quote.Ask.Close - TICK_SIZE : quote.Bid.Close + TICK_SIZE;
            
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

/*
 with private bool _enableMarketOrderOnHighVolatility = true;
  totalFees: 2563,431750
20180919 11:42:49.621 Trace:: STATISTICS:: Total Trades 1342
20180919 11:42:49.621 Trace:: STATISTICS:: Average Win 0.01%
20180919 11:42:49.621 Trace:: STATISTICS:: Average Loss -0.02%
20180919 11:42:49.621 Trace:: STATISTICS:: Compounding Annual Return -50.022%
20180919 11:42:49.621 Trace:: STATISTICS:: Drawdown 3.400%
20180919 11:42:49.621 Trace:: STATISTICS:: Expectancy -0.285
20180919 11:42:49.621 Trace:: STATISTICS:: Net Profit -3.363%
20180919 11:42:49.621 Trace:: STATISTICS:: Sharpe Ratio -17.698
20180919 11:42:49.621 Trace:: STATISTICS:: Loss Rate 53%
20180919 11:42:49.621 Trace:: STATISTICS:: Win Rate 47%
20180919 11:42:49.621 Trace:: STATISTICS:: Profit-Loss Ratio 0.52
20180919 11:42:49.621 Trace:: STATISTICS:: Alpha -0.626
20180919 11:42:49.621 Trace:: STATISTICS:: Beta 11.359
20180919 11:42:49.622 Trace:: STATISTICS:: Annual Standard Deviation 0.027
20180919 11:42:49.622 Trace:: STATISTICS:: Annual Variance 0.001
20180919 11:42:49.622 Trace:: STATISTICS:: Information Ratio -18.191
20180919 11:42:49.622 Trace:: STATISTICS:: Tracking Error 0.027
20180919 11:42:49.622 Trace:: STATISTICS:: Treynor Ratio -0.042
 
 14000 kr på 18 dagar, 
 insator på 14 bitcoins a 800000 kr. Vad är det du satsade, du satsade som mest 5000 dollar på 15 robotar. Det är ungefär 75000 dollar och hade du brytt dig om 14000 kr, hmm ja och nej, troligen nejh, 
 ok då är det här en bra ide. 
 Slutsatsen blir att du ska försöka med denna strategi, eller satsa på en annan risk modell, 
 */
 
 /*
  *
  * 20180919 12:43:58.571 Trace:: STATISTICS:: Total Trades 1510
20180919 12:43:58.571 Trace:: STATISTICS:: Average Win 0.01%
20180919 12:43:58.571 Trace:: STATISTICS:: Average Loss -0.02%
20180919 12:43:58.571 Trace:: STATISTICS:: Compounding Annual Return -47.562%
20180919 12:43:58.571 Trace:: STATISTICS:: Drawdown 3.200%
20180919 12:43:58.571 Trace:: STATISTICS:: Expectancy -0.208
20180919 12:43:58.571 Trace:: STATISTICS:: Net Profit -3.133%
20180919 12:43:58.571 Trace:: STATISTICS:: Sharpe Ratio -10.914
20180919 12:43:58.571 Trace:: STATISTICS:: Loss Rate 52%
20180919 12:43:58.571 Trace:: STATISTICS:: Win Rate 48%
20180919 12:43:58.571 Trace:: STATISTICS:: Profit-Loss Ratio 0.64
20180919 12:43:58.571 Trace:: STATISTICS:: Alpha -0.43
20180919 12:43:58.571 Trace:: STATISTICS:: Beta -1.149
20180919 12:43:58.571 Trace:: STATISTICS:: Annual Standard Deviation 0.041
20180919 12:43:58.571 Trace:: STATISTICS:: Annual Variance 0.002
20180919 12:43:58.572 Trace:: STATISTICS:: Information Ratio -11.234
20180919 12:43:58.572 Trace:: STATISTICS:: Tracking Error 0.041
20180919 12:43:58.572 Trace:: STATISTICS:: Treynor Ratio 0.387
20180919 12:43:58.572 Trace:: STATISTICS:: Total Fees $0.00
  */