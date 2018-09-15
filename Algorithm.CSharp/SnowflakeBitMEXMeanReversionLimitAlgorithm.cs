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
using System.Linq;
using Newtonsoft.Json;
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
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
    public class SnowflakeBitMEXMeanReversionAlgorithm : QCAlgorithm
    {
        private const string BITMEX = "bitmex";
        private const string ENTRY = "ENTRY"; 
        private const string EXIT = "EXIT";
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
            startDate = new DateTime(2018, 9, 10);
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
            if (Portfolio.Invested && _lastSignal.Type == ENTRY && (data.Time - _lastSignal.Time).TotalMinutes >= MINUTES)
            {
                _lastSignal = new Signal{Time = data.Time, Type = EXIT};
                SetHoldings(_xbtusd.Symbol, 0);
                return;
            }

            if (_lastSignal.Type == ENTRY) return;
            var meanReversion = rateOfChange - 1;

            if (Math.Abs(meanReversion) < MEAN_REVERSION_THRESHOLD) return;
            if (Math.Abs(meanReversion) > (decimal) 0.2) return; //Stupid guard for weird data
            _lastSignal = new Signal{Time = data.Time, Type = ENTRY};
            SetHoldings(_xbtusd.Symbol, -1 * Math.Sign(meanReversion));

            var side = -1 * Math.Sign(meanReversion) == 1 ? "Bought" : "Sold";
            Debug($"{side} {data.Time} meanReversion {meanReversion} quote: {quote.Time} {quote.MidPrice} firstQuote: {firstQuote.Time} {firstQuote.MidPrice}");
        }

        public override void OnEndOfAlgorithm()
        {
            var json = JsonConvert.SerializeObject(Portfolio.Transactions.TransactionRecord.ToArray());
            const string format = "yyyyMMdd";
            System.IO.File.WriteAllText($@"../../../snowflake/public/backtest.json", json);
            System.IO.File.WriteAllText($@"../../../snowflake/public/backtest_{startDate.ToString(format)}_{endDate.ToString(format)}.json", json);
        }

        static decimal GetMidPrice(Tick tick) => (tick.AskPrice + tick.BidPrice) / 2;
    }

    internal class Quote
    {
        public DateTime Time { get; set; }
        public decimal MidPrice { get; set; }
    }
}

/*
 *
 *
 * 20180914 21:47:58.709 Trace:: STATISTICS:: Total Trades 146
20180914 21:47:58.709 Trace:: STATISTICS:: Average Win 0.08%
20180914 21:47:58.709 Trace:: STATISTICS:: Average Loss -0.13%
20180914 21:47:58.709 Trace:: STATISTICS:: Compounding Annual Return -99.756%
20180914 21:47:58.709 Trace:: STATISTICS:: Drawdown 2.200%
20180914 21:47:58.709 Trace:: STATISTICS:: Expectancy -0.172
20180914 21:47:58.709 Trace:: STATISTICS:: Net Profit -1.635%
20180914 21:47:58.709 Trace:: STATISTICS:: Sharpe Ratio -12.129
20180914 21:47:58.709 Trace:: STATISTICS:: Loss Rate 48%
20180914 21:47:58.709 Trace:: STATISTICS:: Win Rate 52%
20180914 21:47:58.709 Trace:: STATISTICS:: Profit-Loss Ratio 0.59
20180914 21:47:58.709 Trace:: STATISTICS:: Alpha -3.969
20180914 21:47:58.709 Trace:: STATISTICS:: Beta 276.492
20180914 21:47:58.709 Trace:: STATISTICS:: Annual Standard Deviation 0.17
20180914 21:47:58.709 Trace:: STATISTICS:: Annual Variance 0.029
20180914 21:47:58.709 Trace:: STATISTICS:: Information Ratio -12.214
20180914 21:47:58.709 Trace:: STATISTICS:: Tracking Error 0.169
20180914 21:47:58.709 Trace:: STATISTICS:: Treynor Ratio -0.007
20180914 21:47:58.709 Trace:: STATISTICS:: Total Fees $0.00

 */