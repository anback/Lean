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
using Newtonsoft.Json;
using QuantConnect.Data;
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
        private Crypto _xbtusd;
        private const int MINUTES = 1;
        private decimal bidPrice = 0;
        private decimal askPrice = 0;
        private List<Quote> _quotes = new List<Quote>();
        private OrderTicket _orderticket;
        private static readonly decimal LEVERAGE = new decimal(0.1);
        
        public DateTime _startDate { get; private set; }
        public DateTime _endDate { get; private set; }
        public string _name { get; set; }

        public override void Initialize()
        {
            //20180904
            _startDate = new DateTime(2018, 9, 1);
            _endDate = new DateTime(2018, 9, 15);
            _name = "SnowflakeBitMEXMeanReversionLimitAlgorithm";
            SetStartDate(_startDate);  //Set Start Date
            SetEndDate(_endDate);    //Set End Date
            SetCash(1000000);

            _xbtusd = AddCrypto("XBTUSD", Resolution.Tick, Market.GDAX);
            Securities["XBTUSD"].FeeModel = new RelativeFeeTransactionModel(new decimal(-0.0025));
            
            _xbtusd.SetMarginModel(new SecurityMarginModel());
        }

        public override void OnData(Slice data)
        {   
            if (data.Ticks["XBTUSD"].IsNullOrEmpty()) return;
            var ticks = data.Ticks["XBTUSD"].Where(t => t.TickType == TickType.Quote);
            if (ticks.IsNullOrEmpty()) return;
            var tick = ticks.Last();
            if (tick.BidPrice == 0) return;
            if (tick.AskPrice == 0) return;
            var quote = new Quote {Time = data.Time, BidPrice = tick.BidPrice, AskPrice = tick.AskPrice, MidPrice = (tick.BidPrice + tick.AskPrice)/ 2};
            _quotes.Add(quote);
            _quotes.RemoveAll(q => (data.Time - q.Time).TotalMinutes > MINUTES);
            if (_quotes.IsNullOrEmpty()) return;
            var firstQuote = _quotes.First();
            var rateOfChange = quote.MidPrice / firstQuote.MidPrice;
            
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
                _lastSignal = new Signal{Time = data.Time, Type = ENTRY, Quantity = -1 * Math.Sign(meanReversion) * Portfolio.Cash / quote.AskPrice * LEVERAGE};
                var side = _lastSignal.Quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell;
                Debug($"{side} {data.Time} meanReversion {meanReversion} quote: {quote.Time} {quote.MidPrice} firstQuote: {firstQuote.Time} {firstQuote.MidPrice}");
            }

            SyncOrders();
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
            Debug(orderEvent.ToString());
        }

        public override void OnEndOfAlgorithm()
        {
            Snowflake.Common.OnEndOfAlgorithm(this);
        }

        private class Quote
        {
            public DateTime Time { get; set; }
            public decimal BidPrice { get; set; }
            public decimal AskPrice { get; set; }
            public decimal MidPrice { get; set; }
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
 *
 *
 * 20180915 08:09:55.359 Trace:: STATISTICS:: Total Trades 52
20180915 08:09:55.361 Trace:: STATISTICS:: Average Win 0.15%
20180915 08:09:55.361 Trace:: STATISTICS:: Average Loss -0.27%
20180915 08:09:55.362 Trace:: STATISTICS:: Compounding Annual Return 34.387%
20180915 08:09:55.362 Trace:: STATISTICS:: Drawdown 0.600%
20180915 08:09:55.362 Trace:: STATISTICS:: Expectancy 0.011
20180915 08:09:55.362 Trace:: STATISTICS:: Net Profit 0.068%
20180915 08:09:55.362 Trace:: STATISTICS:: Sharpe Ratio 0
20180915 08:09:55.362 Trace:: STATISTICS:: Loss Rate 35%
20180915 08:09:55.362 Trace:: STATISTICS:: Win Rate 65%
20180915 08:09:55.362 Trace:: STATISTICS:: Profit-Loss Ratio 0.55
20180915 08:09:55.362 Trace:: STATISTICS:: Alpha 0
20180915 08:09:55.363 Trace:: STATISTICS:: Beta 0
20180915 08:09:55.363 Trace:: STATISTICS:: Annual Standard Deviation 0
20180915 08:09:55.363 Trace:: STATISTICS:: Annual Variance 0
20180915 08:09:55.363 Trace:: STATISTICS:: Information Ratio 0
20180915 08:09:55.363 Trace:: STATISTICS:: Tracking Error 0
20180915 08:09:55.363 Trace:: STATISTICS:: Treynor Ratio 0
20180915 08:09:55.363 Trace:: STATISTICS:: Total Fees $0.00
 */