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
using QuantConnect.Data;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash. This is a skeleton
    /// framework you can use for designing an algorithm.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="using quantconnect" />
    /// <meta name="tag" content="trading and orders" />
    public class SnowflakeMeanReversionAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private readonly Symbol _spy = QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA);
        private RateOfChangeRatio _rateOfChangeRatio;
        private const string ENTRY = "ENTRY"; 
        private const string EXIT = "EXIT";
        private Signal _lastSignal = new Signal{Time = DateTime.Now, Type = EXIT};
        private static readonly decimal MEAN_REVERSION_THRESHOLD = new decimal(0.001);
        private const int MINUTES = 1;

        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            // Find more symbols here: http://quantconnect.com/data
            AddEquity("SPY", Resolution.Second);
            Securities["SPY"].FeeModel = new ConstantFeeTransactionModel((decimal) 0);
            _rateOfChangeRatio = ROCR("SPY", 1, Resolution.Minute);
        }

        public override void OnData(Slice data)
        {
            if (Portfolio.Invested && _lastSignal.Type == ENTRY && (data.Time - _lastSignal.Time).Minutes > MINUTES)
            {
                _lastSignal = new Signal{Time = data.Time, Type = EXIT};
                SetHoldings(_spy, 0);    
                return;
            }

            if (_lastSignal.Type == ENTRY) return;
            var meanReversion = _rateOfChangeRatio.Current.Value - 1;
            if (Math.Abs(meanReversion) < MEAN_REVERSION_THRESHOLD) return;
            _lastSignal = new Signal{Time = data.Time, Type = ENTRY};
            SetHoldings(_spy, -1 * Math.Sign(meanReversion));
        }

        public bool CanRunLocally { get; } = true;
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Trades", "1"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "263.153%"},
            {"Drawdown", "2.200%"},
            {"Expectancy", "0"},
            {"Net Profit", "1.663%"},
            {"Sharpe Ratio", "4.41"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0.007"},
            {"Beta", "76.118"},
            {"Annual Standard Deviation", "0.192"},
            {"Annual Variance", "0.037"},
            {"Information Ratio", "4.354"},
            {"Tracking Error", "0.192"},
            {"Treynor Ratio", "0.011"},
            {"Total Fees", "$3.26"}
        };
    }
}
