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
using QuantConnect.Brokerages;
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
    public class SnowflakeBitMEXMeanReversionAlgorithm : QCAlgorithm
    {
        private const string BITMEX = "bitmex";
        private readonly Symbol _xbtusd = QuantConnect.Symbol.Create("XBTUSD", SecurityType.Crypto, Market.GDAX);
        private RateOfChangeRatio _rateOfChangeRatio;
        private const string ENTRY = "ENTRY"; 
        private const string EXIT = "EXIT";
        private Signal _lastSignal = new Signal{Time = DateTime.Now, Type = EXIT};
        private static readonly decimal MEAN_REVERSION_THRESHOLD = new decimal(0.001);
        private const int MINUTES = 1;

        public override void Initialize()
        {
            //20180904
            SetStartDate(2018, 9, 3);  //Set Start Date
            SetEndDate(2018, 9, 4);    //Set End Date
            SetCash(10000);
            SetCash("XBT", 1m, 7300m);
            // Find more symbols here: http://quantconnect.com/data
            AddCrypto("XBTUSD", Resolution.Tick, Market.GDAX);
            Securities["XBTUSD"].FeeModel = new ConstantFeeTransactionModel(0);
            _rateOfChangeRatio = ROCR("XBTUSD", 1, Resolution.Minute);

            SetBrokerageModel(BrokerageName.QuantConnectBrokerage);            
        }

        public override void OnData(Slice data)
        {
            if (Portfolio.Invested && _lastSignal.Type == ENTRY && (data.Time - _lastSignal.Time).Minutes > MINUTES)
            {
                _lastSignal = new Signal{Time = data.Time, Type = EXIT};
                SetHoldings(_xbtusd, 0);    
                Debug("Sold");
                return;
            }

            if (_lastSignal.Type == ENTRY) return;
            var meanReversion = _rateOfChangeRatio.Current.Value - 1;
            if (Math.Abs(meanReversion) < MEAN_REVERSION_THRESHOLD) return;
            _lastSignal = new Signal{Time = data.Time, Type = ENTRY};
            SetHoldings(_xbtusd, -1 * Math.Sign(meanReversion));
            Debug("Bought");
        }
    }
}
