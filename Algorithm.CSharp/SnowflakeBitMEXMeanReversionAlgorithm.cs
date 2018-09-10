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
        private RateOfChangeRatio _rateOfChangeRatio;
        private const string ENTRY = "ENTRY"; 
        private const string EXIT = "EXIT";
        private Signal _lastSignal = new Signal{Time = DateTime.Now, Type = EXIT};
        private static readonly decimal MEAN_REVERSION_THRESHOLD = new decimal(0.002);
        private Crypto _xbtusd;
        private const int MINUTES = 1;
        private decimal bidPrice = 0;
        private decimal askPrice = 0;
        private Quote _quote = new Quote {Time = DateTime.Now, MidPrice = 0};
        private List<Quote> _quotes = new List<Quote>();

        public override void Initialize()
        {
            //20180904
            SetStartDate(2018, 9, 5);  //Set Start Date
            SetEndDate(2018, 9, 5);    //Set End Date
            SetCash(100000); 

            _xbtusd = AddCrypto("XBTUSD", Resolution.Tick, Market.GDAX);
            Securities["XBTUSD"].FeeModel = new ConstantFeeTransactionModel(0);
            _rateOfChangeRatio = ROCR("XBTUSD", 1, Resolution.Minute);
            
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
                // Debug($"Closed {data.Time} _lastSignal.Time {_lastSignal.Time}");
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
            // Portfolio.Transactions.TransactionRecord
        }

        static decimal GetMidPrice(Tick tick) => (tick.AskPrice + tick.BidPrice) / 2;
    }

    internal class Quote
    {
        public DateTime Time { get; set; }
        public decimal MidPrice { get; set; }
    }
}
