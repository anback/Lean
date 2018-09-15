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
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Notifications;
using QuantConnect.Orders;
using QuantConnect.Orders.Fills;
using QuantConnect.Securities;
using QuantConnect.Securities.Crypto;
using QuantConnect.Util;
using static System.Diagnostics.Debug;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash. This is a skeleton
    /// framework you can use for designing an algorithm.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class SnowflakeBitMEXMeanReversionLimitAlgorithm : QCAlgorithm
    {
        private const string ENTRY = "ENTRY"; 
        private const string EXIT = "EXIT";
        private const string XBTUSD = "XBTUSD";
        private Signal _lastSignal = new Signal{Time = DateTime.Now, Type = EXIT};
        private static readonly decimal MEAN_REVERSION_THRESHOLD = new decimal(0.002);
        private Crypto _xbtusd;
        private const int MINUTES = 1;
        private decimal bidPrice = 0;
        private decimal askPrice = 0;
        private List<Quote> _quotes = new List<Quote>();
        private DateTime startDate;
        private DateTime endDate;

        public override void Initialize()
        {
            //20180904
            startDate = new DateTime(2018, 9, 13);
            endDate = new DateTime(2018, 9, 13);
            SetStartDate(startDate);  //Set Start Date
            SetEndDate(endDate);    //Set End Date
            SetCash(100000); 

            _xbtusd = AddCrypto("XBTUSD", Resolution.Tick, Market.GDAX);
            Securities["XBTUSD"].FeeModel = new ConstantFeeTransactionModel(0);
            
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
            var quote = new Quote {Time = data.Time, MidPrice = GetMidPrice(tick)};
            _quotes.Add(quote);
            _quotes.RemoveAll(q => (data.Time - q.Time).TotalMinutes > MINUTES);
            if (_quotes.IsNullOrEmpty()) return;
            var firstQuote = _quotes.First();
            var rateOfChange = quote.MidPrice / firstQuote.MidPrice;
            
            if (Portfolio.CashBook["XBT"].ConversionRate == 0) return;

            _lastSignal.isOld = _lastSignal.IsOld(data.Time);
            var orderDirection = GetDirection();
            
            var meanReversion = rateOfChange - 1;

            if (Math.Abs(meanReversion) < MEAN_REVERSION_THRESHOLD) return;
            if (Math.Abs(meanReversion) > (decimal) 0.2) return; //Stupid guard for weird data
            
            // !!!!!!!!!!!!!!!!!!!!!!alright next step is to define like hasRisenHigh/hasFallenLow variable over here and use it under here!!!! :)!!!!!!!!!!!!!!!!!!!!!!!!!!!
            if (Portfolio.Invested && _lastSignal.Type == ENTRY && _lastSignal.isOld) _lastSignal = new Signal{Time = data.Time, Type = EXIT};
            if (!Portfolio.Invested && _lastSignal.Type == EXIT) _lastSignal = new Signal{Time = data.Time, Type = EXIT}
            
            _lastSignal = new Signal{Time = data.Time, Type = ENTRY, OrderDirection = meanReversion > 0 ? OrderDirection.Sell : OrderDirection.Buy};

            Debug($"{_lastSignal.OrderDirection} {data.Time} meanReversion {meanReversion} quote: {quote.Time} {quote.MidPrice} firstQuote: {firstQuote.Time} {firstQuote.MidPrice}");
        }

        private OrderDirection? GetDirection()
        {
            
            if (_lastSignal.Type == EXIT && Portfolio.Invested) return null;
            if (_lastSignal.Type == EXIT && !Portfolio.Invested) return null;
            if (_lastSignal.Type == ENTRY && Portfolio.Invested) return null;
            if (_lastSignal.Type == ENTRY && !Portfolio.Invested) return _lastSignal.OrderDirection;
            
            throw new Exception("Whatever");
        }

        public override void OnEndOfAlgorithm()
        {
            var json = JsonConvert.SerializeObject(Portfolio.Transactions.TransactionRecord.ToArray());
            const string format = "yyyyMMdd";
            System.IO.File.WriteAllText($@"../../../snowflake/public/backtest.json", json);
            System.IO.File.WriteAllText($@"../../../snowflake/public/backtest_{startDate.ToString(format)}_{endDate.ToString(format)}.json", json);
            // Process.Start("/bin/bash", "-c open -a 'Google Chrome' http://localhost:3000");
        }

        static decimal GetMidPrice(Tick tick) => (tick.AskPrice + tick.BidPrice) / 2;

        private class Quote
        {
            public DateTime Time { get; set; }
            public decimal MidPrice { get; set; }
            public Func<DateTime, DateTime> GetTotalMinutes = (DateTime now) => now;
        }
        
        private class Signal
        {
            public DateTime Time { get; set; }
            public string Type { get; set; }

            public bool IsOld(DateTime time) { return (time - Time).TotalMinutes >= MINUTES; }
            public bool isOld { get; set; }
            public OrderDirection OrderDirection { get; set; }
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