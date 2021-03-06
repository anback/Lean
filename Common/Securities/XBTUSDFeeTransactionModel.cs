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
using System.Diagnostics;
using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides a transaction model that always returns the same order fee.
    /// </summary>
    public sealed class XBTUSDFeeTransactionModel : SecurityTransactionModel
    {
        private const decimal _fee = 0.00075m;

        /// <inheritdoc />
        /// <summary>
        /// Returns the constant fee for the model
        /// </summary>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to compute fees for</param>
        /// <returns>The cost of the order in units of the account currency</returns>
        public override decimal GetOrderFee(Security security, Order order)
        {
            Console.WriteLine($"{order.Type.ToString()}");
            if (order.Type == OrderType.Market)
            {

                return 7000 * order.Quantity * _fee;
            };
            return 0;
        }
    }
}